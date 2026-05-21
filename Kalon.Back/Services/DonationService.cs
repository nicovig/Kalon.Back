using Kalon.Back.Data;
using Kalon.Back.DTOs;
using Kalon.Back.Models;
using Microsoft.EntityFrameworkCore;

namespace Kalon.Back.Services;

public interface IDonationService
{
    string? ValidatePagination(int page, int pageSize);
    string? ValidateListFilters(string? donationType, decimal? minAmount, decimal? maxAmount);

    Task<DonationListResponse> GetAllPagedAsync(
        Guid organizationId,
        DonationListFilters filters,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<DonationByContactListResponse?> GetAllByContactIdAsync(
        Guid organizationId,
        Guid contactId,
        DonationListFilters filters,
        CancellationToken cancellationToken);

    Task<DonationResponse?> GetByIdAsync(
        Guid organizationId,
        Guid donationId,
        CancellationToken cancellationToken);
}

public sealed record DonationListFilters(
    DateTime? FromDate,
    DateTime? ToDate,
    string? DonationType,
    decimal? MinAmount,
    decimal? MaxAmount);

public class DonationService(ApplicationDbContext dbContext) : IDonationService
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;

    private static readonly HashSet<string> AllowedDonationTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "financial",
        "in_kind",
        "sponsoring"
    };

    public string? ValidatePagination(int page, int pageSize)
    {
        if (page < 1)
            return "page must be >= 1.";
        if (pageSize < 1 || pageSize > MaxPageSize)
            return $"pageSize must be between 1 and {MaxPageSize}.";
        return null;
    }

    public string? ValidateListFilters(string? donationType, decimal? minAmount, decimal? maxAmount)
    {
        if (!string.IsNullOrWhiteSpace(donationType) && !AllowedDonationTypes.Contains(donationType.Trim()))
            return "Invalid donation type filter.";

        if (minAmount.HasValue && maxAmount.HasValue && minAmount.Value > maxAmount.Value)
            return "minAmount cannot be greater than maxAmount.";

        return null;
    }

    public async Task<DonationListResponse> GetAllPagedAsync(
        Guid organizationId,
        DonationListFilters filters,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = ApplyFilters(
            dbContext.Donations.AsNoTracking().Where(d => d.OrganizationId == organizationId),
            filters);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(d => d.Date)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ProjectDonation)
            .ToListAsync(cancellationToken);

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        return new DonationListResponse
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages
        };
    }

    public async Task<DonationByContactListResponse?> GetAllByContactIdAsync(
        Guid organizationId,
        Guid contactId,
        DonationListFilters filters,
        CancellationToken cancellationToken)
    {
        var contactExists = await dbContext.Contacts
            .AsNoTracking()
            .AnyAsync(c => c.OrganizationId == organizationId && c.Id == contactId, cancellationToken);

        if (!contactExists)
            return null;

        var query = ApplyFilters(
            dbContext.Donations.AsNoTracking()
                .Where(d => d.OrganizationId == organizationId && d.ContactId == contactId),
            filters);

        var items = await query
            .OrderByDescending(d => d.Date)
            .Select(ProjectDonation)
            .ToListAsync(cancellationToken);

        return new DonationByContactListResponse
        {
            ContactId = contactId,
            Items = items
        };
    }

    public Task<DonationResponse?> GetByIdAsync(
        Guid organizationId,
        Guid donationId,
        CancellationToken cancellationToken)
    {
        return dbContext.Donations
            .AsNoTracking()
            .Where(d => d.OrganizationId == organizationId && d.Id == donationId)
            .Select(ProjectDonation)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static IQueryable<Donation> ApplyFilters(IQueryable<Donation> query, DonationListFilters filters)
    {
        if (filters.FromDate.HasValue)
            query = query.Where(d => d.Date >= filters.FromDate.Value);

        if (filters.ToDate.HasValue)
            query = query.Where(d => d.Date <= filters.ToDate.Value);

        if (!string.IsNullOrWhiteSpace(filters.DonationType))
        {
            var normalized = filters.DonationType.Trim();
            var canonicalType = AllowedDonationTypes.First(a =>
                string.Equals(a, normalized, StringComparison.OrdinalIgnoreCase));
            query = query.Where(d => d.DonationType == canonicalType);
        }

        if (filters.MinAmount.HasValue)
            query = query.Where(d => d.Amount >= filters.MinAmount.Value);

        if (filters.MaxAmount.HasValue)
            query = query.Where(d => d.Amount <= filters.MaxAmount.Value);

        return query;
    }

    private static readonly System.Linq.Expressions.Expression<Func<Donation, DonationResponse>> ProjectDonation = d =>
        new DonationResponse
        {
            Id = d.Id,
            OrganizationId = d.OrganizationId,
            ContactId = d.ContactId,
            ContactDisplayName = $"{d.Contact.Firstname} {d.Contact.Lastname}".Trim(),
            Amount = d.Amount,
            Date = d.Date,
            DonationType = d.DonationType,
            PaymentMethod = d.PaymentMethod,
            Notes = d.Notes,
            IsAnonymous = d.IsAnonymous,
            CreatedAt = d.CreatedAt,
            UpdatedAt = d.UpdatedAt,
            GeneratedDocument = d.GeneratedDocument == null
                ? null
                : new GeneratedDocumentSummary
                {
                    Id = d.GeneratedDocument.Id,
                    DocumentType = d.GeneratedDocument.DocumentType,
                    OrderNumber = d.GeneratedDocument.OrderNumber,
                    Status = d.GeneratedDocument.Status,
                    PdfPath = d.GeneratedDocument.PdfPath
                }
        };
}
