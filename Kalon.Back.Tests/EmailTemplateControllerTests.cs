using Kalon.Back.Controllers;
using Kalon.Back.Data;
using Kalon.Back.DTOs;
using Kalon.Back.Models;
using Kalon.Back.Services.OrganizationAccess;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public class EmailTemplateControllerTests
{
    private static EmailTemplateController CreateController(ApplicationDbContext dbContext) =>
        new(dbContext, new UserOrganizationAccessService(dbContext));

    private static ApplicationDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    private static User CreateUser(Guid id, string email)
    {
        return new User
        {
            Id = id,
            MeranId = Guid.NewGuid(),
            Firstname = "Test",
            Lastname = "User",
            Email = email,
            AssociationName = "Asso",
            PasswordHash = "hash",
            Salt = "salt"
        };
    }

    private static Organization CreateOrganization(Guid id, Guid userId, User user)
    {
        return new Organization
        {
            Id = id,
            Name = "Test Organization",
            Email = "org@test.local",
            UserId = userId,
            User = user,
            RNA = "W442009999",
            SIRET = "12345678901234",
            FiscalStatus = FiscalStatus.GeneralInterest,
            DefaultReceiptFrequency = ReceiptFrequency.Annually,
            CreatedAt = DateTime.UtcNow
        };
    }

    [Fact]
    public async Task Create_ReturnsCreated_WhenRequestIsValid()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, "owner@example.com");
        var organization = CreateOrganization(Guid.NewGuid(), userId, user);
        dbContext.Users.Add(user);
        dbContext.Organizations.Add(organization);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        var request = new EmailTemplateUpsertRequest
        {
            Name = "Reminder #1",
            Subject = "Please donate",
            Body = "Content",
            EmailTemplateType = EmailTemplateTypes.Reminder
        };

        var result = await controller.Create(userId, request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var payload = Assert.IsType<EmailTemplateResponse>(created.Value);
        Assert.Equal("Reminder #1", payload.Name);
        Assert.Equal(EmailTemplateTypes.Reminder, payload.EmailTemplateType);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenTypeInvalid()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, "owner@example.com");
        var organization = CreateOrganization(Guid.NewGuid(), userId, user);
        dbContext.Users.Add(user);
        dbContext.Organizations.Add(organization);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        var request = new EmailTemplateUpsertRequest
        {
            Name = "Invalid",
            Subject = "Subject",
            Body = "Body",
            EmailTemplateType = "not_valid"
        };

        var result = await controller.Create(userId, request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetAll_ReturnsOnlyTemplatesForOrganization()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, "owner@example.com");
        var organizationId = Guid.NewGuid();
        var organization = CreateOrganization(organizationId, userId, user);

        var otherUser = CreateUser(Guid.NewGuid(), "other@example.com");
        var otherOrganizationId = Guid.NewGuid();
        var otherOrganization = CreateOrganization(otherOrganizationId, otherUser.Id, otherUser);

        dbContext.Users.AddRange(user, otherUser);
        dbContext.Organizations.AddRange(organization, otherOrganization);
        dbContext.EmailTemplates.AddRange(
            new EmailTemplate
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Name = "Main",
                Subject = "Subject",
                Body = "Body",
                EmailTemplateType = EmailTemplateTypes.ThankYou,
                CreatedAt = DateTime.UtcNow
            },
            new EmailTemplate
            {
                Id = Guid.NewGuid(),
                OrganizationId = otherOrganizationId,
                Name = "Other",
                Subject = "Subject",
                Body = "Body",
                EmailTemplateType = EmailTemplateTypes.ThankYou,
                CreatedAt = DateTime.UtcNow
            });
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        var result = await controller.GetAll(userId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<List<EmailTemplateResponse>>(ok.Value);
        Assert.Single(payload);
        Assert.Equal("Main", payload[0].Name);
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenTemplateMissing()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, "owner@example.com");
        var organization = CreateOrganization(Guid.NewGuid(), userId, user);
        dbContext.Users.Add(user);
        dbContext.Organizations.Add(organization);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        var result = await controller.GetById(userId, Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Update_ReturnsOk_WhenRequestIsValid()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, "owner@example.com");
        var organizationId = Guid.NewGuid();
        var organization = CreateOrganization(organizationId, userId, user);
        var template = new EmailTemplate
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = "Before",
            Subject = "Before",
            Body = "Before",
            EmailTemplateType = EmailTemplateTypes.Other,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        dbContext.Users.Add(user);
        dbContext.Organizations.Add(organization);
        dbContext.EmailTemplates.Add(template);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        var request = new EmailTemplateUpsertRequest
        {
            Name = "After",
            Subject = "After subject",
            Body = "After body",
            EmailTemplateType = EmailTemplateTypes.Seasonal
        };

        var result = await controller.Update(userId, template.Id, request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<EmailTemplateResponse>(ok.Value);
        Assert.Equal("After", payload.Name);
        Assert.Equal(EmailTemplateTypes.Seasonal, payload.EmailTemplateType);
        Assert.NotNull(payload.UpdatedAt);
    }

    [Fact]
    public async Task Delete_ReturnsNoContent_AndRemovesTemplate()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, "owner@example.com");
        var organizationId = Guid.NewGuid();
        var organization = CreateOrganization(organizationId, userId, user);
        var template = new EmailTemplate
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = "Delete me",
            Subject = "Sub",
            Body = "Body",
            EmailTemplateType = EmailTemplateTypes.Other,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Users.Add(user);
        dbContext.Organizations.Add(organization);
        dbContext.EmailTemplates.Add(template);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        var result = await controller.Delete(userId, template.Id, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        var exists = await dbContext.EmailTemplates.AnyAsync(x => x.Id == template.Id);
        Assert.False(exists);
    }
}
