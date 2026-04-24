using System.Security.Claims;
using Kalon.Back.Controllers;
using Kalon.Back.Data;
using Kalon.Back.DTOs;
using Kalon.Back.Models;
using Kalon.Back.Services.Notification;
using Kalon.Back.Services.OrganizationAccess;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kalon.Back.Tests;

public class NotificationControllerTests
{
    private static NotificationController CreateController(ApplicationDbContext dbContext) =>
        new(
            new UserOrganizationAccessService(dbContext),
            new NotificationDashboardService(dbContext));

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
    public async Task GetDashboard_ReturnsExpectedDashboardListsAndCount()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, "owner@example.com");
        var organizationId = Guid.NewGuid();
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
        var contactInstantly = new Contact
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Kind = ContactKinds.Donor,
            Firstname = "Instant",
            Lastname = "Due",
            Email = "i@x.com",
            CreatedAt = now.AddMonths(-2),
            PreferredFrequencySendingReceipt = "instantly"
        };
        var contactIgnored = new Contact
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Kind = ContactKinds.Donor,
            Firstname = "Ignored",
            Lastname = "Out",
            Email = "o@x.com",
            CreatedAt = now.AddMonths(-20),
            IsOut = true
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
        dbContext.Contacts.AddRange(contactToRemind, contactMonthlyDue, contactInstantly, contactIgnored);
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
            },
            new Donation
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                ContactId = contactInstantly.Id,
                Amount = 15m,
                Date = now.AddDays(-2),
                DonationType = "financial",
                CreatedAt = now.AddDays(-2)
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

        var controller = CreateController(dbContext);
        SetAuthenticatedUser(controller, userId);

        var result = await controller.GetDashboard(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<NotificationDashboardResponse>(ok.Value);
        Assert.Single(payload.ContactsToRemind);
        Assert.Equal(contactToRemind.Id, payload.ContactsToRemind[0].ContactId);
        Assert.Equal("Remind Contact", payload.ContactsToRemind[0].DisplayName);
        Assert.Equal(3, payload.ContactsToSendTaxReceipts.Count);
        Assert.Contains(payload.ContactsToSendTaxReceipts, x => x.ContactId == contactToRemind.Id);
        Assert.Contains(payload.ContactsToSendTaxReceipts, x => x.ContactId == contactMonthlyDue.Id);
        Assert.Contains(payload.ContactsToSendTaxReceipts, x => x.ContactId == contactInstantly.Id);
        Assert.Equal(1, payload.PhysicalLettersToSendCount);
    }

    [Fact]
    public async Task GetDashboard_ExcludesAlreadyGeneratedReceipts()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, "owner@example.com");
        var organizationId = Guid.NewGuid();
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

        var controller = CreateController(dbContext);
        SetAuthenticatedUser(controller, userId);

        var result = await controller.GetDashboard(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<NotificationDashboardResponse>(ok.Value);
        Assert.Single(payload.ContactsToSendTaxReceipts);
        Assert.Equal(contact.Id, payload.ContactsToSendTaxReceipts[0].ContactId);
    }

    [Fact]
    public async Task GetDashboard_ReturnsBadRequest_WhenUserClaimMissing()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var controller = CreateController(dbContext);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var result = await controller.GetDashboard(CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetDashboard_ReturnsNotFound_WhenOrganizationMissing()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        dbContext.Users.Add(CreateUser(userId, "owner@example.com"));
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        SetAuthenticatedUser(controller, userId);

        var result = await controller.GetDashboard(CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }
}
