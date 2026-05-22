using Kalon.Back.Data;
using Kalon.Back.Dtos;
using Kalon.Back.Models;
using Kalon.Back.Services.Mail;
using Microsoft.EntityFrameworkCore;

namespace Kalon.Back.Tests;

public class SendingServiceTests
{
    private sealed class FakeVariableResolverService : IVariableResolverService
    {
        public string Resolve(string template, Contact contact, Organization org) => template;
        public IReadOnlyList<MailEditorVariableTag> GetAvailableTags(bool hasCompanyRecipient) => [];
    }

    private sealed class FakeMailService : IMailService
    {
        public List<MailMessageDto> SentMessages { get; } = [];
        public Task SendAsync(MailMessageDto message)
        {
            SentMessages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDocumentGeneratorService : IDocumentGeneratorService
    {
        public byte[] GenerateMultiPage(List<PrintPageData> pages) => [0x25, 0x50, 0x44, 0x46];
        public byte[] GenerateSingle(PrintPageData page) => [0x25, 0x50, 0x44, 0x46];
    }

    private static ApplicationDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    private static SendingService CreateService(
        ApplicationDbContext dbContext,
        FakeMailService? mailService = null,
        FakeDocumentGeneratorService? documentGenerator = null) =>
        new(
            dbContext,
            new FakeVariableResolverService(),
            mailService ?? new FakeMailService(),
            documentGenerator ?? new FakeDocumentGeneratorService());

    private static (DateTime From, DateTime To) CivilYearPeriod(int year) =>
        (new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(year, 12, 31, 0, 0, 0, DateTimeKind.Utc));

    private static SendDocumentDto TaxReceiptDto(
        string channel,
        IReadOnlyList<Guid> recipientIds,
        int periodYear,
        string? subject = null) =>
        new()
        {
            DocumentType = DocumentType.TaxReceipt,
            Channel = channel,
            Subject = subject,
            BodyHtml = "<p>msg</p>",
            DocumentBodyHtml = "<p>doc</p>",
            RecipientIds = [.. recipientIds],
            TaxReceiptPeriodFrom = CivilYearPeriod(periodYear).From,
            TaxReceiptPeriodTo = CivilYearPeriod(periodYear).To
        };

    private static Organization CreateOrganization(Guid organizationId) => new()
    {
        Id = organizationId,
        Name = "Asso",
        Email = "contact@asso.org",
        UserId = Guid.NewGuid(),
        User = new User
        {
            Id = Guid.NewGuid(),
            MeranId = Guid.NewGuid(),
            Firstname = "Owner",
            Lastname = "User",
            Email = "owner@asso.org",
            AssociationName = "Asso",
            PasswordHash = "hash",
            Salt = "salt"
        },
        RNA = "W442009999",
        SIRET = "12345678901234",
        FiscalStatus = FiscalStatus.GeneralInterest,
        CreatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task GeneratePrintPdfAsync_TaxReceiptRequestWithIndividuals_GeneratesCerfa11580()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var organizationId = Guid.NewGuid();
        db.Organizations.Add(CreateOrganization(organizationId));
        var contact = new Contact
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Kind = ContactKinds.Donor,
            Firstname = "Marie",
            Lastname = "Dupont",
            Email = "marie@demo.org",
            CreatedAt = DateTime.UtcNow
        };
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.GeneratePrintPdfAsync(
            TaxReceiptDto("print", [contact.Id], DateTime.UtcNow.Year),
            organizationId);

        var generatedDoc = await db.GeneratedDocuments.SingleAsync();
        Assert.Equal(DocumentType.Cerfa11580, generatedDoc.DocumentType);
    }

    [Fact]
    public async Task GeneratePrintPdfAsync_TaxReceipt_SetsSnapshotFromDonationsOnDonationDate()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var organizationId = Guid.NewGuid();
        db.Organizations.Add(CreateOrganization(organizationId));
        var contactId = Guid.NewGuid();
        var contact = new Contact
        {
            Id = contactId,
            OrganizationId = organizationId,
            Kind = ContactKinds.Donor,
            Firstname = "Marie",
            Lastname = "Dupont",
            Email = "marie@demo.org",
            CreatedAt = DateTime.UtcNow
        };
        db.Contacts.Add(contact);
        var year = DateTime.UtcNow.Year;
        var donationDate = new DateTime(year, 4, 1, 12, 0, 0, DateTimeKind.Utc);
        db.Donations.Add(new Donation
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            ContactId = contactId,
            Amount = 50m,
            Date = donationDate,
            DonationType = "financial",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.GeneratePrintPdfAsync(
            TaxReceiptDto("print", [contactId], year),
            organizationId);

        var generatedDoc = await db.GeneratedDocuments.SingleAsync();
        Assert.Equal(50m, generatedDoc.SnapshotAmount);
        Assert.Equal(donationDate, generatedDoc.SnapshotDonationDate);
        Assert.Null(generatedDoc.SnapshotDonationToDate);
        Assert.Equal(1, generatedDoc.SnapshotDonationCount);
        Assert.Equal("financial", generatedDoc.SnapshotDonationType);
    }

