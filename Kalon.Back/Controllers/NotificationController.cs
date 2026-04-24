using System.Security.Claims;
using Kalon.Back.DTOs;
using Kalon.Back.Services.Notification;
using Kalon.Back.Services.OrganizationAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kalon.Back.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "organization_master")]
public class NotificationController(
    IUserOrganizationAccessService userOrganizationAccess,
    INotificationDashboardService notificationDashboardService) : ControllerBase
{
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(NotificationDashboardResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken)
    {
        var userId = ResolveUserIdFromJwt();
        if (userId is null)
            return BadRequest(new ApiMessageResponse { Message = "userId is required." });

        var access = await userOrganizationAccess.ResolveAsync(userId.Value, cancellationToken);
        switch (access)
        {
            case OrganizationAccessOutcome.InvalidUserId:
                return BadRequest(new ApiMessageResponse { Message = "userId is required." });
            case OrganizationAccessOutcome.OrganizationNotFoundForUser:
                return NotFound(new ApiMessageResponse { Message = "Organization not found for user." });
            case OrganizationAccessOutcome.Ok(var organizationId):
                var dashboard = await notificationDashboardService.GetDashboardAsync(organizationId, cancellationToken);
                return Ok(dashboard);
            default:
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new ApiMessageResponse { Message = "Unexpected organization access state." });
        }
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
