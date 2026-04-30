using Kalon.Back.Data;
using Kalon.Back.DTOs;
using Kalon.Back.Models;
using Kalon.Back.Services;
using Kalon.Back.Services.Mail;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kalon.Back.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "organization_master")]
public class AIMailController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IAiMailGeneratorService _aiService;
    private readonly IPlanService _planService;

    public AIMailController(ApplicationDbContext db,
        IAiMailGeneratorService aiService,
        IPlanService planService)
    {
        _db = db;
        _aiService = aiService;
        _planService = planService;
    }

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

        if (!_planService.IaMailEnabled)
            return StatusCode(403, new
            {
                error = "La génération IA nécessite le plan Basic ou Premium.",
                feature = "mail_ai",
                canUpgrade = true
            });

        var org = await _db.Organizations
            .FirstOrDefaultAsync(o => o.Id == orgId);

        if (org is null)
            return NotFound(new ApiMessageResponse { Message = "Organisation introuvable." });

        try
        {
            var result = await _aiService.GenerateAsync(dto, org);
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