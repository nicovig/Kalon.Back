using System.Net;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Security.Claims;
using Kalon.Back.Configuration;
using Kalon.Back.Controllers;
using Kalon.Back.Data;
using Kalon.Back.DTOs;
using Kalon.Back.Dtos;
using Kalon.Back.Models;
using Kalon.Back.Services;
using Kalon.Back.Services.Mail;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Kalon.Back.Tests;


public class AuthControllerTests
{
    private sealed class FakePasswordService : IPasswordService
    {
        private readonly bool _verifyResult;

        public FakePasswordService(bool verifyResult)
        {
            _verifyResult = verifyResult;
        }

        public string GenerateSalt() => "fake-salt";

        public string HashPassword(string password, string salt) => "hash";

        public bool VerifyPassword(string password, string passwordHash, string salt) => _verifyResult;
    }

    private static PasswordService CreateRealPasswordService() =>
        new(Options.Create(new PasswordOptions
        {
            Pepper = "viser_lindependance_financiere_002",
            Iterations = 120000,
            HashSize = 32
        }));

    private sealed class NoOpMailService : IMailService
    {
        public Task SendAsync(MailMessageDto message) => Task.CompletedTask;
    }

    private sealed class FixedTokenProvider : IMeranTokenProvider
    {
        public Task<string> GetBearerTokenAsync(CancellationToken cancellationToken = default) => Task.FromResult("token");
    }

    private sealed class StaticJsonHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _json;

