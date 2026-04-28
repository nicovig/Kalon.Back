using Kalon.Back.DTOs;
using Kalon.Back.Data;
using Kalon.Back.Models;
using Kalon.Back.Services.Mail;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kalon.Back.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "organization_master")]
public class AIMailController(
    ApplicationDbContext db,
    IAiMailGeneratorService aiService) : ControllerBase
{
    [HttpPost("generate-mail")]
    [ProducesResponseType(typeof(AiMailResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<AiMailResultDto>> GenerateMail(
        [FromBody] AiMailRequestDto dto)
    {
        if (!EmailTemplateTypes.IsValid(dto.EmailType))
            return BadRequest(new ApiMessageResponse { Message = "Type de mail invalide." });

        var orgId = GetOrganizationId();

        var org = await db.Organizations
            .FirstOrDefaultAsync(o => o.Id == orgId);

        if (org is null)
            return NotFound(new ApiMessageResponse { Message = "Organisation introuvable." });

        try
        {
            var result = await aiService.GenerateAsync(dto, org);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new ApiMessageResponse { Message = ex.Message });
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