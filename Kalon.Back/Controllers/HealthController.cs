using Microsoft.AspNetCore.Mvc;
using Kalon.Back.DTOs;

namespace Kalon.Back.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    public ActionResult<HealthResponse> Get() => Ok(new HealthResponse { Status = "healthy", Timestamp = DateTime.UtcNow });
}
