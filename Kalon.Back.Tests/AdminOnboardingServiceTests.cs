using System.Net;
using System.Text;
using Kalon.Back.Configuration;
using Kalon.Back.Data;
using Kalon.Back.DTOs;
using Kalon.Back.Models;
using Kalon.Back.Services;
using Kalon.Back.Services.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kalon.Back.Tests;

public class AdminOnboardingServiceTests
{
    private const string Pepper = "viser_lindependance_financiere_002";
    private static readonly Guid ApplicationId = Guid.Parse("356c9115-ca1e-4fd7-aa89-d6b07ade1530");

    private sealed class FakeOnboardingMailService : IOnboardingMailService
    {
        public WelcomeMailData? LastData { get; private set; }

        public Task SendWelcomeAsync(WelcomeMailData data, CancellationToken ct)
        {
            LastData = data;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTokenProvider : IMeranTokenProvider
    {
        public Task<string> GetBearerTokenAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("token123");
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseJson;

        public CapturingHandler(HttpStatusCode statusCode, string responseJson)
        {
            _statusCode = statusCode;
            _responseJson = responseJson;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content is not null)
                LastBody = await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseJson, Encoding.UTF8, "application/json")
            };
        }
    }

    private static ApplicationDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    private static PasswordService CreatePasswordService() =>
        new(Options.Create(new PasswordOptions
        {
            Pepper = Pepper,
            Iterations = 120000,
            HashSize = 32
        }));

    private static AdminOnboardingService CreateService(
        ApplicationDbContext db,
        CapturingHandler handler,
        FakeOnboardingMailService mail,
        Guid? applicationId = null)
    {
        var meranClient = new MeranClient(
            new HttpClient(handler),
            Options.Create(new MeranOptions { BaseUrl = "http://meran.local" }),
            new FixedTokenProvider());

        return new AdminOnboardingService(
            db,
            CreatePasswordService(),
            meranClient,
            mail,
            Options.Create(new MeranOptions
            {
                BaseUrl = "http://meran.local",
                ApplicationId = applicationId ?? ApplicationId
            }));
    }

    private static AdminOnboardingRequest ValidRequest() => new()
    {
        Firstname = "Marie",
        Lastname = "Dupont",
        Email = " Marie.Dupont@Asso.FR ",
        AssociationName = " Asso Test ",
        Phone = "0600000000",
        Website = "https://asso.fr",
        RNA = "W441234567"
    };

    [Fact]
    public async Task OnboardAsync_CreatesUserOrganizationAndSendsWelcomeMail()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var meranUserId = Guid.Parse("7fa94a46-e440-4fce-b52b-be011a952c49");
        var handler = new CapturingHandler(HttpStatusCode.OK, $"{{\"id\":\"{meranUserId}\"}}");
        var mail = new FakeOnboardingMailService();
        var service = CreateService(db, handler, mail);

        var result = await service.OnboardAsync(ValidRequest(), CancellationToken.None);

        Assert.Equal("marie.dupont@asso.fr", result.Email);
        Assert.False(string.IsNullOrWhiteSpace(result.TemporaryPassword));

        var user = await db.Users.SingleAsync();
        Assert.Equal(result.UserId, user.Id);
        Assert.Equal(meranUserId, user.MeranId);
        Assert.True(CreatePasswordService().VerifyPassword(
            result.TemporaryPassword,
            user.PasswordHash,
            user.Salt));

        Assert.Equal(PlanTypes.Free, (await db.Organizations.SingleAsync()).CachedPlan);
        Assert.Equal("W441234567", (await db.Organizations.SingleAsync()).RNA);

        var settings = await db.ContactStatusSettings.SingleAsync();
        Assert.Equal(DefaultTagValue.DefaultNewDurationDays, settings.NewDurationDays);
        Assert.Equal(DefaultTagValue.DefaultToRemindAfterMonths, settings.ToRemindAfterMonths);
        Assert.Equal(DefaultTagValue.DefaultInactiveAfterMonths, settings.InactiveAfterMonths);

        Assert.NotNull(mail.LastData);
        Assert.Equal("marie.dupont@asso.fr", mail.LastData!.Email);
        Assert.Equal(result.TemporaryPassword, mail.LastData.TemporaryPassword);

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal(
            $"http://meran.local/api/applications/{ApplicationId}/users",
            handler.LastRequest.RequestUri!.ToString());
        Assert.Contains("Dupont Marie", handler.LastBody);
        Assert.Contains("marie.dupont@asso.fr", handler.LastBody);
        Assert.DoesNotContain("Password", handler.LastBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OnboardAsync_Throws_WhenApplicationIdEmpty()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var handler = new CapturingHandler(HttpStatusCode.OK, "{\"id\":\"11111111-1111-1111-1111-111111111111\"}");
        var service = CreateService(db, handler, new FakeOnboardingMailService(), Guid.Empty);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.OnboardAsync(ValidRequest(), CancellationToken.None));

        Assert.Contains("ApplicationId", ex.Message);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task OnboardAsync_Throws_WhenEmailAlreadyExists()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            MeranId = Guid.NewGuid(),
            Firstname = "Existing",
            Lastname = "User",
            Email = "marie.dupont@asso.fr",
            AssociationName = "Other",
            PasswordHash = "hash",
            Salt = "salt"
        });
        await db.SaveChangesAsync();

        var handler = new CapturingHandler(HttpStatusCode.OK, "{\"id\":\"11111111-1111-1111-1111-111111111111\"}");
        var service = CreateService(db, handler, new FakeOnboardingMailService());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.OnboardAsync(ValidRequest(), CancellationToken.None));

        Assert.Contains("marie.dupont@asso.fr", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task OnboardAsync_DoesNotPersist_WhenMeranFails()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var handler = new CapturingHandler(HttpStatusCode.BadRequest, "{\"error\":\"fail\"}");
        var mail = new FakeOnboardingMailService();
        var service = CreateService(db, handler, mail);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.OnboardAsync(ValidRequest(), CancellationToken.None));

        Assert.Empty(db.Users);
        Assert.Empty(db.Organizations);
        Assert.Null(mail.LastData);
    }
}
