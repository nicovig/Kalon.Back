using Kalon.Back.Data;
using Kalon.Back.Dtos;
using Kalon.Back.Models;
using Kalon.Back.Services;
using Microsoft.EntityFrameworkCore;

namespace Kalon.Back.Services.Mail;

public interface ISendingService
{
    Task<SendDocumentResultDto> SendByEmailAsync(
        SendDocumentDto dto,
        Guid organizationId,
        IReadOnlyList<EmailAttachmentDto>? userAttachments = null);
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
        SendDocumentDto dto,
        Guid organizationId,
        IReadOnlyList<EmailAttachmentDto>? userAttachments = null)
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
                var resolvedSignatureBlock = ResolveSignatureBlockForContact(signatureBlock, contact, org);
                var effectiveDocumentType = ResolveEffectiveDocumentTypeForContact(dto.DocumentType, contact);
                var resolvedCompanionHtml = _resolver.Resolve(dto.BodyHtml, contact, org);
                var documentTemplate = dto.DocumentBodyHtml ?? dto.BodyHtml;
                var resolvedDocumentHtml = _resolver.Resolve(documentTemplate, contact, org);
                var resolvedSubject = dto.Subject != null
                    ? _resolver.Resolve(dto.Subject, contact, org)
                    : null;

                // si c'est un document fiscal → créer GeneratedDocument + PDF
                GeneratedDocument? generatedDoc = null;
                if (RequiresGeneratedDocument(effectiveDocumentType))
                {
                    generatedDoc = await CreateGeneratedDocumentAsync(
                        effectiveDocumentType, contact, org, resolvedSignatureBlock, dto);
                }

                var attachments = new List<EmailAttachmentDto>();
                if (generatedDoc != null)
                {
                    var attachmentPage = new PrintPageData
                    {
                        Contact = contact,
                        Organization = org,
                        ResolvedHtml = resolvedDocumentHtml,
                        DocumentType = effectiveDocumentType,
                        SignatureBlock = resolvedSignatureBlock,
                        GeneratedDocument = generatedDoc
                    };
                    attachments.Add(new EmailAttachmentDto
                    {
                        FileName = BuildDocumentFileName(effectiveDocumentType, generatedDoc.OrderNumber),
                        Content = _documentGenerator.GenerateSingle(attachmentPage),
                        ContentType = "application/pdf"
                    });
                }

                if (userAttachments is { Count: > 0 })
                    attachments.AddRange(userAttachments);

                await _mailService.SendAsync(new MailMessageDto
                {
                    ToEmail = contact.Email!,
                    ToName = ContactDisplayName(contact),
                    Subject = resolvedSubject ?? "Message de votre association",
                    BodyHtml = resolvedCompanionHtml,
                    SenderEmail = org.SenderEmail ?? "noreply@kalon-app.fr",
                    SenderName = org.SenderName ?? org.Name,
                    Attachments = attachments
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
                    Body = resolvedCompanionHtml,
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
            var resolvedSignatureBlock = ResolveSignatureBlockForContact(signatureBlock, contact, org);
            var effectiveDocumentType = ResolveEffectiveDocumentTypeForContact(dto.DocumentType, contact);
            var resolvedCompanionHtml = _resolver.Resolve(dto.BodyHtml, contact, org);
            var resolvedSubject = dto.Subject != null
                ? _resolver.Resolve(dto.Subject, contact, org)
                : null;
            var documentTemplate = dto.DocumentBodyHtml ?? dto.BodyHtml;
            var resolvedDocumentHtml = _resolver.Resolve(documentTemplate, contact, org);

            // créer GeneratedDocument si c'est un document fiscal
            GeneratedDocument? generatedDoc = null;
            if (RequiresGeneratedDocument(effectiveDocumentType))
            {
                generatedDoc = await CreateGeneratedDocumentAsync(
                    effectiveDocumentType, contact, org, resolvedSignatureBlock, dto);
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
                Subject = resolvedSubject ?? dto.DocumentType,
                Body = resolvedCompanionHtml,
                Status = MailLogStatuses.Printed,
                PrintedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            });

            pagesData.Add(new PrintPageData
            {
                Contact = contact,
                Organization = org,
                ResolvedHtml = resolvedCompanionHtml,
                ResolvedSubject = resolvedSubject,
                DocumentType = DocumentType.Message,
                SignatureBlock = resolvedSignatureBlock,
                GeneratedDocument = generatedDoc
            });

            if (generatedDoc is null)
                continue;

            pagesData.Add(new PrintPageData
            {
                Contact = contact,
                Organization = org,
                ResolvedHtml = resolvedDocumentHtml,
                ResolvedSubject = resolvedSubject,
                DocumentType = effectiveDocumentType,
                SignatureBlock = resolvedSignatureBlock,
                GeneratedDocument = generatedDoc
            });
        }

        await _db.SaveChangesAsync();

        // générer le PDF multi-pages
        result.PdfBytes = _documentGenerator.GenerateMultiPage(pagesData);
        result.PageCount = pagesData.Count;

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

    private ContentBlock? ResolveSignatureBlockForContact(
        ContentBlock? signatureBlock,
        Contact contact,
        Organization org)
    {
        if (signatureBlock is null)
            return null;

        if (string.IsNullOrWhiteSpace(signatureBlock.Content))
            return signatureBlock;

        var resolvedContent = _resolver.Resolve(signatureBlock.Content, contact, org);

        return new ContentBlock
        {
            Id = signatureBlock.Id,
            OrganizationId = signatureBlock.OrganizationId,
            Organization = org,
            Name = signatureBlock.Name,
            Kind = signatureBlock.Kind,
            Content = resolvedContent,
            StoredPath = signatureBlock.StoredPath,
            MimeType = signatureBlock.MimeType,
            UsableInEmail = signatureBlock.UsableInEmail,
            UsableInReceipt = signatureBlock.UsableInReceipt,
            CreatedAt = signatureBlock.CreatedAt,
            UpdatedAt = signatureBlock.UpdatedAt
        };
    }

    private async Task<GeneratedDocument> CreateGeneratedDocumentAsync(
        string documentType,
        Contact contact,
        Organization org,
        ContentBlock? signatureBlock,
        SendDocumentDto dto)
    {
        var orderNumber = DocumentType.RequiresOrderNumber(documentType)
            ? await GenerateOrderNumberAsync(org.Id)
            : null;

        var doc = new GeneratedDocument
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            DocumentType = documentType,
            OrderNumber = orderNumber,
            TaxReductionRate = GetTaxRate(org.FiscalStatus, documentType),
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

            SnapshotAmount = 0,
            SnapshotDonationDate = DateTime.UtcNow,
            SnapshotDonationToDate = null,
            SnapshotDonationCount = 0,
            SnapshotDonationType = "financial",

            SignatureImagePath = signatureBlock?.StoredPath,
            CreatedAt = DateTime.UtcNow
        };

        var donationsToLink = await ApplyDonationSnapshotForGeneratedDocumentAsync(
            doc, contact.Id, org.Id, dto);

        _db.GeneratedDocuments.Add(doc);
        foreach (var donation in donationsToLink)
            donation.GeneratedDocumentId = doc.Id;

        return doc;
    }

    private async Task<IReadOnlyList<Donation>> ApplyDonationSnapshotForGeneratedDocumentAsync(
        GeneratedDocument doc,
        Guid contactId,
        Guid organizationId,
        SendDocumentDto dto)
    {
        if (!DocumentType.IsTaxDeductible(doc.DocumentType))
        {
            doc.SnapshotAmount = 0;
            doc.SnapshotDonationDate = DateTime.UtcNow;
            doc.SnapshotDonationToDate = null;
            doc.SnapshotDonationCount = 0;
            doc.SnapshotDonationType = TaxReceiptPeriodHelper.FinancialDonationType;
            return [];
        }

        var period = TaxReceiptPeriodHelper.Create(
            dto.TaxReceiptPeriodFrom!.Value,
            dto.TaxReceiptPeriodTo!.Value);

        var query = _db.Donations
            .Where(d => d.ContactId == contactId
                        && d.OrganizationId == organizationId
                        && d.DonationType == TaxReceiptPeriodHelper.FinancialDonationType
                        && d.Date >= period.From
                        && d.Date <= period.To);

        if (dto.DonationIds is { Count: > 0 })
            query = query.Where(d => dto.DonationIds.Contains(d.Id));

        var slice = await query.OrderBy(d => d.Date).ToListAsync();

        if (slice.Count == 0)
        {
            doc.SnapshotAmount = 0;
            doc.SnapshotDonationDate = period.From;
            doc.SnapshotDonationToDate = null;
            doc.SnapshotDonationCount = 0;
            doc.SnapshotDonationType = TaxReceiptPeriodHelper.FinancialDonationType;
            return [];
        }

        doc.SnapshotAmount = slice.Sum(d => d.Amount);
        doc.SnapshotDonationDate = slice.Min(d => d.Date);
        doc.SnapshotDonationToDate = slice.Count > 1 ? slice.Max(d => d.Date) : null;
        doc.SnapshotDonationCount = slice.Count;
        doc.SnapshotDonationType = TaxReceiptPeriodHelper.FinancialDonationType;

        return slice;
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

    private static bool RequiresGeneratedDocument(string documentType) =>
        DocumentType.RequiresOrderNumber(documentType)
        || documentType == DocumentType.MembershipCertificate
        || documentType == DocumentType.PaymentAttestation;

    private static string ResolveEffectiveDocumentTypeForContact(string requestedDocumentType, Contact contact)
    {
        if (requestedDocumentType != DocumentType.TaxReceipt)
            return requestedDocumentType;
        return contact.Kind == ContactKinds.Company
            ? DocumentType.Cerfa16216
            : DocumentType.Cerfa11580;
    }

    private static string ContactDisplayName(Contact contact) =>
        contact.Kind == ContactKinds.Company
            && contact.Enterprise?.Name is not null
            ? contact.Enterprise.Name
            : $"{contact.Firstname} {contact.Lastname}".Trim();

    private static string BuildDocumentFileName(string documentType, string? orderNumber)
    {
        var normalizedType = documentType.Replace("_", "-");
        var suffix = !string.IsNullOrWhiteSpace(orderNumber) ? orderNumber : DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        return $"{normalizedType}-{suffix}.pdf";
    }
}