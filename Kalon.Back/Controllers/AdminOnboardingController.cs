using Kalon.Back.Configuration;
using Kalon.Back.DTOs;
using Kalon.Back.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Kalon.Back.Controllers;

[ApiController]
[Route("api/admin")]
public class AdminOnboardingController(
    IAdminOnboardingService onboardingService,
    IOptions<AdminOptions> adminOptions) : ControllerBase
{
    [HttpPost("onboard")]
    [ProducesResponseType(typeof(AdminOnboardingResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Onboard(
        [FromBody] AdminOnboardingRequest request,
        CancellationToken ct)
    {
        if (!Request.Headers.TryGetValue("X-Admin-Key", out var key)
            || key != adminOptions.Value.ApiKey)
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Firstname) ||
            string.IsNullOrWhiteSpace(request.Lastname) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.AssociationName))
            return BadRequest(new { message = "Firstname, Lastname, Email et AssociationName sont requis." });

        try
        {
            var result = await onboardingService.OnboardAsync(request, ct);
            return StatusCode(201, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
