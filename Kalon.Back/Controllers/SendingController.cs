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

    public SendingController(ISendingService sendingService)
    {
        _sendingService = sendingService;
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
    public async Task<IActionResult> Print(
        [FromBody] SendDocumentDto dto)
    {
        if (!DocumentType.IsValid(dto.DocumentType))
            return BadRequest("Type de document invalide.");

        if (dto.Channel != "print")
            return BadRequest("Utilisez /send pour les emails.");

        if (!dto.RecipientIds.Any())
            return BadRequest("Aucun destinataire sélectionné.");

        var organizationId = GetOrganizationId();
        var result = await _sendingService.GeneratePrintPdfAsync(dto, organizationId);

        return File(result.PdfBytes, "application/pdf",
            $"courriers_{DateTime.Now:yyyyMMdd}.pdf");
    }

    // confirmation manuelle qu'un courrier a été posté
    [HttpPatch("confirm-mailed/{mailLogId}")]
    public async Task<IActionResult> ConfirmMailed(Guid mailLogId)
    {
        var organizationId = GetOrganizationId();
        await _sendingService.ConfirmMailedAsync(mailLogId, organizationId);
        return NoContent();
    }

    private Guid GetOrganizationId()
    {
        var claim = User.FindFirst("organization_id")?.Value;
        if (claim is null || !Guid.TryParse(claim, out var organizationId))
            throw new UnauthorizedAccessException("organization_id claim is missing.");
        return organizationId;
    }
}