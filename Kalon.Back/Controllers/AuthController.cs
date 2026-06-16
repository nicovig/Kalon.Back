using System.Security.Claims;
using Kalon.Back.Data;
using Kalon.Back.DTOs;
using Kalon.Back.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kalon.Back.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController: ControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IPasswordService _passwordService;
    private readonly IUserPasswordService _userPasswordService;
    private readonly IPasswordResetService _passwordResetService;
    private readonly MeranClient _meranClient;
    private readonly IConfiguration _configuration;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthController(
        ApplicationDbContext dbContext,
        IPasswordService passwordService,
        IUserPasswordService userPasswordService,
        IPasswordResetService passwordResetService,
        MeranClient meranClient,
        IConfiguration configuration,
        IJwtTokenService jwtTokenService)
    {
        _dbContext = dbContext;
        _passwordService = passwordService;
        _userPasswordService = userPasswordService;
        _passwordResetService = passwordResetService;
        _meranClient = meranClient;
        _configuration = configuration;
        _jwtTokenService = jwtTokenService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Login([FromBody] LoginRequest? request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new ApiMessageResponse { Message = "Request body is required." });
        }

        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new ApiMessageResponse { Message = "Email and password are required." });
        }

        var login = request;

        var applicationIdText = _configuration["Application:ApplicationId"];

        if (string.IsNullOrWhiteSpace(applicationIdText) || !Guid.TryParse(applicationIdText, out var applicationId))
        {
            return StatusCode(500, new ApiMessageResponse
            {
                Message = "Erreur de configuration, veuillez contacter l'administrateur."
            });
        }

        var email = login.Email.Trim().ToLowerInvariant();
        var user = await _dbContext.Users
            .Include(u => u.Organization)
            .ThenInclude(o => o!.ContactStatusSettings)
            .SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null)
        {
            return Unauthorized(new ApiMessageResponse { Message = "Utilisateur introuvable" });
        }

        var validPassword = _passwordService.VerifyPassword(login.Password, user.PasswordHash, user.Salt);
        if (!validPassword)
        {
            return Unauthorized(new ApiMessageResponse { Message = "Mot de passe incorrect" });
        }

        if (user.Organization is null)
        {
            return Unauthorized(new ApiMessageResponse { Message = "Association inexistante pour cet utilisateur" });
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
                    Role = user.Role,
                    MeranId = user.MeranId,
                    Organization = new OrganizationLoginResponse
                    {
                        Id = user.Organization.Id,
                        Name = user.Organization.Name,
                        FiscalStatus = user.Organization.FiscalStatus,
                        ContactStatusSettings = user.Organization.ContactStatusSettings is null
                            ? null
                            : new ContactStatusSettingsSummary
                            {
                                NewDurationDays = user.Organization.ContactStatusSettings.NewDurationDays,
                                ToRemindAfterMonths = user.Organization.ContactStatusSettings.ToRemindAfterMonths,
                                InactiveAfterMonths = user.Organization.ContactStatusSettings.InactiveAfterMonths
                            }
                    }
                },
                Meran = new MeranMembershipStatus
                {
                    IsActive = meranStatus.IsActive,
                    Plan = meranStatus.Plan
                }
            };

            response.Token = _jwtTokenService.CreateToken(user, user.Organization.Id, meranStatus);

            return Ok(response);
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(502, new ApiMessageResponse
            {
                Message = "Erreur de connexion au service d'authentification, veuillez contacter l'administrateur.",
                Detail = ex.Message
            });
        }

    }

    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest? request,
        CancellationToken cancellationToken)
    {
        var userId = ResolveUserIdFromJwt();
        if (userId is null)
            return BadRequest(new ApiMessageResponse { Message = "userId is required." });

        if (request is null)
            return BadRequest(new ApiMessageResponse { Message = "Request body is required." });

        if (string.IsNullOrWhiteSpace(request.CurrentPassword)
            || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest(new ApiMessageResponse
            {
                Message = "Current password and new password are required."
            });
        }

        var result = await _userPasswordService.ChangePasswordAsync(
            userId.Value,
            request.CurrentPassword,
            request.NewPassword,
            cancellationToken);

        return result.Status switch
        {
            PasswordChangeStatus.Success => NoContent(),
            PasswordChangeStatus.InvalidCurrentPassword => Unauthorized(new ApiMessageResponse
            {
                Message = "Mot de passe actuel incorrect."
            }),
            PasswordChangeStatus.UserNotFound => NotFound(new ApiMessageResponse
            {
                Message = "Utilisateur introuvable."
            }),
            _ => BadRequest(new ApiMessageResponse { Message = "Unable to change password." })
        };
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new ApiMessageResponse { Message = "Request body is required." });

        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new ApiMessageResponse { Message = "Email is required." });

        await _passwordResetService.RequestResetAsync(request.Email, cancellationToken);
        return NoContent();
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new ApiMessageResponse { Message = "Request body is required." });

        if (string.IsNullOrWhiteSpace(request.Token)
            || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest(new ApiMessageResponse
            {
                Message = "Token and new password are required."
            });
        }

        var result = await _passwordResetService.ResetPasswordAsync(
            request.Token,
            request.NewPassword,
            cancellationToken);

        return result.Status switch
        {
            PasswordResetStatus.Success => NoContent(),
            PasswordResetStatus.InvalidOrExpiredToken => BadRequest(new ApiMessageResponse
            {
                Message = "Lien de réinitialisation invalide ou expiré."
            }),
            _ => BadRequest(new ApiMessageResponse { Message = "Unable to reset password." })
        };
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
