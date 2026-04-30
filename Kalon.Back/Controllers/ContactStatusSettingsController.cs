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
[Route("api/contact-status-settings")]
[Authorize(Roles = "organization_master")]
public class ContactStatusSettingsController(
    ApplicationDbContext dbContext,
    IUserOrganizationAccessService userOrganizationAccess) : ControllerBase
{
    private const int DefaultNewDurationDays = 30;
    private const int DefaultToRemindAfterMonths = 12;
    private const int DefaultInactiveAfterMonths = 24;

    [HttpGet]
    [ProducesResponseType(typeof(ContactStatusSettings), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var userId = ResolveUserIdFromJwt();
        if (userId is null)
            return BadRequest(new ApiMessageResponse { Message = "userId is required." });

        var access = await userOrganizationAccess.ResolveAsync(userId.Value, cancellationToken);
        var resolved = access.ToActionResult();
        if (!resolved.Success)
            return resolved.Error!;

        var organizationId = resolved.OrganizationId;
        var settings = await dbContext.ContactStatusSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId, cancellationToken);

        if (settings is null)
            return Ok(DefaultModel(organizationId));

        return Ok(settings);
    }

    [HttpPut]
    [ProducesResponseType(typeof(ContactStatusSettings), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Upsert([FromBody] ContactStatusSettingsUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var validationError = Validate(request);
        if (validationError is not null)
            return BadRequest(new ApiMessageResponse { Message = validationError });

        var userId = ResolveUserIdFromJwt();
        if (userId is null)
            return BadRequest(new ApiMessageResponse { Message = "userId is required." });

        var access = await userOrganizationAccess.ResolveAsync(userId.Value, cancellationToken);
        var resolved = access.ToActionResult();
        if (!resolved.Success)
            return resolved.Error!;

        var organizationId = resolved.OrganizationId;
        var settings = await dbContext.ContactStatusSettings
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId, cancellationToken);

        if (settings is null)
        {
            settings = new ContactStatusSettings
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                CreatedAt = DateTime.UtcNow
            };
            dbContext.ContactStatusSettings.Add(settings);
        }
        else
        {
            settings.UpdatedAt = DateTime.UtcNow;
        }

        settings.NewDurationDays = request.NewDurationDays;
        settings.ToRemindAfterMonths = request.ToRemindAfterMonths;
        settings.InactiveAfterMonths = request.InactiveAfterMonths;

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(settings);
    }

    [HttpPost("reset")]
    [ProducesResponseType(typeof(ContactStatusSettings), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetToDefaults(CancellationToken cancellationToken)
    {
        var userId = ResolveUserIdFromJwt();
        if (userId is null)
            return BadRequest(new ApiMessageResponse { Message = "userId is required." });

        var access = await userOrganizationAccess.ResolveAsync(userId.Value, cancellationToken);
        var resolved = access.ToActionResult();
        if (!resolved.Success)
            return resolved.Error!;

        var organizationId = resolved.OrganizationId;
        var settings = await dbContext.ContactStatusSettings
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId, cancellationToken);

        if (settings is null)
        {
            settings = new ContactStatusSettings
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                CreatedAt = DateTime.UtcNow
            };
            dbContext.ContactStatusSettings.Add(settings);
        }
        else
        {
            settings.UpdatedAt = DateTime.UtcNow;
        }

        settings.NewDurationDays = DefaultNewDurationDays;
        settings.ToRemindAfterMonths = DefaultToRemindAfterMonths;
        settings.InactiveAfterMonths = DefaultInactiveAfterMonths;

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(settings);
    }

    private static ContactStatusSettings DefaultModel(Guid organizationId) => new()
    {
        OrganizationId = organizationId,
        NewDurationDays = DefaultNewDurationDays,
        ToRemindAfterMonths = DefaultToRemindAfterMonths,
        InactiveAfterMonths = DefaultInactiveAfterMonths
    };

    private static string? Validate(ContactStatusSettingsUpsertRequest request)
    {
        if (request.NewDurationDays < 0)
            return "NewDurationDays must be greater than or equal to 0.";
        if (request.ToRemindAfterMonths < 0)
            return "ToRemindAfterMonths must be greater than or equal to 0.";
        if (request.InactiveAfterMonths < 0)
            return "InactiveAfterMonths must be greater than or equal to 0.";
        if (request.InactiveAfterMonths < request.ToRemindAfterMonths)
            return "InactiveAfterMonths must be greater than or equal to ToRemindAfterMonths.";
        return null;
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
