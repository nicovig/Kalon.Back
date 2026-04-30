using Kalon.Back.Data;
using Kalon.Back.DTOs;
using Kalon.Back.Models;
using Kalon.Back.Services.Mail;
using Kalon.Back.Services.OrganizationAccess;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kalon.Back.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "organization_master")]
public class OrganizationDocumentsController(
    ApplicationDbContext dbContext,
    IUserOrganizationAccessService userOrganizationAccess,
    IDocumentGeneratorService documentGeneratorService) : ControllerBase
{
    [HttpGet("generated-documents")]
    [ProducesResponseType(typeof(List<GeneratedDocumentLightResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGeneratedDocuments(CancellationToken cancellationToken)
    {
        var userId = ResolveUserIdFromJwt();
        if (userId is null)
            return BadRequest(new ApiMessageResponse { Message = "userId is required." });

        var access = await userOrganizationAccess.ResolveAsync(userId.Value, cancellationToken);
        var resolved = access.ToActionResult();
        if (!resolved.Success)
            return resolved.Error!;

        var organizationId = resolved.OrganizationId;
        var items = await dbContext.GeneratedDocuments
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new GeneratedDocumentLightResponse
            {
                Id = x.Id,
                OrganizationId = x.OrganizationId,
                DocumentType = x.DocumentType,
                OrderNumber = x.OrderNumber,
                Status = x.Status,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("generated-documents/{id:guid}")]
    [ProducesResponseType(typeof(GeneratedDocumentLightResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneratedDocumentDetailsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGeneratedDocumentById([FromRoute] Guid id,
        [FromQuery] bool light = true, CancellationToken cancellationToken = default)
    {
        var userId = ResolveUserIdFromJwt();
        if (userId is null)
            return BadRequest(new ApiMessageResponse { Message = "userId is required." });

        var access = await userOrganizationAccess.ResolveAsync(userId.Value, cancellationToken);
        var resolved = access.ToActionResult();
        if (!resolved.Success)
            return resolved.Error!;

        var organizationId = resolved.OrganizationId;
        if (light)
        {
            var lightResult = await dbContext.GeneratedDocuments
                .AsNoTracking()
                .Where(x => x.OrganizationId == organizationId && x.Id == id)
                .Select(x => new GeneratedDocumentLightResponse
                {
                    Id = x.Id,
                    OrganizationId = x.OrganizationId,
                    DocumentType = x.DocumentType,
                    OrderNumber = x.OrderNumber,
                    Status = x.Status,
                    CreatedAt = x.CreatedAt
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (lightResult is null)
                return NotFound(new ApiMessageResponse { Message = "Generated document not found." });

            return Ok(lightResult);
        }

        var detailedResult = await dbContext.GeneratedDocuments
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.Id == id)
            .Select(x => new GeneratedDocumentDetailsResponse
            {
                Id = x.Id,
                OrganizationId = x.OrganizationId,
                DocumentType = x.DocumentType,
                OrderNumber = x.OrderNumber,
                TaxReductionRate = x.TaxReductionRate,
                SnapshotOrgName = x.SnapshotOrgName,
                SnapshotContactDisplayName = x.SnapshotContactDisplayName,
                SnapshotAmount = x.SnapshotAmount,
                SnapshotDonationDate = x.SnapshotDonationDate,
                SnapshotDonationType = x.SnapshotDonationType,
                PdfPath = x.PdfPath,
                Status = x.Status,
                GeneratedAt = x.GeneratedAt,
                SentToEmail = x.SentToEmail,
                SentAt = x.SentAt,
                SendError = x.SendError,
                CreatedAt = x.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (detailedResult is null)
            return NotFound(new ApiMessageResponse { Message = "Generated document not found." });

        return Ok(detailedResult);
    }

    [HttpPost("generated-documents/{id:guid}/regenerate")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RegenerateGeneratedDocument([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var userId = ResolveUserIdFromJwt();
        if (userId is null)
            return BadRequest(new ApiMessageResponse { Message = "userId is required." });

        var access = await userOrganizationAccess.ResolveAsync(userId.Value, cancellationToken);
        var resolved = access.ToActionResult();
        if (!resolved.Success)
            return resolved.Error!;

        var organizationId = resolved.OrganizationId;
        var sourceDocument = await dbContext.GeneratedDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == id, cancellationToken);

        if (sourceDocument is null)
            return NotFound(new ApiMessageResponse { Message = "Generated document not found." });

        var regeneratedDocument = new GeneratedDocument
        {
            Id = Guid.NewGuid(),
            OrganizationId = sourceDocument.OrganizationId,
            DocumentType = sourceDocument.DocumentType,
            OrderNumber = sourceDocument.OrderNumber,
            TaxReductionRate = sourceDocument.TaxReductionRate,
            SnapshotOrgName = sourceDocument.SnapshotOrgName,
            SnapshotOrgRna = sourceDocument.SnapshotOrgRna,
            SnapshotOrgSiret = sourceDocument.SnapshotOrgSiret,
            SnapshotOrgFiscalStatus = sourceDocument.SnapshotOrgFiscalStatus,
            SnapshotOrgStreet = sourceDocument.SnapshotOrgStreet,
            SnapshotOrgPostalCode = sourceDocument.SnapshotOrgPostalCode,
            SnapshotOrgCity = sourceDocument.SnapshotOrgCity,
            SnapshotContactDisplayName = sourceDocument.SnapshotContactDisplayName,
            SnapshotContactAddress = sourceDocument.SnapshotContactAddress,
            SnapshotContactSiret = sourceDocument.SnapshotContactSiret,
            SnapshotAmount = sourceDocument.SnapshotAmount,
            SnapshotDonationDate = sourceDocument.SnapshotDonationDate,
            SnapshotDonationType = sourceDocument.SnapshotDonationType,
            SignatureImagePath = sourceDocument.SignatureImagePath,
            Status = GeneratedDocumentStatuses.Generated,
            GeneratedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.GeneratedDocuments.Add(regeneratedDocument);
        await dbContext.SaveChangesAsync(cancellationToken);

        var pdfBytes = documentGeneratorService.GenerateSingle(new PrintPageData
        {
            Contact = new Contact
            {
                Kind = ContactKinds.Donor,
                Firstname = sourceDocument.SnapshotContactDisplayName,
                Lastname = string.Empty
            },
            Organization = new Organization
            {
                Name = sourceDocument.SnapshotOrgName,
                Email = string.Empty,
                RNA = sourceDocument.SnapshotOrgRna ?? string.Empty,
                SIRET = sourceDocument.SnapshotOrgSiret ?? string.Empty,
                FiscalStatus = sourceDocument.SnapshotOrgFiscalStatus ?? FiscalStatus.GeneralInterest
            },
            ResolvedHtml = string.Empty,
            DocumentType = regeneratedDocument.DocumentType,
            GeneratedDocument = regeneratedDocument
        });

        var fileName = $"{regeneratedDocument.DocumentType}-{regeneratedDocument.OrderNumber ?? regeneratedDocument.Id.ToString("N")}.pdf";
        return File(pdfBytes, "application/pdf", fileName);
    }

    [HttpGet("mail-logs")]
    [ProducesResponseType(typeof(List<MailLogListResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMailLogs(CancellationToken cancellationToken)
    {
        var userId = ResolveUserIdFromJwt();
        if (userId is null)
            return BadRequest(new ApiMessageResponse { Message = "userId is required." });

        var access = await userOrganizationAccess.ResolveAsync(userId.Value, cancellationToken);
        var resolved = access.ToActionResult();
        if (!resolved.Success)
            return resolved.Error!;

        var organizationId = resolved.OrganizationId;
        var items = await dbContext.MailLogs
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new MailLogListResponse
            {
                Id = x.Id,
                Type = x.GeneratedDocument != null ? x.GeneratedDocument.DocumentType : DocumentType.Message,
                Date = x.CreatedAt,
                IsEmail = x.IsEmail,
                SendAt = $"{x.Contact.Firstname} {x.Contact.Lastname}".Trim(),
                Status = x.Status == MailLogStatuses.Error
                    || (x.GeneratedDocument != null && x.GeneratedDocument.Status == GeneratedDocumentStatuses.Error)
                        ? MailLogStatuses.Error
                        : x.Status == MailLogStatuses.Printed
                            || (x.GeneratedDocument != null && x.GeneratedDocument.Status == GeneratedDocumentStatuses.Generated)
                            ? MailLogStatuses.Printed
                            : MailLogStatuses.Sent
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("mail-logs/{id:guid}")]
    [ProducesResponseType(typeof(MailLogLightResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MailLogDetailsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMailLogById([FromRoute] Guid id,
        [FromQuery] bool light = true, CancellationToken cancellationToken = default)
    {
        var userId = ResolveUserIdFromJwt();
        if (userId is null)
            return BadRequest(new ApiMessageResponse { Message = "userId is required." });

        var access = await userOrganizationAccess.ResolveAsync(userId.Value, cancellationToken);
        var resolved = access.ToActionResult();
        if (!resolved.Success)
            return resolved.Error!;

        var organizationId = resolved.OrganizationId;
        if (light)
        {
            var lightResult = await dbContext.MailLogs
                .AsNoTracking()
                .Where(x => x.OrganizationId == organizationId && x.Id == id)
                .Select(x => new MailLogLightResponse
                {
                    Id = x.Id,
                    OrganizationId = x.OrganizationId,
                    ContactId = x.ContactId,
                    IsEmail = x.IsEmail,
                    Status = x.Status,
                    CreatedAt = x.CreatedAt
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (lightResult is null)
                return NotFound(new ApiMessageResponse { Message = "Mail log not found." });

            return Ok(lightResult);
        }

        var detailedResult = await dbContext.MailLogs
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.Id == id)
            .Select(x => new MailLogDetailsResponse
            {
                Id = x.Id,
                OrganizationId = x.OrganizationId,
                ContactId = x.ContactId,
                    ContactDisplayName = $"{x.Contact.Firstname} {x.Contact.Lastname}".Trim(),
                    ContactEmail = x.Contact.Email,
                GeneratedDocumentId = x.GeneratedDocumentId,
                    GeneratedDocumentType = x.GeneratedDocument != null ? x.GeneratedDocument.DocumentType : null,
                    GeneratedDocumentOrderNumber = x.GeneratedDocument != null ? x.GeneratedDocument.OrderNumber : null,
                IsEmail = x.IsEmail,
                SentToEmail = x.SentToEmail,
                Subject = x.Subject,
                Body = x.Body,
                Status = x.Status,
                ErrorMessage = x.ErrorMessage,
                PrintedAt = x.PrintedAt,
                MailedAt = x.MailedAt,
                MailedBy = x.MailedBy,
                CreatedAt = x.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (detailedResult is null)
            return NotFound(new ApiMessageResponse { Message = "Mail log not found." });

        return Ok(detailedResult);
    }

    private Guid? ResolveUserIdFromJwt()
    {
        var principal = HttpContext?.User;
        if (principal is null)
            return null;

        var claimValue = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? principal.FindFirstValue("sub");
        return Guid.TryParse(claimValue, out var parsed) ? parsed : null;
    }
}

