using Kalon.Back.Controllers;
using Kalon.Back.Data;
using Kalon.Back.DTOs;
using Kalon.Back.Models;
using Kalon.Back.Services.OrganizationAccess;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kalon.Back.Tests;

public class OrganizationDocumentsControllerTests
{
    private static OrganizationDocumentsController CreateController(ApplicationDbContext dbContext) =>
        new(dbContext, new UserOrganizationAccessService(dbContext));

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
            DefaultReceiptFrequency = ReceiptFrequency.Annually,
            CreatedAt = DateTime.UtcNow
        };
    }

    [Fact]
    public async Task GetGeneratedDocuments_ReturnsOnlyOrganizationItems()
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
        dbContext.GeneratedDocuments.AddRange(
            new GeneratedDocument
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                DocumentType = DocumentType.PaymentAttestation,
                SnapshotOrgName = "Org",
                SnapshotContactDisplayName = "John Doe",
                SnapshotAmount = 10m,
                SnapshotDonationDate = DateTime.UtcNow.Date,
                SnapshotDonationType = "financial",
                Status = GeneratedDocumentStatuses.Generated,
                CreatedAt = DateTime.UtcNow
            },
            new GeneratedDocument
            {
                Id = Guid.NewGuid(),
                OrganizationId = otherOrganizationId,
                DocumentType = DocumentType.PaymentAttestation,
                SnapshotOrgName = "Other",
                SnapshotContactDisplayName = "Other Contact",
                SnapshotAmount = 20m,
                SnapshotDonationDate = DateTime.UtcNow.Date,
                SnapshotDonationType = "financial",
                Status = GeneratedDocumentStatuses.Generated,
                CreatedAt = DateTime.UtcNow
            });
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        SetAuthenticatedUser(controller, userId);
        var result = await controller.GetGeneratedDocuments(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<List<GeneratedDocumentLightResponse>>(ok.Value);
        Assert.Single(payload);
        Assert.Equal(organizationId, payload[0].OrganizationId);
    }

    [Fact]
    public async Task GetGeneratedDocumentById_ReturnsDetails_WhenLightFalse()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, "owner@example.com");
        var organizationId = Guid.NewGuid();
        var organization = CreateOrganization(organizationId, userId, user);
        var document = new GeneratedDocument
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            DocumentType = DocumentType.PaymentAttestation,
            SnapshotOrgName = "Org",
            SnapshotContactDisplayName = "John Doe",
            SnapshotAmount = 10m,
            SnapshotDonationDate = DateTime.UtcNow.Date,
            SnapshotDonationType = "financial",
            Status = GeneratedDocumentStatuses.Generated,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.Users.Add(user);
        dbContext.Organizations.Add(organization);
        dbContext.GeneratedDocuments.Add(document);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        SetAuthenticatedUser(controller, userId);
        var result = await controller.GetGeneratedDocumentById(document.Id, false, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<GeneratedDocumentDetailsResponse>(ok.Value);
        Assert.Equal(document.Id, payload.Id);
        Assert.Equal(DocumentType.PaymentAttestation, payload.DocumentType);
    }

    [Fact]
    public async Task GetMailLogs_ReturnsOnlyOrganizationItems()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, "owner@example.com");
        var organizationId = Guid.NewGuid();
        var organization = CreateOrganization(organizationId, userId, user);

        var contactId = Guid.NewGuid();
        var contact = new Contact
        {
            Id = contactId,
            OrganizationId = organizationId,
            Kind = ContactKinds.Donor,
            Firstname = "John",
            Lastname = "Doe",
            Email = "john@doe.com",
            CreatedAt = DateTime.UtcNow
        };

        var otherUser = CreateUser(Guid.NewGuid(), "other@example.com");
        var otherOrganizationId = Guid.NewGuid();
        var otherOrganization = CreateOrganization(otherOrganizationId, otherUser.Id, otherUser);
        var otherContact = new Contact
        {
            Id = Guid.NewGuid(),
            OrganizationId = otherOrganizationId,
            Kind = ContactKinds.Donor,
            Firstname = "Other",
            Lastname = "Contact",
            Email = "other@contact.com",
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Users.Add(user);
        dbContext.Organizations.Add(organization);
        dbContext.Contacts.Add(contact);
        dbContext.Organizations.AddRange(organization, otherOrganization);
        dbContext.Contacts.AddRange(contact, otherContact);
        dbContext.MailLogs.AddRange(
            new MailLog
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                ContactId = contact.Id,
                IsEmail = true,
                SentToEmail = "john@doe.com",
                Subject = "A",
                Body = "A",
                Status = MailLogStatuses.Sent,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            },
            new MailLog
            {
                Id = Guid.NewGuid(),
                OrganizationId = otherOrganizationId,
                ContactId = otherContact.Id,
                IsEmail = true,
                SentToEmail = "other@contact.com",
                Subject = "B",
                Body = "B",
                Status = MailLogStatuses.Sent,
                CreatedAt = DateTime.UtcNow
            });
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        SetAuthenticatedUser(controller, userId);
        var result = await controller.GetMailLogs(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<List<MailLogListResponse>>(ok.Value);
        Assert.Single(payload);
        Assert.Equal(DocumentType.Message, payload[0].Type);
        Assert.True(payload[0].IsEmail);
        Assert.Equal("John Doe", payload[0].SendAt);
        Assert.Equal(GeneratedDocumentStatuses.Sent, payload[0].Status);
    }

    [Fact]
    public async Task GetMailLogById_ReturnsDetails_WhenLightFalse()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, "owner@example.com");
        var organizationId = Guid.NewGuid();
        var organization = CreateOrganization(organizationId, userId, user);
        var contact = new Contact
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Kind = ContactKinds.Donor,
            Firstname = "John",
            Lastname = "Doe",
            Email = "john@doe.com",
            CreatedAt = DateTime.UtcNow
        };
        var mailLog = new MailLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            ContactId = contact.Id,
            IsEmail = true,
            SentToEmail = "john@doe.com",
            Subject = "Subject",
            Body = "Body",
            Status = MailLogStatuses.Sent,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.Users.Add(user);
        dbContext.Organizations.Add(organization);
        dbContext.Contacts.Add(contact);
        dbContext.MailLogs.Add(mailLog);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        SetAuthenticatedUser(controller, userId);
        var result = await controller.GetMailLogById(mailLog.Id, false, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<MailLogDetailsResponse>(ok.Value);
        Assert.Equal(mailLog.Id, payload.Id);
        Assert.Equal("Subject", payload.Subject);
    }
}
