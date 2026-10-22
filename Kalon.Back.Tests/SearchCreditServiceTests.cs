using Kalon.Back.Data;
using Kalon.Back.Dtos;
using Kalon.Back.Exceptions;
using Kalon.Back.Models;
using Kalon.Back.Services;
using Kalon.Back.Services.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Kalon.Back.Tests;

public class SearchCreditServiceTests
{
    private sealed class FakeMailService : IMailService
    {
        public List<MailMessageDto> Sent { get; } = [];

        public Task SendAsync(MailMessageDto message)
        {
            Sent.Add(message);
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

    private static IConfiguration CreateConfig(decimal totalPurchased = 5000, decimal threshold = 100)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SocieteCom:TotalCreditsPurchased"] = totalPurchased.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["SocieteCom:AlertThreshold"] = threshold.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Admin:AlertEmail"] = "nicolas@kalon-app.fr",
                ["Brevo:DefaultSenderEmail"] = "noreply@kalon-app.fr"
            })
            .Build();
    }

    private static Organization CreateOrganization(Guid id) => new()
    {
        Id = id,
        Name = "Org",
        Email = "org@test.local",
        UserId = Guid.NewGuid(),
        RNA = "W1",
        SIRET = "1",
        FiscalStatus = FiscalStatus.GeneralInterest,
        DefaultReceiptFrequency = ReceiptFrequency.Annually,
        CreatedAt = DateTime.UtcNow,
        CachedPlan = PlanTypes.Premium
    };

    [Fact]
    public async Task GetAvailableCreditsAsync_SumsNonExpiredLines()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var orgId = Guid.NewGuid();
        db.Organizations.Add(CreateOrganization(orgId));
        db.SearchCredits.AddRange(
            new SearchCredit
            {
                Id = Guid.NewGuid(),
                OrganizationId = orgId,
                Source = SearchCreditSources.MonthlyGrant,
                CreditsGranted = 10,
                CreditsUsed = 3,
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            },
            new SearchCredit
            {
                Id = Guid.NewGuid(),
                OrganizationId = orgId,
                Source = SearchCreditSources.Purchased,
                CreditsGranted = 5,
                CreditsUsed = 0,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                ExpiresAt = DateTime.UtcNow.AddDays(-1)
            });
        await db.SaveChangesAsync();

        var service = new SearchCreditService(db, new FakeMailService(), CreateConfig());
        var available = await service.GetAvailableCreditsAsync(orgId);

        Assert.Equal(7m, available);
    }

    [Fact]
    public async Task ConsumeAsync_UsesFifoAndWritesLogs()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var orgId = Guid.NewGuid();
        db.Organizations.Add(CreateOrganization(orgId));
        var older = Guid.NewGuid();
        var newer = Guid.NewGuid();
        db.SearchCredits.AddRange(
            new SearchCredit
            {
                Id = older,
                OrganizationId = orgId,
                Source = SearchCreditSources.MonthlyGrant,
                CreditsGranted = 1,
                CreditsUsed = 0,
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            },
            new SearchCredit
            {
                Id = newer,
                OrganizationId = orgId,
                Source = SearchCreditSources.Purchased,
                CreditsGranted = 5,
                CreditsUsed = 0,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            });
        await db.SaveChangesAsync();

        var service = new SearchCreditService(db, new FakeMailService(), CreateConfig());
        await service.ConsumeAsync(orgId, SearchOperationTypes.Search, 1.5m, "123456789");

        var olderLine = await db.SearchCredits.SingleAsync(c => c.Id == older);
        var newerLine = await db.SearchCredits.SingleAsync(c => c.Id == newer);
        Assert.Equal(1m, olderLine.CreditsUsed);
        Assert.Equal(0.5m, newerLine.CreditsUsed);

        var logs = await db.SearchCreditLogs.OrderBy(l => l.CreatedAt).ToListAsync();
        Assert.Equal(2, logs.Count);
        Assert.Equal(older, logs[0].SearchCreditId);
        Assert.Equal(1m, logs[0].CreditsConsumed);
        Assert.Equal(newer, logs[1].SearchCreditId);
        Assert.Equal(0.5m, logs[1].CreditsConsumed);
        Assert.Equal("123456789", logs[0].Siren);
    }

    [Fact]
    public async Task ConsumeAsync_Throws_WhenCreditsInsufficient()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var orgId = Guid.NewGuid();
        db.Organizations.Add(CreateOrganization(orgId));
        db.SearchCredits.Add(new SearchCredit
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            Source = SearchCreditSources.MonthlyGrant,
            CreditsGranted = 0.05m,
            CreditsUsed = 0,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new SearchCreditService(db, new FakeMailService(), CreateConfig());

        var ex = await Assert.ThrowsAsync<InsufficientSearchCreditsException>(() =>
            service.ConsumeAsync(orgId, SearchOperationTypes.Search, SearchOperationTypes.SearchCost));

        Assert.Equal(0.05m, ex.Available);
        Assert.Equal(SearchOperationTypes.SearchCost, ex.Required);
    }

    [Fact]
    public async Task GrantMonthlyCreditsAsync_CreatesMonthlyGrantLine()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var orgId = Guid.NewGuid();
        db.Organizations.Add(CreateOrganization(orgId));
        await db.SaveChangesAsync();

        var service = new SearchCreditService(db, new FakeMailService(), CreateConfig());
        await service.GrantMonthlyCreditsAsync(orgId, 20);

        var line = await db.SearchCredits.SingleAsync();
        Assert.Equal(SearchCreditSources.MonthlyGrant, line.Source);
        Assert.Equal(20m, line.CreditsGranted);
        Assert.Equal(0m, line.CreditsUsed);
        Assert.Null(line.ExpiresAt);
    }

    [Fact]
    public async Task ConsumeAsync_SendsAlert_WhenGlobalStockBelowThreshold()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var orgId = Guid.NewGuid();
        db.Organizations.Add(CreateOrganization(orgId));
        db.SearchCredits.Add(new SearchCredit
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            Source = SearchCreditSources.Purchased,
            CreditsGranted = 10,
            CreditsUsed = 0,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var mail = new FakeMailService();
        var service = new SearchCreditService(db, mail, CreateConfig(totalPurchased: 1, threshold: 1));
        await service.ConsumeAsync(orgId, SearchOperationTypes.Search, SearchOperationTypes.SearchCost);

        Assert.Single(mail.Sent);
        Assert.Contains("credits restants", mail.Sent[0].Subject);
    }
}
