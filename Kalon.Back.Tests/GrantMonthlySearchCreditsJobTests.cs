using Kalon.Back.Data;
using Kalon.Back.Jobs;
using Kalon.Back.Models;
using Kalon.Back.Services;
using Microsoft.EntityFrameworkCore;

namespace Kalon.Back.Tests;

public class GrantMonthlySearchCreditsJobTests
{
    private sealed class TrackingCreditService : ISearchCreditService
    {
        public List<(Guid OrgId, decimal Credits)> Grants { get; } = [];

        public Task<decimal> GetAvailableCreditsAsync(Guid organizationId, CancellationToken cancellationToken = default)
            => Task.FromResult(0m);

        public Task ConsumeAsync(Guid organizationId, string operationType, decimal cost, string? siren = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task GrantMonthlyCreditsAsync(Guid organizationId, decimal credits, CancellationToken cancellationToken = default)
        {
            Grants.Add((organizationId, credits));
            return Task.CompletedTask;
        }
    }

    private static ApplicationDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    private static Organization CreateOrganization(Guid id, string plan) => new()
    {
        Id = id,
        Name = "Org",
        Email = $"{id}@test.local",
        UserId = Guid.NewGuid(),
        RNA = "W1",
        SIRET = "1",
        FiscalStatus = FiscalStatus.GeneralInterest,
        DefaultReceiptFrequency = ReceiptFrequency.Annually,
        CreatedAt = DateTime.UtcNow,
        CachedPlan = plan
    };

    [Fact]
    public async Task ExecuteAsync_GrantsCredits_OnlyToPremiumOrganizations()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var premiumId = Guid.NewGuid();
        var basicId = Guid.NewGuid();
        db.Organizations.AddRange(
            CreateOrganization(premiumId, PlanTypes.Premium),
            CreateOrganization(basicId, PlanTypes.Basic),
            CreateOrganization(Guid.NewGuid(), "premium"));
        await db.SaveChangesAsync();

        var credits = new TrackingCreditService();
        var job = new GrantMonthlySearchCreditsJob(db, credits);

        await job.ExecuteAsync();

        Assert.Equal(2, credits.Grants.Count);
        Assert.All(credits.Grants, g => Assert.Equal(GrantMonthlySearchCreditsJob.MonthlyCredits, g.Credits));
        Assert.Contains(credits.Grants, g => g.OrgId == premiumId);
        Assert.DoesNotContain(credits.Grants, g => g.OrgId == basicId);
    }
}
