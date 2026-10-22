using Kalon.Back.Data;
using Kalon.Back.Models;
using Kalon.Back.Services;
using Microsoft.EntityFrameworkCore;

namespace Kalon.Back.Jobs;

public class GrantMonthlySearchCreditsJob
{
    public const decimal MonthlyCredits = 20m;

    private readonly ApplicationDbContext _db;
    private readonly ISearchCreditService _credits;

    public GrantMonthlySearchCreditsJob(
        ApplicationDbContext db,
        ISearchCreditService credits)
    {
        _db = db;
        _credits = credits;
    }

    public async Task ExecuteAsync()
    {
        var premiumOrgs = await _db.Organizations
            .Where(o => o.CachedPlan.ToLower() == PlanTypes.Premium.ToLower())
            .Select(o => o.Id)
            .ToListAsync();

        foreach (var orgId in premiumOrgs)
            await _credits.GrantMonthlyCreditsAsync(orgId, MonthlyCredits);
    }
}
