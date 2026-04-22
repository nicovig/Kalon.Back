using Kalon.Back.Dtos;
using Kalon.Back.DTOs;
using Kalon.Back.Models;
using Kalon.Back.Services.Mail;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Kalon.Back.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "organization_master")]
public class SendingController : ControllerBase
{
    private readonly ISendingService _sendingService;
    private readonly IVariableResolverService _variableResolverService;

    public SendingController(ISendingService sendingService, IVariableResolverService variableResolverService)
    {
        _sendingService = sendingService;
        _variableResolverService = variableResolverService;
    }

    [HttpGet("mail-editor-tags")]
    [ProducesResponseType(typeof(List<MailEditorVariableTag>), StatusCodes.Status200OK)]
    public ActionResult<List<MailEditorVariableTag>> GetMailEditorTags([FromQuery] bool hasCompanyRecipient = false)
    {
        var tags = _variableResolverService.GetAvailableTags(hasCompanyRecipient).ToList();
        return Ok(tags);
    }

    [HttpPost("send")]
    [ProducesResponseType(typeof(SendDocumentResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SendDocumentResultDto>> Send(
        [FromBody] SendDocumentDto dto)
    {
        if (!DocumentType.IsValid(dto.DocumentType))
            return BadRequest(new ApiMessageResponse { Message = "Type de document invalide" });

        if (dto.Channel != "email")
            return BadRequest(new ApiMessageResponse { Message = "Utilisez 'Courrier' pour les courriers." });

        if (!dto.RecipientIds.Any())
            return BadRequest(new ApiMessageResponse { Message = "Aucun destinataire sélectionné." });

        var organizationId = GetOrganizationId();
        var result = await _sendingService.SendByEmailAsync(dto, organizationId);
        return Ok(result);
    }

    // impression PDF
    [HttpPost("print")]
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

        var organizationId = GetOrganizationId();
        try
        {
            var result = await _sendingService.GeneratePrintPdfAsync(dto, organizationId);

            return File(result.PdfBytes, "application/pdf",
                $"courriers_{DateTime.Now:yyyyMMdd}.pdf");
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new ApiMessageResponse { Message = ex.Message });
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