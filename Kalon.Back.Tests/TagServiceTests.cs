using Kalon.Back.Data;
using Kalon.Back.DTOs;
using Kalon.Back.Models;
using Kalon.Back.Services;
using Microsoft.EntityFrameworkCore;

namespace Kalon.Back.Tests;

public class TagServiceTests
{
    private static ApplicationDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    private static Organization CreateOrganization(Guid id) => new()
    {
        Id = id,
        Name = "Org",
        Email = "org@test.local",
        UserId = Guid.NewGuid(),
        RNA = "W1",
        SIRET = "1",
        FiscalStatus = FiscalStatus.GeneralInterest,
        DefaultReceiptFrequency = ReceiptFrequency.Annually,
        CreatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task CreateAsync_CreatesTag()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var orgId = Guid.NewGuid();
        db.Organizations.Add(CreateOrganization(orgId));
        await db.SaveChangesAsync();

        var service = new TagService(db);
        var result = await service.CreateAsync(
            orgId,
            new TagCreateRequest { Name = " VIP ", Color = " #abc " },
            CancellationToken.None);

        Assert.Equal(TagOperationStatus.Success, result.Status);
        Assert.NotNull(result.Tag);
        Assert.Equal("VIP", result.Tag.Name);
        Assert.Equal("#abc", result.Tag.Color);
    }

    [Fact]
    public async Task CreateAsync_ReturnsInvalid_WhenNameEmpty()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var service = new TagService(db);

        var result = await service.CreateAsync(
            Guid.NewGuid(),
            new TagCreateRequest { Name = "  " },
            CancellationToken.None);

        Assert.Equal(TagOperationStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task CreateAsync_ReturnsConflict_WhenDuplicateName()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var orgId = Guid.NewGuid();
        db.Organizations.Add(CreateOrganization(orgId));
        db.Tags.Add(new Tag
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            Name = "VIP",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new TagService(db);
        var result = await service.CreateAsync(
            orgId,
            new TagCreateRequest { Name = "vip" },
            CancellationToken.None);

        Assert.Equal(TagOperationStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task DeleteAsync_UnlinksContacts()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var orgId = Guid.NewGuid();
        var tag = new Tag
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            Name = "VIP",
            CreatedAt = DateTime.UtcNow
        };
        var contact = new Contact
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            Firstname = "John",
            Lastname = "Doe",
            CreatedAt = DateTime.UtcNow,
            Tags = [tag]
        };

        db.Organizations.Add(CreateOrganization(orgId));
        db.Tags.Add(tag);
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();

        var service = new TagService(db);
        var result = await service.DeleteAsync(orgId, tag.Id, CancellationToken.None);

        Assert.Equal(TagOperationStatus.Success, result.Status);
        Assert.False(await db.Tags.AnyAsync(t => t.Id == tag.Id));

        var reloaded = await db.Contacts.Include(c => c.Tags).SingleAsync(c => c.Id == contact.Id);
        Assert.Empty(reloaded.Tags);
    }

    [Fact]
    public async Task ResolveTagsForOrganizationAsync_ReturnsOnlyMatchingOrgTags()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var orgId = Guid.NewGuid();
        var otherOrgId = Guid.NewGuid();
        var tag1 = new Tag { Id = Guid.NewGuid(), OrganizationId = orgId, Name = "A", CreatedAt = DateTime.UtcNow };
        var tag2 = new Tag { Id = Guid.NewGuid(), OrganizationId = otherOrgId, Name = "B", CreatedAt = DateTime.UtcNow };

        db.Organizations.AddRange(CreateOrganization(orgId), CreateOrganization(otherOrgId));
        db.Tags.AddRange(tag1, tag2);
        await db.SaveChangesAsync();

        var service = new TagService(db);
        var tags = await service.ResolveTagsForOrganizationAsync(
            orgId,
            [tag1.Id, tag2.Id],
            CancellationToken.None);

        Assert.Single(tags);
        Assert.Equal(tag1.Id, tags[0].Id);
    }
}