        public StaticJsonHandler(HttpStatusCode statusCode, string json)
        {
            _statusCode = statusCode;
            _json = json;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_json, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

    private static ApplicationDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    private static IConfiguration CreateConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Application:ApplicationId"] = "356c9115-ca1e-4fd7-aa89-d6b07ade1530",
                ["Jwt:Issuer"] = "Kalon.Back.Tests",
                ["Jwt:Audience"] = "Kalon.Front.Tests",
                ["Jwt:SigningKey"] = "Kalon_Back_Tests_Jwt_Signing_Key_At_Least_32_Chars",
                ["Jwt:ExpirationMinutes"] = "60"
            })
            .Build();
    }

    private static IJwtTokenService CreateJwtTokenService() =>
        new JwtTokenService(Options.Create(new JwtOptions
        {
            Issuer = "Kalon.Back.Tests",
            Audience = "Kalon.Front.Tests",
            SigningKey = "Kalon_Back_Tests_Jwt_Signing_Key_At_Least_32_Chars",
            ExpirationMinutes = 60
        }));

    private static void SetAuthenticatedUser(ControllerBase controller, Guid userId)
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim("sub", userId.ToString())
                ], "TestAuth"))
            }
        };
    }

    private static PasswordResetService CreatePasswordResetService(
        ApplicationDbContext dbContext,
        IPasswordService passwordService,
        IMailService? mailService = null)
    {
        return new PasswordResetService(
            dbContext,
            passwordService,
            mailService ?? new NoOpMailService(),
            Options.Create(new PasswordResetOptions
            {
                FrontendResetUrl = "http://localhost:4300/reset-password",
                TokenExpirationMinutes = 60
            }));
    }

    private static AuthController CreateAuthController(
        ApplicationDbContext dbContext,
        IPasswordService passwordService,
        MeranClient meranClient,
        IUserPasswordService? userPasswordService = null,
        IPasswordResetService? passwordResetService = null)
    {
        return new AuthController(
            dbContext,
            passwordService,
            userPasswordService ?? new UserPasswordService(dbContext, passwordService),
            passwordResetService ?? CreatePasswordResetService(dbContext, passwordService),
            meranClient,
            CreateConfiguration(),
            CreateJwtTokenService());
    }

    private static MeranClient CreateMeranClient(HttpStatusCode statusCode, string json)
    {
        var httpClient = new HttpClient(new StaticJsonHandler(statusCode, json));
        return new MeranClient(
            httpClient,
            Options.Create(new MeranOptions { BaseUrl = "http://meran.local" }),
            new FixedTokenProvider());
    }

    private static Organization CreateOrganization(Guid id, Guid userId, User user)
    {
        return new Organization
        {
            Id = id,
            Name = "Test Organization",
            Email = "org@test.local",
            UserId = userId,
            User = user,
            RNA = "W442009999",
            SIRET = "12345678901234",
            FiscalStatus = FiscalStatus.GeneralInterest,
            DefaultReceiptFrequency = ReceiptFrequency.Annually,
            CreatedAt = DateTime.UtcNow
        };
    }

    [Fact]
    public async Task Login_ReturnsOk_WithMeranPayload_WhenCredentialsValid()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            MeranId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Firstname = "John",
            Lastname = "Doe",
            Email = "john@doe.com",
            AssociationName = "Asso",
            PasswordHash = "hash",
            Salt = "salt"
        };
        dbContext.Users.Add(user);
        dbContext.Organizations.Add(CreateOrganization(Guid.NewGuid(), userId, user));
        await dbContext.SaveChangesAsync();

        var controller = CreateAuthController(
            dbContext,
            new FakePasswordService(true),
            CreateMeranClient(HttpStatusCode.OK, "{\"isActive\":true,\"plan\":\"basic\"}"));

        var result = await controller.Login(new LoginRequest { Email = "john@doe.com", Password = "pwd" }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<LoginResponse>(ok.Value);
        Assert.True(payload.Meran.IsActive);
        Assert.Equal("basic", payload.Meran.Plan);
        Assert.Equal("john@doe.com", payload.User.Email);
        Assert.Equal("organization_master", payload.User.Role);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(payload.Token);
        Assert.Contains(jwt.Claims, c => c.Type == "organization_id");
        Assert.Contains(jwt.Claims, c => c.Type == ClaimTypes.Role && c.Value == "organization_master");
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_WhenPasswordInvalid()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            MeranId = Guid.NewGuid(),
            Firstname = "John",
            Lastname = "Doe",
            Email = "john@doe.com",
            AssociationName = "Asso",
            PasswordHash = "hash",
            Salt = "salt"
        };
        dbContext.Users.Add(user);
        dbContext.Organizations.Add(CreateOrganization(Guid.NewGuid(), userId, user));
        await dbContext.SaveChangesAsync();

        var controller = CreateAuthController(
            dbContext,
            new FakePasswordService(false),
            CreateMeranClient(HttpStatusCode.OK, "{\"isActive\":true}"));

        var result = await controller.Login(new LoginRequest { Email = "john@doe.com", Password = "bad" }, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Login_ReturnsBadGateway_WhenMeranFails()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            MeranId = Guid.NewGuid(),
            Firstname = "John",
            Lastname = "Doe",
            Email = "john@doe.com",
            AssociationName = "Asso",
            PasswordHash = "hash",
            Salt = "salt"
        };
        dbContext.Users.Add(user);
        dbContext.Organizations.Add(CreateOrganization(Guid.NewGuid(), userId, user));
        await dbContext.SaveChangesAsync();

        var controller = CreateAuthController(
            dbContext,
            new FakePasswordService(true),
            CreateMeranClient(HttpStatusCode.Unauthorized, "{\"error\":\"forbidden\"}"));

        var result = await controller.Login(new LoginRequest { Email = "john@doe.com", Password = "pwd" }, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(502, objectResult.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_ReturnsNoContent_WhenCurrentPasswordValid()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var passwordService = CreateRealPasswordService();
        var salt = passwordService.GenerateSalt();
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            MeranId = Guid.NewGuid(),
            Firstname = "John",
            Lastname = "Doe",
            Email = "john@doe.com",
            AssociationName = "Asso",
            Salt = salt,
            PasswordHash = passwordService.HashPassword("OldPassword123!", salt)
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var controller = CreateAuthController(
            dbContext,
            passwordService,
            CreateMeranClient(HttpStatusCode.OK, "{\"isActive\":true}"));
        SetAuthenticatedUser(controller, userId);

        var result = await controller.ChangePassword(
            new ChangePasswordRequest
            {
                CurrentPassword = "OldPassword123!",
                NewPassword = "NewPassword456!"
            },
            CancellationToken.None);

        Assert.IsType<NoContentResult>(result);

        var updated = await dbContext.Users.SingleAsync(u => u.Id == userId);
        Assert.True(passwordService.VerifyPassword("NewPassword456!", updated.PasswordHash, updated.Salt));
    }

    [Fact]
    public async Task ChangePassword_ReturnsUnauthorized_WhenCurrentPasswordInvalid()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var passwordService = CreateRealPasswordService();
        var salt = passwordService.GenerateSalt();
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            MeranId = Guid.NewGuid(),
            Firstname = "John",
            Lastname = "Doe",
            Email = "john@doe.com",
            AssociationName = "Asso",
            Salt = salt,
            PasswordHash = passwordService.HashPassword("OldPassword123!", salt)
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var controller = CreateAuthController(
            dbContext,
            passwordService,
            CreateMeranClient(HttpStatusCode.OK, "{\"isActive\":true}"));
        SetAuthenticatedUser(controller, userId);

        var result = await controller.ChangePassword(
            new ChangePasswordRequest
            {
                CurrentPassword = "WrongPassword!",
                NewPassword = "NewPassword456!"
            },
            CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task ChangePassword_ReturnsBadRequest_WhenPasswordsMissing()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var passwordService = CreateRealPasswordService();
        var controller = CreateAuthController(
            dbContext,
            passwordService,
            CreateMeranClient(HttpStatusCode.OK, "{\"isActive\":true}"));
        SetAuthenticatedUser(controller, Guid.NewGuid());

        var result = await controller.ChangePassword(
            new ChangePasswordRequest
            {
                CurrentPassword = "",
                NewPassword = "NewPassword456!"
            },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ForgotPassword_ReturnsNoContent_EvenWhenUserMissing()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var controller = CreateAuthController(
            dbContext,
            CreateRealPasswordService(),
            CreateMeranClient(HttpStatusCode.OK, "{\"isActive\":true}"));

        var result = await controller.ForgotPassword(
            new ForgotPasswordRequest { Email = "missing@doe.com" },
            CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task ForgotPassword_ReturnsBadRequest_WhenEmailMissing()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var controller = CreateAuthController(
            dbContext,
            CreateRealPasswordService(),
            CreateMeranClient(HttpStatusCode.OK, "{\"isActive\":true}"));

        var result = await controller.ForgotPassword(
            new ForgotPasswordRequest { Email = "" },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ResetPassword_ReturnsNoContent_WhenTokenValid()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var passwordService = CreateRealPasswordService();
        var salt = passwordService.GenerateSalt();
        var user = new User
        {
            Id = Guid.NewGuid(),
            MeranId = Guid.NewGuid(),
            Firstname = "John",
            Lastname = "Doe",
            Email = "john@doe.com",
            AssociationName = "Asso",
            Salt = salt,
            PasswordHash = passwordService.HashPassword("OldPassword123!", salt)
        };
        dbContext.Users.Add(user);

        var rawToken = PasswordResetService.GenerateResetToken();
        dbContext.PasswordResetTokens.Add(new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = PasswordResetService.HashToken(rawToken),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        });
        await dbContext.SaveChangesAsync();

        var controller = CreateAuthController(
            dbContext,
            passwordService,
            CreateMeranClient(HttpStatusCode.OK, "{\"isActive\":true}"));

        var result = await controller.ResetPassword(
            new ResetPasswordRequest
            {
                Token = rawToken,
                NewPassword = "NewPassword456!"
            },
            CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        var updated = await dbContext.Users.SingleAsync(u => u.Id == user.Id);
        Assert.True(passwordService.VerifyPassword("NewPassword456!", updated.PasswordHash, updated.Salt));
    }

    [Fact]
    public async Task ResetPassword_ReturnsBadRequest_WhenTokenInvalid()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var controller = CreateAuthController(
            dbContext,
            CreateRealPasswordService(),
            CreateMeranClient(HttpStatusCode.OK, "{\"isActive\":true}"));

        var result = await controller.ResetPassword(
            new ResetPasswordRequest
            {
                Token = "invalid-token",
                NewPassword = "NewPassword456!"
            },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }
}

