using Kalon.Back.Data;
using Kalon.Back.Models;
using Kalon.Back.Services;
using Microsoft.EntityFrameworkCore;

namespace Kalon.Back.Tests;

public class DonationServiceTests
{
    private static ApplicationDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task GetAllByContactIdAsync_ReturnsEveryDonationForContact()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var organizationId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var otherContactId = Guid.NewGuid();
        db.Donations.AddRange(
            new Donation
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                ContactId = contactId,
                Amount = 1m,
                Date = DateTime.UtcNow.AddDays(-2),
                DonationType = "financial",
                CreatedAt = DateTime.UtcNow
            },
            new Donation
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                ContactId = contactId,
                Amount = 2m,
                Date = DateTime.UtcNow.AddDays(-1),
                DonationType = "financial",
                CreatedAt = DateTime.UtcNow
            },
            new Donation
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                ContactId = otherContactId,
                Amount = 100m,
                Date = DateTime.UtcNow,
                DonationType = "financial",
                CreatedAt = DateTime.UtcNow
            });
        db.Contacts.AddRange(
            new Contact
            {
                Id = contactId,
                OrganizationId = organizationId,
                Kind = ContactKinds.Donor,
                Firstname = "A",
                Lastname = "B",
                Email = "a@b.com",
                CreatedAt = DateTime.UtcNow
            },
            new Contact
            {
                Id = otherContactId,
                OrganizationId = organizationId,
                Kind = ContactKinds.Donor,
                Firstname = "C",
                Lastname = "D",
                Email = "c@d.com",
                CreatedAt = DateTime.UtcNow
            });
        await db.SaveChangesAsync();

        var service = new DonationService(db);
        var result = await service.GetAllByContactIdAsync(
            organizationId,
            contactId,
            new DonationListFilters(null, null, null, null, null),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(2, result!.Items.Count);
        Assert.DoesNotContain(result.Items, d => d.ContactId == otherContactId);
    }
}
