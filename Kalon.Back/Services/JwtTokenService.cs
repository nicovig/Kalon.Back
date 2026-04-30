using Kalon.Back.Configuration;
using Kalon.Back.DTOs;
using Kalon.Back.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace Kalon.Back.Services;

public interface IJwtTokenService
{
    string CreateToken(User user, Guid organizationId, MeranMembershipStatus meranStatus);
}

public class JwtTokenService(IOptions<JwtOptions> options) : IJwtTokenService
{
    private readonly JwtOptions _options = options.Value;

    public string CreateToken(User user, Guid organizationId, MeranMembershipStatus meranStatus)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("organization_id", organizationId.ToString()),
            new(ClaimTypes.Role, user.Role),
            new("plan_name",meranStatus.Plan ?? "Free"),
            new("plan_features", JsonSerializer.Serialize(meranStatus.Features))
        };

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_options.ExpirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
