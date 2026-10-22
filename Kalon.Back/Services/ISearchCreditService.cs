namespace Kalon.Back.Services;

public interface ISearchCreditService
{
    Task<decimal> GetAvailableCreditsAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task ConsumeAsync(Guid organizationId, string operationType, decimal cost, string? siren = null, CancellationToken cancellationToken = default);
    Task GrantMonthlyCreditsAsync(Guid organizationId, decimal credits, CancellationToken cancellationToken = default);
}
