using System.Text.Json;
using Kalon.Back.Dtos;
using Kalon.Back.Dtos.Errors;
using Kalon.Back.DTOs;
using Kalon.Back.Models;
using Kalon.Back.Services;
using Kalon.Back.Services.Mail;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Kalon.Back.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "organization_master")]
public class SendingController : ControllerBase
{
    private readonly ISendingService _sendingService;
    private readonly IVariableResolverService _variableResolverService;
    private readonly IQuotaService _quotaService;
    private readonly IPlanService _planService;

    public SendingController(ISendingService sendingService, 
        IVariableResolverService variableResolverService,
        IQuotaService quotaService,
        IPlanService planService)
    {
        _sendingService = sendingService;
        _variableResolverService = variableResolverService;
        _quotaService = quotaService;
        _planService = planService;
    }

    [HttpGet("mail-editor-tags")]
    [ProducesResponseType(typeof(List<MailEditorVariableTag>), StatusCodes.Status200OK)]
    public ActionResult<List<MailEditorVariableTag>> GetMailEditorTags([FromQuery] bool hasCompanyRecipient = false)
    {
        var tags = _variableResolverService.GetAvailableTags(hasCompanyRecipient).ToList();
        return Ok(tags);
    }

    [HttpPost("send")]
    [Consumes("application/json", "multipart/form-data")]
    [SwaggerOperation(
        Summary = "Envoi email",
        Description = "Envoie un email (JSON ou multipart/form-data). Multipart: champ 'payload' (JSON SendDocumentDto) + jusqu'à 2 fichiers 'attachments'. Si DocumentType = tax_receipt, le backend choisit automatiquement le CERFA par destinataire."
    )]
    [ProducesResponseType(typeof(SendDocumentResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SendDocumentResultDto>> Send(CancellationToken cancellationToken)
    {
        IReadOnlyList<EmailAttachmentDto>? userAttachments = null;
        SendDocumentDto? dto;

        if (Request.HasFormContentType)
        {
            var formCollection = await Request.ReadFormAsync(cancellationToken);
            var payload = formCollection["payload"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(payload))
                return BadRequest(new ApiMessageResponse { Message = "Form field 'payload' is required for multipart requests." });

            try
            {
                dto = JsonSerializer.Deserialize<SendDocumentDto>(payload,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException)
            {
                return BadRequest(new ApiMessageResponse { Message = "Invalid JSON in form field 'payload'." });
            }

            if (dto is null)
                return BadRequest(new ApiMessageResponse { Message = "Invalid JSON in form field 'payload'." });

            var (parsedAttachments, attachmentError) = await SendingEmailAttachments.ParseAsync(
                formCollection.Files.GetFiles("attachments"), cancellationToken);
            if (attachmentError is not null)
                return BadRequest(new ApiMessageResponse { Message = attachmentError });

            userAttachments = parsedAttachments;
        }
        else
        {
            try
            {
                dto = await JsonSerializer.DeserializeAsync<SendDocumentDto>(
                    Request.Body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                    cancellationToken);
            }
            catch (JsonException)
            {
                return BadRequest(new ApiMessageResponse { Message = "Invalid JSON request body." });
            }
        }

        if (dto is null)
            return BadRequest(new ApiMessageResponse { Message = "Request body is required." });

        if (!DocumentType.IsValid(dto.DocumentType))
            return BadRequest(new ApiMessageResponse { Message = "Type de document invalide" });

        if (dto.Channel != "email")
            return BadRequest(new ApiMessageResponse { Message = "Utilisez 'Courrier' pour les courriers." });

        if (!dto.RecipientIds.Any())
            return BadRequest(new ApiMessageResponse { Message = "Aucun destinataire sélectionné." });

        if (dto.DocumentType != DocumentType.Message && string.IsNullOrWhiteSpace(dto.DocumentBodyHtml))
            return BadRequest(new ApiMessageResponse { Message = "DocumentBodyHtml is required for document types." });

        try
        {
            SendingTaxReceiptRequestValidator.ValidateTaxReceiptPeriod(dto);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiMessageResponse { Message = ex.Message });
        }

        var organizationId = GetOrganizationId();

        await _quotaService.CheckAndIncrementAsync(
            organizationId,
            QuotaTypes.Emails,
            _planService.MaxEmailsAnnual,
            dto.RecipientIds.Count);

        if (dto.DocumentType != DocumentType.Message)
            await _quotaService.CheckAndIncrementAsync(
                organizationId,
                QuotaTypes.Documents,
                _planService.MaxDocumentsAnnual,
                dto.RecipientIds.Count);

        try
        {
            var result = await _sendingService.SendByEmailAsync(dto, organizationId, userAttachments);
            return Ok(result);
        }
        catch (QuotaExceededException qex)
        {
            return StatusCode(403, new
            {
                error = qex.Message,
                quotaType = qex.QuotaType,
                current = qex.Current,
                limit = qex.Limit,
                canUpgrade = true
            });
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("introuvable", StringComparison.OrdinalIgnoreCase))
                return NotFound(new ApiMessageResponse { Message = ex.Message });

            return BadRequest(new ApiMessageResponse { Message = ex.Message });
        }
    }

    // impression PDF
    [HttpPost("print")]
    [SwaggerOperation(
        Summary = "Génération PDF courrier",
        Description = "Génère un PDF pour impression. Si DocumentType = tax_receipt, le backend choisit automatiquement le CERFA par destinataire: cerfa_11580 (particulier) ou cerfa_16216 (entreprise), y compris pour des listes RecipientIds mixtes."
    )]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Print(
        [FromBody] SendDocumentDto dto)
    {
        if (!DocumentType.IsValid(dto.DocumentType))
            return BadRequest(new ApiMessageResponse { Message = "Type de document invalide." });

        if (dto.Channel != "print")
            return BadRequest(new ApiMessageResponse { Message = "Utilisez /send pour les emails." });

        if (dto.RecipientIds is null || !dto.RecipientIds.Any())
            return BadRequest(new ApiMessageResponse { Message = "Aucun destinataire sélectionné." });

        if (dto.DocumentType != DocumentType.Message && string.IsNullOrWhiteSpace(dto.DocumentBodyHtml))
            return BadRequest(new ApiMessageResponse { Message = "DocumentBodyHtml is required for document types." });

        try
        {
            SendingTaxReceiptRequestValidator.ValidateTaxReceiptPeriod(dto);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiMessageResponse { Message = ex.Message });
        }

        var organizationId = GetOrganizationId();

        if (dto.DocumentType != DocumentType.Message)
            await _quotaService.CheckAndIncrementAsync(
                organizationId,
                QuotaTypes.Documents,
                _planService.MaxDocumentsAnnual,
                dto.RecipientIds.Count);

        try
        {
            var result = await _sendingService.GeneratePrintPdfAsync(dto, organizationId);

            return File(result.PdfBytes, "application/pdf",
                $"courriers_{DateTime.Now:yyyyMMdd}.pdf");
        }
        catch (QuotaExceededException qex)
        {
            return StatusCode(403, new
            {
                error = qex.Message,
                quotaType = qex.QuotaType,
                current = qex.Current,
                limit = qex.Limit,
                canUpgrade = true
            });
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("introuvable", StringComparison.OrdinalIgnoreCase))
                return NotFound(new ApiMessageResponse { Message = ex.Message });

            return BadRequest(new ApiMessageResponse { Message = ex.Message });
        }
    }

    // confirmation manuelle qu'un courrier a été posté
    [HttpPatch("confirm-mailed/{mailLogId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConfirmMailed(Guid mailLogId)
    {
        if (mailLogId == Guid.Empty)
            return BadRequest(new ApiMessageResponse { Message = "mailLogId is required." });

        var organizationId = GetOrganizationId();
        try
        {
            await _sendingService.ConfirmMailedAsync(mailLogId, organizationId);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new ApiMessageResponse { Message = ex.Message });
        }
    }

    private Guid GetOrganizationId()
    {
        var claim = User.FindFirst("organization_id")?.Value;
        if (claim is null || !Guid.TryParse(claim, out var organizationId))
            throw new UnauthorizedAccessException("organization_id claim is missing.");
        return organizationId;
    }
}