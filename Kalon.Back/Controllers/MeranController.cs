using System.Text.Json;
using Kalon.Back.Data;
using Kalon.Back.Models;
using Kalon.Back.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;

namespace Kalon.Back.Controllers
{
    [ApiController]
    [Route("api/meran")]
    public class MeranController(ApplicationDbContext dbContext, MeranClient meranClient, IConfiguration configuration) : ControllerBase
    {
        [HttpGet("users/{userId:guid}/status")]
        public async Task<ActionResult<JsonElement>> GetUserStatus([FromRoute] Guid userId, CancellationToken cancellationToken)
        {
            var user = await dbContext.Users.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);
            if (user is null)
            {
                return NotFound(new { message = "User not found." });
            }

            var applicationIdText = configuration["Application:ApplicationId"];
            if (string.IsNullOrWhiteSpace(applicationIdText) || !Guid.TryParse(applicationIdText, out var applicationId))
            {
                return StatusCode(500, new { message = "ApplicationId is not configured." });
            }
            var status = await meranClient.GetUserStatusAsync(applicationId, user.MeranId, cancellationToken);
            return Ok(status);
        }
    }
}

