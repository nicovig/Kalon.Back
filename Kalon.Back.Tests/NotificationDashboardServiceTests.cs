using Kalon.Back.Data;
using Kalon.Back.Models;
using Kalon.Back.Services;
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

        var periodFrom = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodTo = new DateTime(now.Year, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        dbContext.Donations.RemoveRange(dbContext.Donations);
        dbContext.Donations.Add(new Donation
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            ContactId = contactMonthlyDue.Id,
            Amount = 12m,
            Date = new DateTime(now.Year, 5, 10, 0, 0, 0, DateTimeKind.Utc),
            DonationType = "financial",
            CreatedAt = now
        });
        await dbContext.SaveChangesAsync();

        var service = new NotificationDashboardService(dbContext);
        var result = await service.GetDashboardAsync(
            organizationId, CancellationToken.None, periodFrom, periodTo);

        Assert.Single(result.ContactsToRemind);
        Assert.Equal(contactToRemind.Id, result.ContactsToRemind[0].ContactId);
        Assert.Single(result.ContactsToSendTaxReceipts);
        Assert.Equal(contactMonthlyDue.Id, result.ContactsToSendTaxReceipts[0].ContactId);
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
        var periodFrom = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodTo = new DateTime(now.Year, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        var generatedDocument = new GeneratedDocument
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            DocumentType = DocumentType.Cerfa11580,
            SnapshotOrgName = "Org",
            SnapshotContactDisplayName = "John Doe",
            SnapshotAmount = 50m,
            SnapshotDonationDate = now.AddDays(-10),
            SnapshotDonationToDate = now.AddDays(-10),
            SnapshotDonationCount = 1,
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
        var result = await service.GetDashboardAsync(
            organizationId, CancellationToken.None, periodFrom, periodTo);

        Assert.Single(result.ContactsToSendTaxReceipts);
        Assert.Equal(contact.Id, result.ContactsToSendTaxReceipts[0].ContactId);
    }

    [Fact]
    public async Task GetDashboardAsync_ExcludesContactWhenCerfaSnapshotCoversPeriodWithoutDonationLink()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var organizationId = Guid.NewGuid();
        var user = CreateUser(Guid.NewGuid(), "owner@example.com");
        var organization = CreateOrganization(organizationId, user.Id, user);
        var now = DateTime.UtcNow;
        var periodFrom = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodTo = new DateTime(now.Year, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        var contact = new Contact
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Kind = ContactKinds.Donor,
            Firstname = "Alice",
            Lastname = "Martin",
            Email = "alice@doe.com",
            CreatedAt = now.AddMonths(-2),
            PreferredFrequencySendingReceipt = "yearly"
        };
        var cerfa = new GeneratedDocument
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            DocumentType = DocumentType.Cerfa11580,
            SnapshotOrgName = "Org",
            SnapshotContactDisplayName = "Alice Martin",
            SnapshotAmount = 50m,
            SnapshotDonationDate = new DateTime(now.Year, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            SnapshotDonationToDate = new DateTime(now.Year, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            SnapshotDonationCount = 1,
            SnapshotDonationType = "financial",
            Status = GeneratedDocumentStatuses.Sent,
            CreatedAt = now
        };

        dbContext.Users.Add(user);
        dbContext.Organizations.Add(organization);
        dbContext.Contacts.Add(contact);
        dbContext.GeneratedDocuments.Add(cerfa);
        dbContext.Donations.Add(new Donation
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            ContactId = contact.Id,
            Amount = 50m,
            Date = new DateTime(now.Year, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            DonationType = "financial",
            CreatedAt = now
        });
        await dbContext.SaveChangesAsync();

        var service = new NotificationDashboardService(dbContext);
        var result = await service.GetDashboardAsync(
            organizationId, CancellationToken.None, periodFrom, periodTo);

        Assert.DoesNotContain(result.ContactsToSendTaxReceipts, x => x.ContactId == contact.Id);
    }

    [Fact]
    public async Task GetDashboardAsync_StillDueWhenOnlyNonCerfaGeneratedDocumentLinked()
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
            Firstname = "Jane",
            Lastname = "Doe",
            Email = "jane@doe.com",
            CreatedAt = now.AddMonths(-2),
            PreferredFrequencySendingReceipt = "instantly"
        };
        var attestation = new GeneratedDocument
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            DocumentType = DocumentType.PaymentAttestation,
            SnapshotOrgName = "Org",
            SnapshotContactDisplayName = "Jane Doe",
            SnapshotAmount = 50m,
            SnapshotDonationDate = now.AddDays(-10),
            SnapshotDonationType = "financial",
            Status = GeneratedDocumentStatuses.Generated,
            CreatedAt = now.AddDays(-10)
        };

        dbContext.Users.Add(user);
        dbContext.Organizations.Add(organization);
        dbContext.Contacts.Add(contact);
        dbContext.GeneratedDocuments.Add(attestation);
        dbContext.Donations.Add(new Donation
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            ContactId = contact.Id,
            Amount = 50m,
            Date = now.AddDays(-10),
            DonationType = "financial",
            GeneratedDocumentId = attestation.Id,
            CreatedAt = now.AddDays(-10)
        });
        await dbContext.SaveChangesAsync();

        var periodFrom = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodTo = new DateTime(now.Year, 12, 31, 0, 0, 0, DateTimeKind.Utc);

        var service = new NotificationDashboardService(dbContext);
        var result = await service.GetDashboardAsync(
            organizationId, CancellationToken.None, periodFrom, periodTo);

        Assert.Single(result.ContactsToSendTaxReceipts);
        Assert.Equal(contact.Id, result.ContactsToSendTaxReceipts[0].ContactId);
    }

    [Fact]
    public async Task GetDashboardAsync_ExcludesContactWhenAllDonationsLinkedToCerfa()
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
            Firstname = "Paul",
            Lastname = "Martin",
            Email = "paul@doe.com",
            CreatedAt = now.AddMonths(-2),
            PreferredFrequencySendingReceipt = "instantly"
        };
        var cerfa = new GeneratedDocument
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            DocumentType = DocumentType.Cerfa11580,
            SnapshotOrgName = "Org",
            SnapshotContactDisplayName = "Paul Martin",
            SnapshotAmount = 50m,
            SnapshotDonationDate = now.AddDays(-10),
            SnapshotDonationToDate = now.AddDays(-5),
            SnapshotDonationCount = 2,
            SnapshotDonationType = "financial",
            Status = GeneratedDocumentStatuses.Sent,
            CreatedAt = now.AddDays(-5)
        };

        dbContext.Users.Add(user);
        dbContext.Organizations.Add(organization);
        dbContext.Contacts.Add(contact);
        dbContext.GeneratedDocuments.Add(cerfa);
        dbContext.Donations.AddRange(
            new Donation
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                ContactId = contact.Id,
                Amount = 30m,
                Date = now.AddDays(-10),
                DonationType = "financial",
                GeneratedDocumentId = cerfa.Id,
                CreatedAt = now.AddDays(-10)
            },
            new Donation
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                ContactId = contact.Id,
                Amount = 20m,
                Date = now.AddDays(-5),
                DonationType = "financial",
                GeneratedDocumentId = cerfa.Id,
                CreatedAt = now.AddDays(-5)
            });
        await dbContext.SaveChangesAsync();

        var periodFrom = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodTo = new DateTime(now.Year, 12, 31, 0, 0, 0, DateTimeKind.Utc);

        var service = new NotificationDashboardService(dbContext);
        var result = await service.GetDashboardAsync(
            organizationId, CancellationToken.None, periodFrom, periodTo);

        Assert.DoesNotContain(result.ContactsToSendTaxReceipts, x => x.ContactId == contact.Id);
    }
}

