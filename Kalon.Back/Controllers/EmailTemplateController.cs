using Kalon.Back.Data;
using Kalon.Back.DTOs;
using Kalon.Back.Models;
using Kalon.Back.Services.OrganizationAccess;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kalon.Back.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "organization_master")]
public class EmailTemplateController(
    ApplicationDbContext dbContext,
    IUserOrganizationAccessService userOrganizationAccess) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(EmailTemplateResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create([FromBody] EmailTemplateUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var userId = ResolveUserIdFromJwt();
        if (userId is null)
            return BadRequest(new ApiMessageResponse { Message = "userId is required." });

        var access = await userOrganizationAccess.ResolveAsync(userId.Value, cancellationToken);
        var resolved = access.ToActionResult();
        if (!resolved.Success)
            return resolved.Error!;

        var organizationId = resolved.OrganizationId;
        var validationError = ValidateRequest(request);
        if (validationError is not null)
            return BadRequest(new ApiMessageResponse { Message = validationError });

        var template = new EmailTemplate
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            CreatedAt = DateTime.UtcNow
        };
        ApplyRequest(template, request);

        dbContext.EmailTemplates.Add(template);
        await dbContext.SaveChangesAsync(cancellationToken);

        var result = await ProjectAsync(template.Id, organizationId, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = template.Id }, result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<EmailTemplateResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var userId = ResolveUserIdFromJwt();
        if (userId is null)
            return BadRequest(new ApiMessageResponse { Message = "userId is required." });

        var access = await userOrganizationAccess.ResolveAsync(userId.Value, cancellationToken);
        var resolved = access.ToActionResult();
        if (!resolved.Success)
            return resolved.Error!;

        var organizationId = resolved.OrganizationId;

        var items = await dbContext.EmailTemplates
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new EmailTemplateResponse
            {
                Id = x.Id,
                OrganizationId = x.OrganizationId,
                Name = x.Name,
                Subject = x.Subject,
                Body = x.Body,
                EmailTemplateType = x.EmailTemplateType,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EmailTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var userId = ResolveUserIdFromJwt();
        if (userId is null)
            return BadRequest(new ApiMessageResponse { Message = "userId is required." });

        var access = await userOrganizationAccess.ResolveAsync(userId.Value, cancellationToken);
        var resolved = access.ToActionResult();
        if (!resolved.Success)
            return resolved.Error!;

        var organizationId = resolved.OrganizationId;
        var result = await ProjectAsync(id, organizationId, cancellationToken);
        if (result is null)
            return NotFound(new ApiMessageResponse { Message = "Email template not found." });

        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(EmailTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromRoute] Guid id,
        [FromBody] EmailTemplateUpsertRequest request, CancellationToken cancellationToken)
    {
        var userId = ResolveUserIdFromJwt();
        if (userId is null)
            return BadRequest(new ApiMessageResponse { Message = "userId is required." });

        var access = await userOrganizationAccess.ResolveAsync(userId.Value, cancellationToken);
        var resolved = access.ToActionResult();
        if (!resolved.Success)
            return resolved.Error!;

        var organizationId = resolved.OrganizationId;
        var validationError = ValidateRequest(request);
        if (validationError is not null)
            return BadRequest(new ApiMessageResponse { Message = validationError });

        var template = await dbContext.EmailTemplates
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == id, cancellationToken);
        if (template is null)
            return NotFound(new ApiMessageResponse { Message = "Email template not found." });

        ApplyRequest(template, request);
        template.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        var result = await ProjectAsync(template.Id, organizationId, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete([FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var userId = ResolveUserIdFromJwt();
        if (userId is null)
            return BadRequest(new ApiMessageResponse { Message = "userId is required." });

        var access = await userOrganizationAccess.ResolveAsync(userId.Value, cancellationToken);
        var resolved = access.ToActionResult();
        if (!resolved.Success)
            return resolved.Error!;

        var organizationId = resolved.OrganizationId;
        var template = await dbContext.EmailTemplates
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == id, cancellationToken);
        if (template is null)
            return NotFound(new ApiMessageResponse { Message = "Email template not found." });

        dbContext.EmailTemplates.Remove(template);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static string? ValidateRequest(EmailTemplateUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return "Name is required.";
        if (string.IsNullOrWhiteSpace(request.Subject))
            return "Subject is required.";
        if (string.IsNullOrWhiteSpace(request.Body))
            return "Body is required.";
        if (!EmailTemplateTypes.IsValid(request.EmailTemplateType))
            return "Invalid email template type.";
        return null;
    }

    private static void ApplyRequest(EmailTemplate template, EmailTemplateUpsertRequest request)
    {
        template.Name = request.Name.Trim();
        template.Subject = request.Subject.Trim();
        template.Body = request.Body.Trim();
        template.EmailTemplateType = request.EmailTemplateType.Trim();
    }

    private Task<EmailTemplateResponse?> ProjectAsync(Guid id, Guid organizationId, CancellationToken cancellationToken)
    {
        return dbContext.EmailTemplates
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.Id == id)
            .Select(x => new EmailTemplateResponse
            {
                Id = x.Id,
                OrganizationId = x.OrganizationId,
                Name = x.Name,
                Subject = x.Subject,
                Body = x.Body,
                EmailTemplateType = x.EmailTemplateType,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);
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
