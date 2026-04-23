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
    public async Task GeneratePrintPdfAsync_CerfaRequestWithIndividuals_GeneratesCerfa11580()
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
        await service.GeneratePrintPdfAsync(new SendDocumentDto
        {
            DocumentType = DocumentType.Cerfa11580,
            Channel = "print",
            BodyHtml = "<p>msg</p>",
            DocumentBodyHtml = "<p>doc</p>",
            RecipientIds = [contact.Id]
        }, organizationId);

        var generatedDoc = await db.GeneratedDocuments.SingleAsync();
        Assert.Equal(DocumentType.Cerfa11580, generatedDoc.DocumentType);
    }

    [Fact]
    public async Task GeneratePrintPdfAsync_CerfaRequestWithCompanies_GeneratesCerfa16216()
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
        await service.GeneratePrintPdfAsync(new SendDocumentDto
        {
            DocumentType = DocumentType.Cerfa11580,
            Channel = "print",
            BodyHtml = "<p>msg</p>",
            DocumentBodyHtml = "<p>doc</p>",
            RecipientIds = [contact.Id]
        }, organizationId);

        var generatedDoc = await db.GeneratedDocuments.SingleAsync();
        Assert.Equal(DocumentType.Cerfa16216, generatedDoc.DocumentType);
    }

    [Fact]
    public async Task GeneratePrintPdfAsync_CerfaRequestWithMixedRecipients_Throws()
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
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.GeneratePrintPdfAsync(new SendDocumentDto
        {
            DocumentType = DocumentType.Cerfa11580,
            Channel = "print",
            BodyHtml = "<p>msg</p>",
            DocumentBodyHtml = "<p>doc</p>",
            RecipientIds = [individual.Id, company.Id]
        }, organizationId));

        Assert.Contains("uniquement des entreprises", ex.Message);
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
        await service.SendByEmailAsync(new SendDocumentDto
        {
            DocumentType = DocumentType.Cerfa11580,
            Channel = "email",
            Subject = "Sujet",
            BodyHtml = "<p>accompagnement</p>",
            DocumentBodyHtml = "<p>document</p>",
            RecipientIds = [contact.Id]
        }, organizationId);

        var sent = Assert.Single(fakeMailService.SentMessages);
        Assert.NotNull(sent.AttachmentBytes);
        Assert.NotEmpty(sent.AttachmentBytes!);
        Assert.False(string.IsNullOrWhiteSpace(sent.AttachmentFileName));
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
        var result = await service.GeneratePrintPdfAsync(new SendDocumentDto
        {
            DocumentType = DocumentType.Cerfa11580,
            Channel = "print",
            BodyHtml = "<p>accompagnement</p>",
            DocumentBodyHtml = "<p>document</p>",
            RecipientIds = [contact.Id]
        }, organizationId);

        Assert.Equal(2, result.PageCount);
    }
}
