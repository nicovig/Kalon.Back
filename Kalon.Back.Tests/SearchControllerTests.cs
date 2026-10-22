using System.Security.Claims;
using Kalon.Back.Controllers;
using Kalon.Back.Configuration;
using Kalon.Back.DTOs;
using Kalon.Back.Models;
using Kalon.Back.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Kalon.Back.Tests;

public class SearchControllerTests
{
    private sealed class FakeSearchCreditService : ISearchCreditService
    {
        public decimal Available { get; set; } = 10m;
        public List<(Guid OrgId, string Type, decimal Cost, string? Siren)> Consumptions { get; } = [];

        public Task<decimal> GetAvailableCreditsAsync(Guid organizationId, CancellationToken cancellationToken = default)
            => Task.FromResult(Available);

        public Task ConsumeAsync(Guid organizationId, string operationType, decimal cost, string? siren = null, CancellationToken cancellationToken = default)
        {
            Consumptions.Add((organizationId, operationType, cost, siren));
            Available -= cost;
            return Task.CompletedTask;
        }

        public Task GrantMonthlyCreditsAsync(Guid organizationId, decimal credits, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeSocieteComService : ISocieteComService
    {
        public Task<List<CompanySearchResultDto>> SearchAsync(string? dep, string? naf, int nbrep = 20, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<CompanySearchResultDto>
            {
                new() { Siren = "123456789", NomCommercial = "Alpha", Dep = dep ?? "" }
            });

        public Task<CompanyBilanDto> GetBilanAsync(string siren, CancellationToken cancellationToken = default)
            => Task.FromResult(new CompanyBilanDto { Siren = siren, Annee = 2023, ChiffreAffaires = 10 });

        public Task<CompanyContactDto> GetContactAsync(string siren, CancellationToken cancellationToken = default)
            => Task.FromResult(new CompanyContactDto { Siren = siren, NomDirigeant = "Dupont" });
    }

    private static SearchController CreateController(
        Guid organizationId,
        string planName,
        FakeSearchCreditService credits,
        FakeSocieteComService? societeCom = null)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("organization_id", organizationId.ToString()),
                new Claim("plan_name", planName)
            ], "TestAuth"))
        };

        var controller = new SearchController(
            credits,
            societeCom ?? new FakeSocieteComService(),
            new PlanService(
                new HttpContextAccessor { HttpContext = httpContext },
                Options.Create(new PlanOptions())));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    [Fact]
    public async Task Search_ReturnsForbidden_WhenNotPremium()
    {
        var controller = CreateController(Guid.NewGuid(), PlanTypes.Basic, new FakeSearchCreditService());

        var result = await controller.Search("49", null);

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, status.StatusCode);
    }

    [Fact]
    public async Task Search_ReturnsPaymentRequired_WhenCreditsInsufficient()
    {
        var credits = new FakeSearchCreditService { Available = 0.05m };
        var controller = CreateController(Guid.NewGuid(), PlanTypes.Premium, credits);

        var result = await controller.Search("49", null);

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(402, status.StatusCode);
    }

    [Fact]
    public async Task Search_ReturnsOk_AndConsumesCredits_WhenPremium()
    {
        var orgId = Guid.NewGuid();
        var credits = new FakeSearchCreditService { Available = 5m };
        var controller = CreateController(orgId, PlanTypes.Premium, credits);

        var result = await controller.Search("49", "0150Z");

        Assert.IsType<OkObjectResult>(result);
        Assert.Single(credits.Consumptions);
        Assert.Equal(orgId, credits.Consumptions[0].OrgId);
        Assert.Equal(SearchOperationTypes.Search, credits.Consumptions[0].Type);
        Assert.Equal(SearchOperationTypes.SearchCost, credits.Consumptions[0].Cost);
    }

    [Fact]
    public async Task GetDetails_ConsumesBilanAndContact()
    {
        var orgId = Guid.NewGuid();
        var credits = new FakeSearchCreditService { Available = 10m };
        var controller = CreateController(orgId, PlanTypes.Premium, credits);

        var result = await controller.GetDetails("123456789");

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(2, credits.Consumptions.Count);
        Assert.Equal(SearchOperationTypes.Bilan, credits.Consumptions[0].Type);
        Assert.Equal(SearchOperationTypes.Contact, credits.Consumptions[1].Type);
        Assert.Equal("123456789", credits.Consumptions[0].Siren);
    }

    [Fact]
    public async Task GetCredits_ReturnsAvailabilityFlags()
    {
        var credits = new FakeSearchCreditService { Available = 1m };
        var controller = CreateController(Guid.NewGuid(), PlanTypes.Premium, credits);

        var result = await controller.GetCredits();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
    }
}
