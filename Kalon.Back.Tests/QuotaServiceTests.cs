using Kalon.Back.Data;
using Kalon.Back.Dtos.Errors;
using Kalon.Back.Models;
using Kalon.Back.Services;
using Microsoft.EntityFrameworkCore;

namespace Kalon.Back.Tests;

public class QuotaServiceTests
{
    private static ApplicationDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task CheckAndIncrementAsync_DoesNothing_WhenLimitIsNull()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var service = new QuotaService(dbContext);

        await service.CheckAndIncrementAsync(Guid.NewGuid(), QuotaTypes.Documents, null, 5);

        Assert.Empty(dbContext.QuotaUsages);
    }

    [Fact]
    public async Task CheckAndIncrementAsync_CreatesQuota_WhenMissing()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var service = new QuotaService(dbContext);
        var organizationId = Guid.NewGuid();

        await service.CheckAndIncrementAsync(organizationId, QuotaTypes.Documents, 10, 3);

        var quota = await dbContext.QuotaUsages.SingleAsync();
        Assert.Equal(organizationId, quota.OrganizationId);
        Assert.Equal(QuotaTypes.Documents, quota.QuotaType);
        Assert.Equal(3, quota.Count);
    }

    [Fact]
    public async Task CheckAndIncrementAsync_Throws_WhenLimitExceeded()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var service = new QuotaService(dbContext);
        var organizationId = Guid.NewGuid();
        var period = QuotaTypes.GetPeriod(QuotaTypes.Emails);

        dbContext.QuotaUsages.Add(new QuotaUsage
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            QuotaType = QuotaTypes.Emails,
            Period = period,
            Count = 4,
            LastUpdatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<QuotaExceededException>(() =>
            service.CheckAndIncrementAsync(organizationId, QuotaTypes.Emails, 5, 2));
    }

    [Fact]
    public async Task GetCurrentCountAsync_ReturnsStoredCount()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var service = new QuotaService(dbContext);
        var organizationId = Guid.NewGuid();
        var period = QuotaTypes.GetPeriod(QuotaTypes.DonorsSearchMonthlyLimit);

        dbContext.QuotaUsages.Add(new QuotaUsage
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            QuotaType = QuotaTypes.DonorsSearchMonthlyLimit,
            Period = period,
            Count = 7,
            LastUpdatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var result = await service.GetCurrentCountAsync(organizationId, QuotaTypes.DonorsSearchMonthlyLimit);

        Assert.Equal(7, result);
    }
}
