using Kalon.Back.Data;
using Kalon.Back.Models;
using Kalon.Back.Services.Notification;
using Microsoft.EntityFrameworkCore;

namespace Kalon.Back.Tests;

public class NotificationDashboardServiceTests
{
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
            DefaultReceiptFrequency = ReceiptFrequency.Monthly,
            CreatedAt = DateTime.UtcNow
        };
    }

    [Fact]
    public async Task GetDashboardAsync_ComputesAllThreeCounters()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var user = CreateUser(userId, "owner@example.com");
        var organization = CreateOrganization(organizationId, userId, user);
        var now = DateTime.UtcNow;

        var contactToRemind = new Contact
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Kind = ContactKinds.Donor,
            Firstname = "Remind",
            Lastname = "Contact",
            Email = "r@x.com",
            CreatedAt = now.AddMonths(-18),
            PreferredFrequencySendingReceipt = "yearly"
        };
        var contactMonthlyDue = new Contact
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Kind = ContactKinds.Donor,
            Firstname = "Monthly",
            Lastname = "Due",
            Email = "m@x.com",
            CreatedAt = now.AddMonths(-6),
            PreferredFrequencySendingReceipt = "monthly"
        };

        dbContext.Users.Add(user);
        dbContext.Organizations.Add(organization);
        dbContext.ContactStatusSettings.Add(new ContactStatusSettings
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            NewDurationDays = 30,
            ToRemindAfterMonths = 12,
            InactiveAfterMonths = 24,
            CreatedAt = now
        });
        dbContext.Contacts.AddRange(contactToRemind, contactMonthlyDue);
        dbContext.Donations.AddRange(
            new Donation
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                ContactId = contactToRemind.Id,
                Amount = 10m,
                Date = now.AddMonths(-13),
                DonationType = "financial",
                CreatedAt = now.AddMonths(-13)
            },
            new Donation
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                ContactId = contactMonthlyDue.Id,
                Amount = 12m,
                Date = now.AddMonths(-1).AddDays(-2),
                DonationType = "financial",
                CreatedAt = now.AddMonths(-1)
            });
        dbContext.MailLogs.Add(new MailLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            ContactId = contactToRemind.Id,
            IsEmail = false,
            Subject = "paper",
            Body = "paper",
            Status = MailLogStatuses.Printed,
            CreatedAt = now
        });
        await dbContext.SaveChangesAsync();

        var service = new NotificationDashboardService(dbContext);
        var result = await service.GetDashboardAsync(organizationId, CancellationToken.None);

        Assert.Single(result.ContactsToRemind);
        Assert.Equal(contactToRemind.Id, result.ContactsToRemind[0].ContactId);
        Assert.Equal("Remind Contact", result.ContactsToRemind[0].DisplayName);
        Assert.Equal(2, result.ContactsToSendTaxReceipts.Count);
        Assert.Contains(result.ContactsToSendTaxReceipts, x => x.ContactId == contactToRemind.Id);
        Assert.Contains(result.ContactsToSendTaxReceipts, x => x.ContactId == contactMonthlyDue.Id);
        Assert.Equal(1, result.PhysicalLettersToSendCount);
    }

    [Fact]
    public async Task GetDashboardAsync_ExcludesDonationsWithGeneratedDocument()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var user = CreateUser(userId, "owner@example.com");
        var organization = CreateOrganization(organizationId, userId, user);
        var now = DateTime.UtcNow;
        var contact = new Contact
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Kind = ContactKinds.Donor,
            Firstname = "John",
            Lastname = "Doe",
            Email = "john@doe.com",
            CreatedAt = now.AddMonths(-2),
            PreferredFrequencySendingReceipt = "instantly"
        };
        var generatedDocument = new GeneratedDocument
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            DocumentType = DocumentType.Cerfa11580,
            SnapshotOrgName = "Org",
            SnapshotContactDisplayName = "John Doe",
            SnapshotAmount = 50m,
            SnapshotDonationDate = now.AddDays(-10),
            SnapshotDonationType = "financial",
            Status = GeneratedDocumentStatuses.Generated,
            CreatedAt = now.AddDays(-10)
        };

        dbContext.Users.Add(user);
        dbContext.Organizations.Add(organization);
        dbContext.Contacts.Add(contact);
        dbContext.GeneratedDocuments.Add(generatedDocument);
        dbContext.Donations.AddRange(
            new Donation
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                ContactId = contact.Id,
                Amount = 50m,
                Date = now.AddDays(-10),
                DonationType = "financial",
                GeneratedDocumentId = generatedDocument.Id,
                CreatedAt = now.AddDays(-10)
            },
            new Donation
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                ContactId = contact.Id,
                Amount = 80m,
                Date = now.AddDays(-5),
                DonationType = "financial",
                CreatedAt = now.AddDays(-5)
            });
        await dbContext.SaveChangesAsync();

        var service = new NotificationDashboardService(dbContext);
        var result = await service.GetDashboardAsync(organizationId, CancellationToken.None);

        Assert.Single(result.ContactsToSendTaxReceipts);
        Assert.Equal(contact.Id, result.ContactsToSendTaxReceipts[0].ContactId);
    }
}
