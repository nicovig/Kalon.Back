using Microsoft.AspNetCore.Mvc;

namespace Kalon.Back.Services.OrganizationAccess;

public readonly record struct OrganizationAccessActionResult(bool Success, Guid OrganizationId, IActionResult? Error);

public static class OrganizationAccessMapper
{
    public static OrganizationAccessActionResult ToActionResult(this OrganizationAccessOutcome outcome)
    {
        return outcome switch
        {
            OrganizationAccessOutcome.InvalidUserId => new OrganizationAccessActionResult(false, default,
                new BadRequestObjectResult(new { message = "userId is required." })),
            OrganizationAccessOutcome.OrganizationNotFoundForUser => new OrganizationAccessActionResult(false, default,
                new NotFoundObjectResult(new { message = "Organization not found for user." })),
            OrganizationAccessOutcome.Ok(var id) => new OrganizationAccessActionResult(true, id, null),
            _ => throw new InvalidOperationException()
        };
    }
}
