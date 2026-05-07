using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Kalon.Back.Configuration;
using Kalon.Back.DTOs;
using Kalon.Back.Models;
using Kalon.Back.Services;
using Microsoft.Extensions.Options;

namespace Kalon.Back.Tests;

public class JwtTokenServiceTests
{
    [Fact]
    public void CreateToken_EmbedsExpectedClaims()
    {
        var options = Options.Create(new JwtOptions
        {
            Issuer = "issuer-test",
            Audience = "audience-test",
            SigningKey = "this_is_a_test_signing_key_with_32_chars_min",
            ExpirationMinutes = 30
        });
        var service = new JwtTokenService(options);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "owner@test.local",
            Role = "organization_master"
        };
        var organizationId = Guid.NewGuid();
        var meranStatus = new MeranMembershipStatus
        {
            Plan = "Premium",
            Features = new Dictionary<string, string>
            {
                ["max_annual_documents"] = "999999"
            }
        };

        var token = service.CreateToken(user, organizationId, meranStatus);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal(user.Id.ToString(), jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal(user.Email, jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Equal(organizationId.ToString(), jwt.Claims.First(c => c.Type == "organization_id").Value);
        Assert.Equal(user.Role, jwt.Claims.First(c => c.Type == ClaimTypes.Role).Value);
        Assert.Equal("Premium", jwt.Claims.First(c => c.Type == "plan_name").Value);

        var planFeaturesJson = jwt.Claims.First(c => c.Type == "plan_features").Value;
        var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(planFeaturesJson);
        Assert.NotNull(parsed);
        Assert.Equal("999999", parsed!["max_annual_documents"]);
    }

    [Fact]
    public void CreateToken_UsesFreePlan_WhenPlanIsNull()
    {
        var options = Options.Create(new JwtOptions
        {
            Issuer = "issuer-test",
            Audience = "audience-test",
            SigningKey = "this_is_a_test_signing_key_with_32_chars_min",
            ExpirationMinutes = 30
        });
        var service = new JwtTokenService(options);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "owner@test.local",
            Role = "organization_master"
        };

        var token = service.CreateToken(user, Guid.NewGuid(), new MeranMembershipStatus());

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal("Free", jwt.Claims.First(c => c.Type == "plan_name").Value);
    }
}
