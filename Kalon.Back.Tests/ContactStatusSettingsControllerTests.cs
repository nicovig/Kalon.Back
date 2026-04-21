using Kalon.Back.Controllers;
using Kalon.Back.Data;
using Kalon.Back.DTOs;
using Kalon.Back.Models;
using Kalon.Back.Services.OrganizationAccess;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kalon.Back.Tests;

public class ContactStatusSettingsControllerTests
{
    private static ContactStatusSettingsController CreateController(ApplicationDbContext dbContext) =>
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
    public async Task Get_ReturnsBadRequest_WhenUserIdIsEmpty()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var controller = CreateController(dbContext);

        var result = await controller.Get(Guid.Empty, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequest.StatusCode);
    }

    [Fact]
    public async Task Get_ReturnsNotFound_WhenOrganizationDoesNotExistForUser()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var controller = CreateController(dbContext);

        var result = await controller.Get(Guid.NewGuid(), CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(404, notFound.StatusCode);
    }

    [Fact]
    public async Task Get_ReturnsDefaults_WhenSettingsDoNotExist()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, "owner@example.com");
        var organization = CreateOrganization(Guid.NewGuid(), userId, user);
        dbContext.Users.Add(user);
        dbContext.Organizations.Add(organization);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        var result = await controller.Get(userId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<ContactStatusSettings>(ok.Value);
        Assert.Equal(30, payload.NewDurationDays);
        Assert.Equal(12, payload.ToRemindAfterMonths);
        Assert.Equal(24, payload.InactiveAfterMonths);
    }

    [Fact]
    public async Task Upsert_CreatesSettings_WhenMissing()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, "owner@example.com");
        var organization = CreateOrganization(Guid.NewGuid(), userId, user);
        dbContext.Users.Add(user);
        dbContext.Organizations.Add(organization);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        var result = await controller.Upsert(userId, new ContactStatusSettingsUpsertRequest
        {
            NewDurationDays = 20,
            ToRemindAfterMonths = 10,
            InactiveAfterMonths = 18
        }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<ContactStatusSettings>(ok.Value);
        Assert.Equal(20, payload.NewDurationDays);
        Assert.Equal(10, payload.ToRemindAfterMonths);
        Assert.Equal(18, payload.InactiveAfterMonths);
    }

    [Fact]
    public async Task Upsert_ReturnsBadRequest_WhenInvalidRuleOrder()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, "owner@example.com");
        var organization = CreateOrganization(Guid.NewGuid(), userId, user);
        dbContext.Users.Add(user);
        dbContext.Organizations.Add(organization);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        var result = await controller.Upsert(userId, new ContactStatusSettingsUpsertRequest
        {
            NewDurationDays = 30,
            ToRemindAfterMonths = 24,
            InactiveAfterMonths = 12
        }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ResetToDefaults_ResetsPersistedValues()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, "owner@example.com");
        var organizationId = Guid.NewGuid();
        var organization = CreateOrganization(organizationId, userId, user);
        dbContext.Users.Add(user);
        dbContext.Organizations.Add(organization);
        dbContext.ContactStatusSettings.Add(new ContactStatusSettings
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            NewDurationDays = 3,
            ToRemindAfterMonths = 2,
            InactiveAfterMonths = 4,
            CreatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        var result = await controller.ResetToDefaults(userId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<ContactStatusSettings>(ok.Value);
        Assert.Equal(30, payload.NewDurationDays);
        Assert.Equal(12, payload.ToRemindAfterMonths);
        Assert.Equal(24, payload.InactiveAfterMonths);
    }
}
