using Kalon.Back.Data;
using Kalon.Back.Dtos;
using Kalon.Back.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;


namespace Kalon.Back.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController: ControllerBase
{
        private readonly ApplicationDbContext _dbContext;
        private readonly IPasswordService _passwordService;

    public AuthController(ApplicationDbContext dbContext, IPasswordService passwordService)
    {
        _dbContext = dbContext;
        _passwordService = passwordService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _dbContext.Users.SingleOrDefaultAsync(u => u.Email == email);

        if (user is null)
        {
            return Unauthorized(new { message = "Utilisateur introuvable" });
        }

        var validPassword = _passwordService.VerifyPassword(request.Password, user.PasswordHash, user.Salt);
        if (!validPassword)
        {
            return Unauthorized(new { message = "Mot de passe incorrect" });
        }

        return Ok(new
        {
            token = Guid.NewGuid().ToString("N"),
            user = new
            {
                user.Id,
                user.Firstname,
                user.Lastname,
                user.Email,
                user.AssociationName,
            }
        });
    }
}
