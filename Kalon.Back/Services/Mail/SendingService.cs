using Kalon.Back.Data;
using Kalon.Back.Dtos;
using Kalon.Back.Models;
using Microsoft.EntityFrameworkCore;

namespace Kalon.Back.Services.Mail;

public interface ISendingService
{
    Task<SendDocumentResultDto> SendByEmailAsync(SendDocumentDto dto, Guid organizationId);
    Task<PrintDocumentResultDto> GeneratePrintPdfAsync(SendDocumentDto dto, Guid organizationId);
    Task ConfirmMailedAsync(Guid mailLogId, Guid organizationId);
}

// Services/SendingService.cs
public class SendingService : ISendingService
{
    private readonly ApplicationDbContext _db;
    private readonly IVariableResolverService _resolver;
    private readonly IMailService _mailService;
    private readonly IDocumentGeneratorService _documentGenerator;

    public SendingService(
        ApplicationDbContext db,
        IVariableResolverService resolver,
        IMailService mailService,
        IDocumentGeneratorService documentGenerator)
    {
        _db = db;
        _resolver = resolver;
        _mailService = mailService;
        _documentGenerator = documentGenerator;
    }

    public async Task<SendDocumentResultDto> SendByEmailAsync(
        SendDocumentDto dto, Guid organizationId)
    {
        var org = await _db.Organizations.Include(o => o.Logo)
            .FirstOrDefaultAsync(o => o.Id == organizationId)
            ?? throw new InvalidOperationException("Association introuvable.");

        var contacts = await _db.Contacts
            .Where(c => dto.RecipientIds.Contains(c.Id)
                     && c.OrganizationId == organizationId)
            .ToListAsync();

        ContentBlock? signatureBlock = null;
        if (dto.SignatureBlockId.HasValue)
            signatureBlock = await _db.ContentBlocks
                .FirstOrDefaultAsync(b => b.Id == dto.SignatureBlockId
                                       && b.OrganizationId == organizationId);

        var result = new SendDocumentResultDto();

        foreach (var contact in contacts)
        {
            try
            {
                var resolvedHtml = _resolver.Resolve(dto.BodyHtml, contact, org);
                var resolvedSubject = dto.Subject != null
                    ? _resolver.Resolve(dto.Subject, contact, org)
                    : null;

                // si c'est un document fiscal → créer GeneratedDocument + PDF
                GeneratedDocument? generatedDoc = null;
                if (DocumentType.RequiresOrderNumber(dto.DocumentType)
                    || dto.DocumentType == DocumentType.MembershipCertificate
                    || dto.DocumentType == DocumentType.PaymentAttestation)
                {
                    generatedDoc = await CreateGeneratedDocumentAsync(
                        dto, contact, org, signatureBlock, resolvedHtml);
                }

                // envoi mail
                await _mailService.SendAsync(new MailMessageDto
                {
                    ToEmail = contact.Email!,
                    ToName = ContactDisplayName(contact),
                    Subject = resolvedSubject ?? "Message de votre association",
                    BodyHtml = resolvedHtml,
                    SenderEmail = org.SenderEmail ?? "noreply@kalon-app.fr",
                    SenderName = org.SenderName ?? org.Name
                });

                // log
                _db.MailLogs.Add(new MailLog
                {
                    OrganizationId = organizationId,
                    ContactId = contact.Id,
                    GeneratedDocumentId = generatedDoc?.Id,
                    IsEmail = true,
                    SentToEmail = contact.Email!,
                    Subject = resolvedSubject ?? "",
                    Body = resolvedHtml,
                    Status = MailLogStatuses.Sent,
                    CreatedAt = DateTime.UtcNow
                });

                if (generatedDoc != null)
                {
                    generatedDoc.Status = GeneratedDocumentStatuses.Sent;
                    generatedDoc.SentToEmail = contact.Email;
                    generatedDoc.SentAt = DateTime.UtcNow;
                }

                result.SuccessCount++;
            }
            catch (Exception ex)
            {
                _db.MailLogs.Add(new MailLog
                {
                    OrganizationId = organizationId,
                    ContactId = contact.Id,
                    IsEmail = true,
                    SentToEmail = contact.Email ?? "",
                    Subject = dto.Subject ?? "",
                    Body = dto.BodyHtml,
                    Status = MailLogStatuses.Error,
                    ErrorMessage = ex.Message,
                    CreatedAt = DateTime.UtcNow
                });

                result.ErrorCount++;
                result.Errors.Add(new SendDocumentErrorDto
                {
                    ContactId = contact.Id,
                    ContactName = ContactDisplayName(contact),
                    Reason = ex.Message
                });
            }
        }

        await _db.SaveChangesAsync();
        return result;
    }

