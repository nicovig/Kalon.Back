using Kalon.Back.Data;
using Kalon.Back.Models;
using Kalon.Back.Services.OrganizationAccess;
using Microsoft.EntityFrameworkCore;

namespace Kalon.Back.Tests;

public class UserOrganizationAccessServiceTests
{
    private static ApplicationDbContext CreateDb(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsInvalidUserId_WhenUserIdEmpty()
    {
        using var db = CreateDb(Guid.NewGuid().ToString());
        var sut = new UserOrganizationAccessService(db);
        var result = await sut.ResolveAsync(Guid.Empty);
        Assert.IsType<OrganizationAccessOutcome.InvalidUserId>(result);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsOrganizationNotFound_WhenUserHasNoOrganization()
    {
        using var db = CreateDb(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = userId,
            MeranId = Guid.NewGuid(),
            Firstname = "A",
            Lastname = "B",
            Email = "a@b.c",
            AssociationName = "X",
            PasswordHash = "h",
            Salt = "s"
        });
        await db.SaveChangesAsync();

        var sut = new UserOrganizationAccessService(db);
        var result = await sut.ResolveAsync(userId);
        Assert.IsType<OrganizationAccessOutcome.OrganizationNotFoundForUser>(result);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsOk_WhenOrganizationLinkedToUser()
    {
        using var db = CreateDb(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            MeranId = Guid.NewGuid(),
            Firstname = "A",
            Lastname = "B",
            Email = "a@b.c",
            AssociationName = "X",
            PasswordHash = "h",
            Salt = "s"
        };
        db.Users.Add(user);
        db.Organizations.Add(new Organization
        {
            Id = orgId,
            Name = "Org",
            Email = "o@b.c",
            UserId = userId,
            User = user,
            RNA = "W442009999",
            SIRET = "12345678901234",
            FiscalStatus = FiscalStatus.GeneralInterest,
            DefaultReceiptFrequency = ReceiptFrequency.Annually,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var sut = new UserOrganizationAccessService(db);
        var result = await sut.ResolveAsync(userId);
        var ok = Assert.IsType<OrganizationAccessOutcome.Ok>(result);
        Assert.Equal(orgId, ok.OrganizationId);
    }
}
