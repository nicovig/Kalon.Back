using Kalon.Back.Controllers;
using Kalon.Back.Data;
using Kalon.Back.DTOs;
using Kalon.Back.Models;
using Kalon.Back.Services;
using Kalon.Back.Services.OrganizationAccess;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Kalon.Back.Tests;

public class TagControllerTests
{
    private static ApplicationDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    private static TagController CreateController(ApplicationDbContext dbContext) =>
        new(new TagService(dbContext), new UserOrganizationAccessService(dbContext));

    private static void SetAuthenticatedUser(ControllerBase controller, Guid userId)
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim("sub", userId.ToString())
                ], "TestAuth"))
            }
        };
    }

    private static User CreateUser(Guid id) => new()
    {
        Id = id,
        MeranId = Guid.NewGuid(),
        Firstname = "Test",
        Lastname = "User",
        Email = $"{id}@example.com",
        AssociationName = "Asso",
        PasswordHash = "hash",
        Salt = "salt"
    };

    private static Organization CreateOrganization(Guid id, Guid userId, User user) => new()
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

    [Fact]
    public async Task Create_ReturnsCreated_WhenValid()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var user = CreateUser(userId);
        var org = CreateOrganization(Guid.NewGuid(), userId, user);
        db.Users.Add(user);
        db.Organizations.Add(org);
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        SetAuthenticatedUser(controller, userId);

        var result = await controller.Create(
            new TagCreateRequest { Name = "VIP", Color = "#FF0000" },
            CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var payload = Assert.IsType<TagResponse>(created.Value);
        Assert.Equal("VIP", payload.Name);
        Assert.Equal("#FF0000", payload.Color);
        Assert.Equal(org.Id, payload.OrganizationId);
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenNameAlreadyExists()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var user = CreateUser(userId);
        var org = CreateOrganization(Guid.NewGuid(), userId, user);
        db.Users.Add(user);
        db.Organizations.Add(org);
        db.Tags.Add(new Tag
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            Name = "VIP",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        SetAuthenticatedUser(controller, userId);

        var result = await controller.Create(
            new TagCreateRequest { Name = "vip" },
            CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task GetAll_ReturnsOnlyOrganizationTags()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var user = CreateUser(userId);
        var org = CreateOrganization(Guid.NewGuid(), userId, user);
        var otherUser = CreateUser(Guid.NewGuid());
        var otherOrg = CreateOrganization(Guid.NewGuid(), otherUser.Id, otherUser);

        db.Users.AddRange(user, otherUser);
        db.Organizations.AddRange(org, otherOrg);
        db.Tags.AddRange(
            new Tag { Id = Guid.NewGuid(), OrganizationId = org.Id, Name = "A", CreatedAt = DateTime.UtcNow },
            new Tag { Id = Guid.NewGuid(), OrganizationId = org.Id, Name = "B", CreatedAt = DateTime.UtcNow },
            new Tag { Id = Guid.NewGuid(), OrganizationId = otherOrg.Id, Name = "Other", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        SetAuthenticatedUser(controller, userId);

        var result = await controller.GetAll(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsAssignableFrom<IEnumerable<TagResponse>>(ok.Value).ToList();
        Assert.Equal(2, payload.Count);
        Assert.Equal(["A", "B"], payload.Select(t => t.Name).ToList());
    }

    [Fact]
    public async Task Update_ReturnsOk_WhenValid()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var user = CreateUser(userId);
        var org = CreateOrganization(Guid.NewGuid(), userId, user);
        var tagId = Guid.NewGuid();
        db.Users.Add(user);
        db.Organizations.Add(org);
        db.Tags.Add(new Tag
        {
            Id = tagId,
            OrganizationId = org.Id,
            Name = "Old",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        SetAuthenticatedUser(controller, userId);

        var result = await controller.Update(
            tagId,
            new TagUpdateRequest { Name = "New", Color = "#00FF00" },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<TagResponse>(ok.Value);
        Assert.Equal("New", payload.Name);
        Assert.Equal("#00FF00", payload.Color);
    }

    [Fact]
    public async Task Delete_ReturnsNoContent_AndRemovesTag()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var user = CreateUser(userId);
        var org = CreateOrganization(Guid.NewGuid(), userId, user);
        var tagId = Guid.NewGuid();
        db.Users.Add(user);
        db.Organizations.Add(org);
        db.Tags.Add(new Tag
        {
            Id = tagId,
            OrganizationId = org.Id,
            Name = "ToDelete",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        SetAuthenticatedUser(controller, userId);

        var result = await controller.Delete(tagId, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.False(await db.Tags.AnyAsync(t => t.Id == tagId));
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenMissing()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var user = CreateUser(userId);
        var org = CreateOrganization(Guid.NewGuid(), userId, user);
        db.Users.Add(user);
        db.Organizations.Add(org);
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        SetAuthenticatedUser(controller, userId);

        var result = await controller.Delete(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }
}
