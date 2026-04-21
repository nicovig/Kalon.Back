using Kalon.Back.Controllers;
using Kalon.Back.Data;
using Kalon.Back.DTOs;
using Kalon.Back.Models;
using Kalon.Back.Services.OrganizationAccess;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kalon.Back.Tests;


public class OrganizationCustomContentControllerTests
{
    private static OrganizationCustomContentController CreateController(ApplicationDbContext dbContext) =>
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
    public async Task Create_ReturnsCreated_WhenTextRequestIsValid()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, "owner@example.com");
        var organization = CreateOrganization(Guid.NewGuid(), userId, user);
        dbContext.Users.Add(user);
        dbContext.Organizations.Add(organization);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        var request = new ContentBlockUpsertRequest
        {
            Name = "Intro",
            Kind = "text",
            Content = "Hello {{firstname}}",
            UsableInEmail = true,
            UsableInReceipt = false
        };

        var result = await controller.CreateContentBlock(userId, request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var payload = Assert.IsType<ContentBlockResponse>(created.Value);
        Assert.Equal("text", payload.Kind);
        Assert.Equal("Hello {{firstname}}", payload.Content);
        Assert.False(payload.UsableInReceipt);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenImageMissingPath()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, "owner@example.com");
        var organization = CreateOrganization(Guid.NewGuid(), userId, user);
        dbContext.Users.Add(user);
        dbContext.Organizations.Add(organization);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        var request = new ContentBlockUpsertRequest
        {
            Name = "Signature",
            Kind = "image",
            MimeType = "image/png"
        };

        var result = await controller.CreateContentBlock(userId, request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetAll_ReturnsOnlyBlocksForOrganization()
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
        dbContext.ContentBlocks.AddRange(
            new ContentBlock
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Name = "Mine",
                Kind = "text",
                Content = "A",
                CreatedAt = DateTime.UtcNow
            },
            new ContentBlock
            {
                Id = Guid.NewGuid(),
                OrganizationId = otherOrganizationId,
                Name = "Other",
                Kind = "text",
                Content = "B",
                CreatedAt = DateTime.UtcNow
            });
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        var result = await controller.GetContentBlocks(userId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<List<ContentBlockResponse>>(ok.Value);
        Assert.Single(payload);
        Assert.Equal("Mine", payload[0].Name);
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenMissing()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, "owner@example.com");
        var organization = CreateOrganization(Guid.NewGuid(), userId, user);
        dbContext.Users.Add(user);
        dbContext.Organizations.Add(organization);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        var result = await controller.GetContentBlockById(userId, Guid.NewGuid(), CancellationToken.None);

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
        var block = new ContentBlock
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = "Before",
            Kind = "text",
            Content = "Before content",
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        dbContext.Users.Add(user);
        dbContext.Organizations.Add(organization);
        dbContext.ContentBlocks.Add(block);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        var request = new ContentBlockUpsertRequest
        {
            Name = "After",
            Kind = "signature",
            StoredPath = "/blocks/signature.png",
            MimeType = "image/png",
            UsableInEmail = false,
            UsableInReceipt = true
        };

        var result = await controller.UpdateContentBlock(userId, block.Id, request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<ContentBlockResponse>(ok.Value);
        Assert.Equal("signature", payload.Kind);
        Assert.Equal("/blocks/signature.png", payload.StoredPath);
        Assert.False(payload.UsableInEmail);
        Assert.NotNull(payload.UpdatedAt);
    }

    [Fact]
    public async Task Delete_ReturnsNoContent_AndRemovesBlock()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, "owner@example.com");
        var organizationId = Guid.NewGuid();
        var organization = CreateOrganization(organizationId, userId, user);
        var block = new ContentBlock
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = "Delete me",
            Kind = "text",
            Content = "To delete",
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Users.Add(user);
        dbContext.Organizations.Add(organization);
        dbContext.ContentBlocks.Add(block);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        var result = await controller.DeleteContentBlock(userId, block.Id, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        var exists = await dbContext.ContentBlocks.AnyAsync(x => x.Id == block.Id);
        Assert.False(exists);
    }

    [Fact]
    public async Task CreateOrganizationLogo_ReturnsCreated_WhenValid()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, "owner@example.com");
        var organizationId = Guid.NewGuid();
        var organization = CreateOrganization(organizationId, userId, user);
        dbContext.Users.Add(user);
        dbContext.Organizations.Add(organization);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        var request = new OrganizationLogoUpsertRequest
        {
            FileName = "logo.png",
            StoredPath = "/logos/logo.png",
            MimeType = "image/png",
            FileSizeBytes = 1234
        };

        var result = await controller.CreateOrganizationLogo(userId, request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var payload = Assert.IsType<OrganizationLogoResponse>(created.Value);
        Assert.Equal("logo.png", payload.FileName);
    }

    [Fact]
    public async Task UpdateOrganizationLogo_ReturnsOk_WhenExists()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, "owner@example.com");
        var organizationId = Guid.NewGuid();
        var organization = CreateOrganization(organizationId, userId, user);
        var logo = new OrganizationLogo
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            FileName = "before.png",
            StoredPath = "/logos/before.png",
            MimeType = "image/png",
            FileSizeBytes = 100,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.Users.Add(user);
        dbContext.Organizations.Add(organization);
        dbContext.OrganizationLogos.Add(logo);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        var request = new OrganizationLogoUpsertRequest
        {
            FileName = "after.png",
            StoredPath = "/logos/after.png",
            MimeType = "image/png",
            FileSizeBytes = 200
        };

        var result = await controller.UpdateOrganizationLogo(userId, request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<OrganizationLogoResponse>(ok.Value);
        Assert.Equal("after.png", payload.FileName);
        Assert.NotNull(payload.UpdatedAt);
    }

    [Fact]
    public async Task DeleteOrganizationLogo_ReturnsNoContent_WhenExists()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, "owner@example.com");
        var organizationId = Guid.NewGuid();
        var organization = CreateOrganization(organizationId, userId, user);
        var logo = new OrganizationLogo
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            FileName = "to-delete.png",
            StoredPath = "/logos/to-delete.png",
            MimeType = "image/png",
            FileSizeBytes = 200,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.Users.Add(user);
        dbContext.Organizations.Add(organization);
        dbContext.OrganizationLogos.Add(logo);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        var result = await controller.DeleteOrganizationLogo(userId, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        var exists = await dbContext.OrganizationLogos.AnyAsync(x => x.OrganizationId == organizationId);
        Assert.False(exists);
    }
}
