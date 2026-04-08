using System.Net;
using System.Text;
using Kalon.Back.Controllers;
using Kalon.Back.Data;
using Kalon.Back.Dtos;
using Kalon.Back.Models;
using Kalon.Back.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

public class AuthControllerTests
{
    private sealed class FakePasswordService : IPasswordService
    {
        private readonly bool _verifyResult;

        public FakePasswordService(bool verifyResult)
        {
            _verifyResult = verifyResult;
        }

        public string HashPassword(string password, string salt) => "hash";

        public bool VerifyPassword(string password, string passwordHash, string salt) => _verifyResult;
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
                ["Application:ApplicationId"] = "356c9115-ca1e-4fd7-aa89-d6b07ade1530"
            })
            .Build();
    }

    [Fact]
    public async Task Login_ReturnsOk_WithMeranPayload_WhenCredentialsValid()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        dbContext.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            MeranId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Firstname = "John",
            Lastname = "Doe",
            Email = "john@doe.com",
            AssociationName = "Asso",
            PasswordHash = "hash",
            Salt = "salt"
        });
        await dbContext.SaveChangesAsync();

        using var httpClient = new HttpClient(new StaticJsonHandler(HttpStatusCode.OK, "{\"isActive\":true,\"plan\":\"basic\"}"));
        var meranClient = new MeranClient(
            httpClient,
            Options.Create(new MeranOptions { BaseUrl = "http://meran.local" }),
            new FixedTokenProvider());
        var controller = new AuthController(dbContext, new FakePasswordService(true), meranClient, CreateConfiguration());

        var result = await controller.Login(new LoginRequest { Email = "john@doe.com", Password = "pwd" }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<LoginResponse>(ok.Value);
        Assert.True(payload.Meran.IsActive);
        Assert.Equal("basic", payload.Meran.Plan);
        Assert.Equal("john@doe.com", payload.User.Email);
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_WhenPasswordInvalid()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        dbContext.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            MeranId = Guid.NewGuid(),
            Firstname = "John",
            Lastname = "Doe",
            Email = "john@doe.com",
            AssociationName = "Asso",
            PasswordHash = "hash",
            Salt = "salt"
        });
        await dbContext.SaveChangesAsync();

        using var httpClient = new HttpClient(new StaticJsonHandler(HttpStatusCode.OK, "{\"isActive\":true}"));
        var meranClient = new MeranClient(
            httpClient,
            Options.Create(new MeranOptions { BaseUrl = "http://meran.local" }),
            new FixedTokenProvider());
        var controller = new AuthController(dbContext, new FakePasswordService(false), meranClient, CreateConfiguration());

        var result = await controller.Login(new LoginRequest { Email = "john@doe.com", Password = "bad" }, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Login_ReturnsBadGateway_WhenMeranFails()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        dbContext.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            MeranId = Guid.NewGuid(),
            Firstname = "John",
            Lastname = "Doe",
            Email = "john@doe.com",
            AssociationName = "Asso",
            PasswordHash = "hash",
            Salt = "salt"
        });
        await dbContext.SaveChangesAsync();

        using var httpClient = new HttpClient(new StaticJsonHandler(HttpStatusCode.Unauthorized, "{\"error\":\"forbidden\"}"));
        var meranClient = new MeranClient(
            httpClient,
            Options.Create(new MeranOptions { BaseUrl = "http://meran.local" }),
            new FixedTokenProvider());
        var controller = new AuthController(dbContext, new FakePasswordService(true), meranClient, CreateConfiguration());

        var result = await controller.Login(new LoginRequest { Email = "john@doe.com", Password = "pwd" }, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(502, objectResult.StatusCode);
    }
}

