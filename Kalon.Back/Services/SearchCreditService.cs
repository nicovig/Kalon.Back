using Kalon.Back.Data;
using Kalon.Back.Dtos;
using Kalon.Back.Exceptions;
using Kalon.Back.Models;
using Kalon.Back.Services.Mail;
using Microsoft.EntityFrameworkCore;

namespace Kalon.Back.Services;

public interface ISearchCreditService
{
    Task<decimal> GetAvailableCreditsAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task ConsumeAsync(Guid organizationId, string operationType, decimal cost, string? siren = null, CancellationToken cancellationToken = default);
    Task GrantMonthlyCreditsAsync(Guid organizationId, decimal credits, CancellationToken cancellationToken = default);
}


public class SearchCreditService : ISearchCreditService
{
    private readonly ApplicationDbContext _db;
    private readonly IMailService _mail;
    private readonly IConfiguration _config;

    public SearchCreditService(
        ApplicationDbContext db,
        IMailService mail,
        IConfiguration config)
    {
        _db = db;
        _mail = mail;
        _config = config;
    }

    public async Task<decimal> GetAvailableCreditsAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        var lines = await _db.SearchCredits
            .Where(c => c.OrganizationId == organizationId
                        && (c.ExpiresAt == null || c.ExpiresAt > DateTime.UtcNow))
            .ToListAsync(cancellationToken);

        return lines.Sum(c => c.CreditsGranted - c.CreditsUsed);
    }

    public async Task ConsumeAsync(
        Guid organizationId,
        string operationType,
        decimal cost,
        string? siren = null,
        CancellationToken cancellationToken = default)
    {
        var available = await GetAvailableCreditsAsync(organizationId, cancellationToken);
        if (available < cost)
            throw new InsufficientSearchCreditsException(available, cost);

        var lines = await _db.SearchCredits
            .Where(c => c.OrganizationId == organizationId
                        && c.CreditsUsed < c.CreditsGranted
                        && (c.ExpiresAt == null || c.ExpiresAt > DateTime.UtcNow))
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        var remaining = cost;

        foreach (var line in lines)
        {
            if (remaining <= 0) break;

            var availableOnLine = line.CreditsGranted - line.CreditsUsed;
            var consume = Math.Min(remaining, availableOnLine);

            line.CreditsUsed += consume;
            remaining -= consume;

            _db.SearchCreditLogs.Add(new SearchCreditLog
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                SearchCreditId = line.Id,
                OperationType = operationType,
                CreditsConsumed = consume,
                Siren = siren,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        await CheckGlobalAlertAsync(cancellationToken);
    }

    public async Task GrantMonthlyCreditsAsync(Guid organizationId, decimal credits, CancellationToken cancellationToken = default)
    {
        _db.SearchCredits.Add(new SearchCredit
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Source = SearchCreditSources.MonthlyGrant,
            CreditsGranted = credits,
            CreditsUsed = 0,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = null
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task CheckGlobalAlertAsync(CancellationToken cancellationToken)
    {
        var alertEmail = _config["Admin:AlertEmail"];
        if (string.IsNullOrWhiteSpace(alertEmail))
            return;

        var totalConsumed = await _db.SearchCreditLogs
            .SumAsync(l => (decimal?)l.CreditsConsumed, cancellationToken) ?? 0m;

        var totalPurchased = _config.GetValue<decimal>("SocieteCom:TotalCreditsPurchased");
        var remaining = totalPurchased - totalConsumed;
        var threshold = _config.GetValue("SocieteCom:AlertThreshold", 100m);

        if (remaining > threshold)
            return;

        await _mail.SendAsync(new MailMessageDto
        {
            ToEmail = alertEmail,
            ToName = "Nicolas",
            Subject = $"Kalon — Societe.com : {remaining} credits restants",
            BodyHtml = $"<p>Il reste <strong>{remaining} credits</strong> sur ton compte Societe.com.</p><p>Recharge des que possible.</p>",
            SenderEmail = _config["Brevo:DefaultSenderEmail"],
            SenderName = "Kalon Alertes"
        });
    }
}