    public async Task<PrintDocumentResultDto> GeneratePrintPdfAsync(
        SendDocumentDto dto, Guid organizationId)
    {
        var org = await _db.Organizations
            .Include(o => o.Logo)
            .FirstOrDefaultAsync(o => o.Id == organizationId)
            ?? throw new InvalidOperationException("Association introuvable.");

        var contacts = await _db.Contacts
            .Where(c => dto.RecipientIds.Contains(c.Id)
                     && c.OrganizationId == organizationId)
            .ToListAsync();

        ContentBlock? signatureBlock = null;
        if (dto.SignatureBlockId.HasValue)
            signatureBlock = await _db.ContentBlocks
                .FirstOrDefaultAsync(b => b.Id == dto.SignatureBlockId
                                       && b.OrganizationId == organizationId);

        var result = new PrintDocumentResultDto();
        var pagesData = new List<PrintPageData>();

        foreach (var contact in contacts)
        {
            var resolvedHtml = _resolver.Resolve(dto.BodyHtml, contact, org);

            // créer GeneratedDocument si c'est un document fiscal
            GeneratedDocument? generatedDoc = null;
            if (dto.DocumentType != "reminder")
            {
                generatedDoc = await CreateGeneratedDocumentAsync(
                    dto, contact, org, signatureBlock, resolvedHtml);
                generatedDoc.Status = GeneratedDocumentStatuses.Generated;
                result.GeneratedDocumentIds.Add(generatedDoc.Id);
            }

            // log courrier
            _db.MailLogs.Add(new MailLog
            {
                OrganizationId = organizationId,
                ContactId = contact.Id,
                GeneratedDocumentId = generatedDoc?.Id,
                IsEmail = false,
                Subject = dto.Subject ?? dto.DocumentType,
                Body = resolvedHtml,
                Status = MailLogStatuses.Printed,
                PrintedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            });

            pagesData.Add(new PrintPageData
            {
                Contact = contact,
                Organization = org,
                ResolvedHtml = resolvedHtml,
                DocumentType = dto.DocumentType,
                SignatureBlock = signatureBlock,
                GeneratedDocument = generatedDoc
            });
        }

        await _db.SaveChangesAsync();

        // générer le PDF multi-pages
        result.PdfBytes = _documentGenerator.GenerateMultiPage(pagesData);
        result.PageCount = contacts.Count;

        return result;
    }

    public async Task ConfirmMailedAsync(Guid mailLogId, Guid organizationId)
    {
        var log = await _db.MailLogs
            .Include(l => l.GeneratedDocument)
            .FirstOrDefaultAsync(l => l.Id == mailLogId
                                   && l.OrganizationId == organizationId
                                   && !l.IsEmail)
            ?? throw new InvalidOperationException("Courrier introuvable.");

        log.Status = MailLogStatuses.Mailed;
        log.MailedAt = DateTime.UtcNow;

        // mettre à jour le GeneratedDocument associé
        if (log.GeneratedDocument != null)
        {
            log.GeneratedDocument.Status = GeneratedDocumentStatuses.Sent;
            log.GeneratedDocument.SentAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
    }

    // ── helpers privés ────────────────────────────────────────────

    private async Task<GeneratedDocument> CreateGeneratedDocumentAsync(
        SendDocumentDto dto,
        Contact contact,
        Organization org,
        ContentBlock? signatureBlock,
        string resolvedHtml)
    {
        var orderNumber = DocumentType.RequiresOrderNumber(dto.DocumentType)
            ? await GenerateOrderNumberAsync(org.Id)
            : null;

        var doc = new GeneratedDocument
        {
            OrganizationId = org.Id,
            DocumentType = dto.DocumentType,
            OrderNumber = orderNumber,
            TaxReductionRate = GetTaxRate(org.FiscalStatus, dto.DocumentType),
            Status = GeneratedDocumentStatuses.Pending,

            // snapshot org
            SnapshotOrgName = org.Name,
            SnapshotOrgRna = org.RNA,
            SnapshotOrgSiret = org.SIRET,
            SnapshotOrgFiscalStatus = org.FiscalStatus,
            SnapshotOrgStreet = org.Street,
            SnapshotOrgPostalCode = org.PostalCode,
            SnapshotOrgCity = org.City,

            // snapshot contact
            SnapshotContactDisplayName = ContactDisplayName(contact),
            SnapshotContactAddress = contact.Address != null
                ? $"{contact.Address.Street}, {contact.Address.PostalCode} {contact.Address.City}"
                : null,
            SnapshotContactSiret = contact.Enterprise?.Siret,

            // snapshot donation (premier don lié si disponible)
            SnapshotAmount = 0,
            SnapshotDonationDate = DateTime.UtcNow,
            SnapshotDonationType = "financial",

            SignatureImagePath = signatureBlock?.StoredPath,
            CreatedAt = DateTime.UtcNow
        };

        _db.GeneratedDocuments.Add(doc);
        return doc;
    }

    private async Task<string> GenerateOrderNumberAsync(Guid organizationId)
    {
        var year = DateTime.UtcNow.Year;
        var count = await _db.GeneratedDocuments
            .CountAsync(d => d.OrganizationId == organizationId
                          && d.CreatedAt.Year == year
                          && d.OrderNumber != null);

        return $"{year}-{(count + 1):D3}";
    }

    private decimal? GetTaxRate(string? fiscalStatus, string documentType)
    {
        if (!DocumentType.IsTaxDeductible(documentType)) return null;
        return fiscalStatus switch
        {
            FiscalStatus.AidOrganization => 0.75m,
            _ => 0.66m
        };
    }

    private static string ContactDisplayName(Contact contact) =>
        contact.Kind == ContactKinds.Company
            && contact.Enterprise?.Name is not null
            ? contact.Enterprise.Name
            : $"{contact.Firstname} {contact.Lastname}".Trim();
}