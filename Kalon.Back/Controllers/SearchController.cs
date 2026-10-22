using Kalon.Back.Models;
using Kalon.Back.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kalon.Back.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "organization_master")]
public class SearchController : ControllerBase
{
    private readonly ISearchCreditService _credits;
    private readonly ISocieteComService _societecom;
    private readonly IPlanService _plan;

    public SearchController(
        ISearchCreditService credits,
        ISocieteComService societecom,
        IPlanService plan)
    {
        _credits = credits;
        _societecom = societecom;
        _plan = plan;
    }

    [HttpGet("companies")]
    public async Task<IActionResult> Search(
        [FromQuery] string? dep,
        [FromQuery] string? naf,
        [FromQuery] int nbrep = 20,
        CancellationToken cancellationToken = default)
    {
        if (!_plan.RechercheMecenesEnabled)
            return StatusCode(403, new { error = "Plan Premium requis." });

        var orgId = GetOrganizationId();
        var available = await _credits.GetAvailableCreditsAsync(orgId, cancellationToken);

        if (available < SearchOperationTypes.SearchCost)
        {
            return StatusCode(402, new
            {
                error = "Credits insuffisants.",
                availableCredits = available,
                canPurchase = true
            });
        }

        var results = await _societecom.SearchAsync(dep, naf, nbrep, cancellationToken);
        await _credits.ConsumeAsync(
            orgId,
            SearchOperationTypes.Search,
            SearchOperationTypes.SearchCost,
            cancellationToken: cancellationToken);

        return Ok(new
        {
            results,
            creditsUsed = SearchOperationTypes.SearchCost,
            availableCredits = available - SearchOperationTypes.SearchCost
        });
    }

    [HttpGet("companies/{siren}/details")]
    public async Task<IActionResult> GetDetails(string siren, CancellationToken cancellationToken = default)
    {
        if (!_plan.RechercheMecenesEnabled)
            return StatusCode(403, new { error = "Plan Premium requis." });

        var orgId = GetOrganizationId();
        var available = await _credits.GetAvailableCreditsAsync(orgId, cancellationToken);

        if (available < SearchOperationTypes.DetailCost)
        {
            return StatusCode(402, new
            {
                error = "Credits insuffisants pour ouvrir cette fiche.",
                availableCredits = available,
                requiredCredits = SearchOperationTypes.DetailCost,
                canPurchase = true
            });
        }

        var bilan = await _societecom.GetBilanAsync(siren, cancellationToken);
        var contact = await _societecom.GetContactAsync(siren, cancellationToken);

        await _credits.ConsumeAsync(
            orgId,
            SearchOperationTypes.Bilan,
            SearchOperationTypes.BilanCost,
            siren,
            cancellationToken);
        await _credits.ConsumeAsync(
            orgId,
            SearchOperationTypes.Contact,
            SearchOperationTypes.ContactCost,
            siren,
            cancellationToken);

        return Ok(new
        {
            bilan,
            contact,
            creditsUsed = SearchOperationTypes.DetailCost,
            availableCredits = available - SearchOperationTypes.DetailCost
        });
    }

    [HttpGet("credits")]
    public async Task<IActionResult> GetCredits(CancellationToken cancellationToken = default)
    {
        var orgId = GetOrganizationId();
        var available = await _credits.GetAvailableCreditsAsync(orgId, cancellationToken);

        return Ok(new
        {
            availableCredits = available,
            canSearch = available >= SearchOperationTypes.SearchCost,
            canOpenDetail = available >= SearchOperationTypes.DetailCost
        });
    }

    private Guid GetOrganizationId()
    {
        var claim = User.FindFirst("organization_id")?.Value;
        if (claim is null || !Guid.TryParse(claim, out var id))
            throw new UnauthorizedAccessException("organization_id manquant.");
        return id;
    }
}
