using Kalon.Back.Data;
using Kalon.Back.Dtos;
using Kalon.Back.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;


namespace Kalon.Back.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController: ControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IPasswordService _passwordService;
    private readonly MeranClient _meranClient;
    private readonly IConfiguration _configuration;

    public AuthController(
        ApplicationDbContext dbContext,
        IPasswordService passwordService,
        MeranClient meranClient,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _passwordService = passwordService;
        _meranClient = meranClient;
        _configuration = configuration;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _dbContext.Users.SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null)
        {
            return Unauthorized(new { message = "Utilisateur introuvable" });
        }

        var validPassword = _passwordService.VerifyPassword(request.Password, user.PasswordHash, user.Salt);
        if (!validPassword)
        {
            return Unauthorized(new { message = "Mot de passe incorrect" });
        }

        var applicationIdText = _configuration["Application:ApplicationId"];
        if (string.IsNullOrWhiteSpace(applicationIdText) || !Guid.TryParse(applicationIdText, out var applicationId))
        {
            return StatusCode(500, new { message = "ApplicationId is not configured." });
        }

        try
        {
            var meranStatus = await _meranClient.GetUserStatusAsync(applicationId, user.MeranId, cancellationToken);

            var response = new LoginResponse
            {
                Token = Guid.NewGuid().ToString("N"),
                User = new LoginUserResponse
                {
                    Id = user.Id,
                    Firstname = user.Firstname,
                    Lastname = user.Lastname,
                    Email = user.Email,
                    AssociationName = user.AssociationName,
                    MeranId = user.MeranId
                },
                Meran = meranStatus
            };

            return Ok(response);
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(502, new
            {
                message = "Unable to fetch user status from authentication web service.",
                detail = ex.Message
            });
        }

    }
}
