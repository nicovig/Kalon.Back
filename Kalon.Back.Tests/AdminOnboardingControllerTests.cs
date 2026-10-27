using Kalon.Back.Configuration;
using Kalon.Back.Controllers;
using Kalon.Back.DTOs;
using Kalon.Back.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Kalon.Back.Tests;

public class AdminOnboardingControllerTests
{
    private sealed class FakeOnboardingService : IAdminOnboardingService
    {
        public AdminOnboardingRequest? LastRequest { get; private set; }
        public bool ThrowDuplicate { get; set; }

        public Task<AdminOnboardingResponse> OnboardAsync(AdminOnboardingRequest request, CancellationToken ct)
        {
            LastRequest = request;
            if (ThrowDuplicate)
                throw new InvalidOperationException("Un compte existe déjà pour test@asso.fr.");

            return Task.FromResult(new AdminOnboardingResponse
            {
                UserId = Guid.NewGuid(),
                OrganizationId = Guid.NewGuid(),
                Email = request.Email.Trim().ToLowerInvariant(),
                TemporaryPassword = "TempPass1!"
            });
        }
    }

    private static AdminOnboardingController CreateController(
        FakeOnboardingService service,
        string apiKey = "secret-admin-key",
        string? headerKey = "secret-admin-key")
    {
        var controller = new AdminOnboardingController(
            service,
            Options.Create(new AdminOptions { ApiKey = apiKey }));

        var httpContext = new DefaultHttpContext();
        if (headerKey is not null)
            httpContext.Request.Headers["X-Admin-Key"] = headerKey;

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    private static AdminOnboardingRequest ValidRequest() => new()
    {
        Firstname = "Marie",
        Lastname = "Dupont",
        Email = "marie@asso.fr",
        AssociationName = "Asso Test"
    };

    [Fact]
    public async Task Onboard_ReturnsUnauthorized_WhenAdminKeyMissing()
    {
        var controller = CreateController(new FakeOnboardingService(), headerKey: null);

        var result = await controller.Onboard(ValidRequest(), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task Onboard_ReturnsUnauthorized_WhenAdminKeyInvalid()
    {
        var controller = CreateController(new FakeOnboardingService(), headerKey: "wrong");

        var result = await controller.Onboard(ValidRequest(), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task Onboard_ReturnsBadRequest_WhenRequiredFieldsMissing()
    {
        var controller = CreateController(new FakeOnboardingService());

        var result = await controller.Onboard(new AdminOnboardingRequest
        {
            Firstname = "Marie",
            Email = "marie@asso.fr"
        }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Onboard_ReturnsCreated_WhenValid()
    {
        var service = new FakeOnboardingService();
        var controller = CreateController(service);

        var result = await controller.Onboard(ValidRequest(), CancellationToken.None);

        var created = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, created.StatusCode);
        var payload = Assert.IsType<AdminOnboardingResponse>(created.Value);
        Assert.Equal("marie@asso.fr", payload.Email);
        Assert.NotNull(service.LastRequest);
    }

    [Fact]
    public async Task Onboard_ReturnsBadRequest_WhenServiceThrowsInvalidOperation()
    {
        var controller = CreateController(new FakeOnboardingService { ThrowDuplicate = true });

        var result = await controller.Onboard(ValidRequest(), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
