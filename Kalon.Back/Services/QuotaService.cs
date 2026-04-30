using Kalon.Back.Data;
using Kalon.Back.Dtos.Errors;
using Kalon.Back.Models;
using Microsoft.EntityFrameworkCore;

namespace Kalon.Back.Services;

public interface IQuotaService
{
    Task CheckAndIncrementAsync(
        Guid organizationId,
        string quotaType,
        int? limit,
        int increment = 1);

    Task<int> GetCurrentCountAsync(
        Guid organizationId,
        string quotaType);
}

public class QuotaService : IQuotaService
{
    private readonly ApplicationDbContext _db;

    public QuotaService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task CheckAndIncrementAsync(
        Guid organizationId,
        string quotaType,
        int? limit,
        int increment = 1)
    {
        // null = illimité → on ne vérifie rien
        if (!limit.HasValue) return;

        var period = QuotaTypes.GetPeriod(quotaType);

        var quota = await _db.QuotaUsages
            .FirstOrDefaultAsync(q =>
                q.OrganizationId == organizationId
                && q.QuotaType == quotaType
                && q.Period == period);

        var currentCount = quota?.Count ?? 0;

        if (currentCount + increment > limit.Value)
            throw new QuotaExceededException(
                quotaType, currentCount, limit.Value);

        if (quota is null)
        {
            _db.QuotaUsages.Add(new QuotaUsage
            {
                OrganizationId = organizationId,
                QuotaType = quotaType,
                Period = period,
                Count = increment,
                LastUpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            quota.Count += increment;
            quota.LastUpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
    }

    public async Task CheckAndIncrementDecimalAsync(
        Guid organizationId,
        string quotaType,
        int? monthlyLimit,        // nb d'appels autorisés par mois
        decimal creditCost)       // coût en crédits Societe.com
    {
        // pour la recherche on limite le nombre d'appels, pas les crédits
        // monthlyLimit = 10 appels/mois inclus Premium
        await CheckAndIncrementAsync(
            organizationId,
            quotaType,
            monthlyLimit,
            increment: 1);  // 1 appel = 1 unité de quota
    }

    public async Task<int> GetCurrentCountAsync(
        Guid organizationId, string quotaType)
    {
        var period = QuotaTypes.GetPeriod(quotaType);

        return await _db.QuotaUsages
            .Where(q =>
                q.OrganizationId == organizationId
                && q.QuotaType == quotaType
                && q.Period == period)
            .Select(q => q.Count)
            .FirstOrDefaultAsync();
    }
}