    [Fact]
    public async Task GeneratePrintPdfAsync_TaxReceipt_AggregatesMultipleDonationsSameCivilYear()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var organizationId = Guid.NewGuid();
        db.Organizations.Add(CreateOrganization(organizationId));
        var contactId = Guid.NewGuid();
        var contact = new Contact
        {
            Id = contactId,
            OrganizationId = organizationId,
            Kind = ContactKinds.Donor,
            Firstname = "Marie",
            Lastname = "Dupont",
            Email = "marie@demo.org",
            CreatedAt = DateTime.UtcNow
        };
        db.Contacts.Add(contact);
        var y = DateTime.UtcNow.Year;
        db.Donations.AddRange(
            new Donation
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                ContactId = contactId,
                Amount = 20m,
                Date = new DateTime(y, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                DonationType = "financial",
                CreatedAt = DateTime.UtcNow
            },
            new Donation
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                ContactId = contactId,
                Amount = 30m,
                Date = new DateTime(y, 6, 15, 0, 0, 0, DateTimeKind.Utc),
                DonationType = "financial",
                CreatedAt = DateTime.UtcNow
            });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.GeneratePrintPdfAsync(
            TaxReceiptDto("print", [contactId], y),
            organizationId);

        var generatedDoc = await db.GeneratedDocuments.SingleAsync();
        Assert.Equal(50m, generatedDoc.SnapshotAmount);
        Assert.Equal(new DateTime(y, 2, 1, 0, 0, 0, DateTimeKind.Utc), generatedDoc.SnapshotDonationDate);
        Assert.Equal(new DateTime(y, 6, 15, 0, 0, 0, DateTimeKind.Utc), generatedDoc.SnapshotDonationToDate);
        Assert.Equal(2, generatedDoc.SnapshotDonationCount);
        foreach (var d in await db.Donations.ToListAsync())
            Assert.Equal(generatedDoc.Id, d.GeneratedDocumentId);
    }

    [Fact]
    public async Task SendByEmailAsync_TaxReceipt_SetsDonationGeneratedDocumentId()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var organizationId = Guid.NewGuid();
        db.Organizations.Add(CreateOrganization(organizationId));
        var contactId = Guid.NewGuid();
        var contact = new Contact
        {
            Id = contactId,
            OrganizationId = organizationId,
            Kind = ContactKinds.Donor,
            Firstname = "Marie",
            Lastname = "Dupont",
            Email = "marie@demo.org",
            CreatedAt = DateTime.UtcNow
        };
        db.Contacts.Add(contact);
        var donationId = Guid.NewGuid();
        db.Donations.Add(new Donation
        {
            Id = donationId,
            OrganizationId = organizationId,
            ContactId = contactId,
            Amount = 40m,
            Date = new DateTime(DateTime.UtcNow.Year, 3, 10, 0, 0, 0, DateTimeKind.Utc),
            DonationType = "financial",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.SendByEmailAsync(
            TaxReceiptDto("email", [contactId], DateTime.UtcNow.Year, "Sujet"),
            organizationId);

        var generatedDoc = await db.GeneratedDocuments.SingleAsync();
        var donation = await db.Donations.FindAsync(donationId);
        Assert.NotNull(donation);
        Assert.Equal(generatedDoc.Id, donation!.GeneratedDocumentId);
    }

    [Fact]
    public async Task GeneratePrintPdfAsync_TaxReceipt_SetsDonationGeneratedDocumentId()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var organizationId = Guid.NewGuid();
        db.Organizations.Add(CreateOrganization(organizationId));
        var contactId = Guid.NewGuid();
        var contact = new Contact
        {
            Id = contactId,
            OrganizationId = organizationId,
            Kind = ContactKinds.Donor,
            Firstname = "Marie",
            Lastname = "Dupont",
            Email = "marie@demo.org",
            CreatedAt = DateTime.UtcNow
        };
        db.Contacts.Add(contact);
        var donationId = Guid.NewGuid();
        db.Donations.Add(new Donation
        {
            Id = donationId,
            OrganizationId = organizationId,
            ContactId = contactId,
            Amount = 25m,
            Date = new DateTime(DateTime.UtcNow.Year, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            DonationType = "financial",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.GeneratePrintPdfAsync(
            TaxReceiptDto("print", [contactId], DateTime.UtcNow.Year),
            organizationId);

        var generatedDoc = await db.GeneratedDocuments.SingleAsync();
        var donation = await db.Donations.FindAsync(donationId);
        Assert.NotNull(donation);
        Assert.Equal(generatedDoc.Id, donation!.GeneratedDocumentId);
    }

    [Fact]
    public async Task GeneratePrintPdfAsync_TaxReceiptRequestWithCompanies_GeneratesCerfa16216()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var organizationId = Guid.NewGuid();
        db.Organizations.Add(CreateOrganization(organizationId));
        var contact = new Contact
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Kind = ContactKinds.Company,
            Firstname = "",
            Lastname = "",
            Email = "compta@alpha.fr",
            Enterprise = new ContactEnterprise { Name = "Alpha SAS", Siret = "98765432100017" },
            CreatedAt = DateTime.UtcNow
        };
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.GeneratePrintPdfAsync(
            TaxReceiptDto("print", [contact.Id], DateTime.UtcNow.Year),
            organizationId);

        var generatedDoc = await db.GeneratedDocuments.SingleAsync();
        Assert.Equal(DocumentType.Cerfa16216, generatedDoc.DocumentType);
    }

    [Fact]
    public async Task GeneratePrintPdfAsync_TaxReceiptRequestWithMixedRecipients_GeneratesBothCerfaTypes()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var organizationId = Guid.NewGuid();
        db.Organizations.Add(CreateOrganization(organizationId));

        var individual = new Contact
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Kind = ContactKinds.Donor,
            Firstname = "Marie",
            Lastname = "Dupont",
            Email = "marie@demo.org",
            CreatedAt = DateTime.UtcNow
        };
        var company = new Contact
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Kind = ContactKinds.Company,
            Firstname = "",
            Lastname = "",
            Email = "compta@alpha.fr",
            Enterprise = new ContactEnterprise { Name = "Alpha SAS" },
            CreatedAt = DateTime.UtcNow
        };
        db.Contacts.AddRange(individual, company);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.GeneratePrintPdfAsync(
            TaxReceiptDto("print", [individual.Id, company.Id], DateTime.UtcNow.Year),
            organizationId);

        var generatedTypes = await db.GeneratedDocuments
            .Select(d => d.DocumentType)
            .ToListAsync();
        Assert.Contains(DocumentType.Cerfa11580, generatedTypes);
        Assert.Contains(DocumentType.Cerfa16216, generatedTypes);
        Assert.Equal(4, result.PageCount);
    }

    [Fact]
    public async Task SendByEmailAsync_WithGeneratedDocument_SendsAttachment()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var organizationId = Guid.NewGuid();
        db.Organizations.Add(CreateOrganization(organizationId));
        var contact = new Contact
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Kind = ContactKinds.Donor,
            Firstname = "Marie",
            Lastname = "Dupont",
            Email = "marie@demo.org",
            CreatedAt = DateTime.UtcNow
        };
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();

        var fakeMailService = new FakeMailService();
        var service = CreateService(db, fakeMailService);
        await service.SendByEmailAsync(
            TaxReceiptDto("email", [contact.Id], DateTime.UtcNow.Year, "Sujet"),
            organizationId);

        var sent = Assert.Single(fakeMailService.SentMessages);
        var attachment = Assert.Single(sent.Attachments);
        Assert.NotEmpty(attachment.Content);
        Assert.False(string.IsNullOrWhiteSpace(attachment.FileName));
        Assert.Equal("application/pdf", attachment.ContentType);
    }

    [Fact]
    public async Task SendByEmailAsync_WithUserAttachments_IncludesThemInEmail()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var organizationId = Guid.NewGuid();
        db.Organizations.Add(CreateOrganization(organizationId));
        var contact = new Contact
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Kind = ContactKinds.Donor,
            Firstname = "Marie",
            Lastname = "Dupont",
            Email = "marie@demo.org",
            CreatedAt = DateTime.UtcNow
        };
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();

        var fakeMailService = new FakeMailService();
        var service = CreateService(db, fakeMailService);
        var userAttachments = new List<EmailAttachmentDto>
        {
            new() { FileName = "brochure.pdf", Content = [0x25, 0x50], ContentType = "application/pdf" },
            new() { FileName = "photo.jpg", Content = [0xFF, 0xD8], ContentType = "image/jpeg" }
        };

        await service.SendByEmailAsync(
            new SendDocumentDto
            {
                DocumentType = DocumentType.Message,
                Channel = "email",
                BodyHtml = "<p>Relance</p>",
                RecipientIds = [contact.Id]
            },
            organizationId,
            userAttachments);

        var sent = Assert.Single(fakeMailService.SentMessages);
        Assert.Equal(2, sent.Attachments.Count);
        Assert.Equal("brochure.pdf", sent.Attachments[0].FileName);
        Assert.Equal("photo.jpg", sent.Attachments[1].FileName);
    }

    [Fact]
    public async Task SendByEmailAsync_WithGeneratedDocumentAndUserAttachments_IncludesAll()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var organizationId = Guid.NewGuid();
        db.Organizations.Add(CreateOrganization(organizationId));
        var contact = new Contact
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Kind = ContactKinds.Donor,
            Firstname = "Marie",
            Lastname = "Dupont",
            Email = "marie@demo.org",
            CreatedAt = DateTime.UtcNow
        };
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();

        var fakeMailService = new FakeMailService();
        var service = CreateService(db, fakeMailService);
        var userAttachments = new List<EmailAttachmentDto>
        {
            new() { FileName = "extra.pdf", Content = [0x01], ContentType = "application/pdf" }
        };

        await service.SendByEmailAsync(
            TaxReceiptDto("email", [contact.Id], DateTime.UtcNow.Year, "Sujet"),
            organizationId,
            userAttachments);

        var sent = Assert.Single(fakeMailService.SentMessages);
        Assert.Equal(2, sent.Attachments.Count);
        Assert.Equal("application/pdf", sent.Attachments[0].ContentType);
        Assert.Equal("extra.pdf", sent.Attachments[1].FileName);
    }

    [Fact]
    public async Task GeneratePrintPdfAsync_WithGeneratedDocument_ReturnsTwoPagesPerRecipient()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var organizationId = Guid.NewGuid();
        db.Organizations.Add(CreateOrganization(organizationId));
        var contact = new Contact
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Kind = ContactKinds.Donor,
            Firstname = "Marie",
            Lastname = "Dupont",
            Email = "marie@demo.org",
            CreatedAt = DateTime.UtcNow
        };
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.GeneratePrintPdfAsync(
            TaxReceiptDto("print", [contact.Id], DateTime.UtcNow.Year),
            organizationId);

        Assert.Equal(2, result.PageCount);
    }

    [Fact]
    public async Task SendByEmailAsync_TaxReceiptWithMixedRecipients_GeneratesBothCerfaTypesAndAttachments()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var organizationId = Guid.NewGuid();
        db.Organizations.Add(CreateOrganization(organizationId));

        var individual = new Contact
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Kind = ContactKinds.Donor,
            Firstname = "Marie",
            Lastname = "Dupont",
            Email = "marie@demo.org",
            CreatedAt = DateTime.UtcNow
        };
        var company = new Contact
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Kind = ContactKinds.Company,
            Firstname = "",
            Lastname = "",
            Email = "compta@alpha.fr",
            Enterprise = new ContactEnterprise { Name = "Alpha SAS" },
            CreatedAt = DateTime.UtcNow
        };
        db.Contacts.AddRange(individual, company);
        await db.SaveChangesAsync();

        var fakeMailService = new FakeMailService();
        var service = CreateService(db, fakeMailService);

        await service.SendByEmailAsync(
            TaxReceiptDto("email", [individual.Id, company.Id], DateTime.UtcNow.Year, "Sujet"),
            organizationId);

        Assert.Equal(2, fakeMailService.SentMessages.Count);
        Assert.All(fakeMailService.SentMessages, m => Assert.NotEmpty(m.Attachments));

        var generatedTypes = await db.GeneratedDocuments
            .Select(d => d.DocumentType)
            .ToListAsync();
        Assert.Contains(DocumentType.Cerfa11580, generatedTypes);
        Assert.Contains(DocumentType.Cerfa16216, generatedTypes);
    }
}
