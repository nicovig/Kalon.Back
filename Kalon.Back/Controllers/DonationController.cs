using Kalon.Back.Data;
using Kalon.Back.DTOs;
using Kalon.Back.Models;
using Kalon.Back.Services.OrganizationAccess;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kalon.Back.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DonationController(ApplicationDbContext dbContext, IUserOrganizationAccessService userOrganizationAccess)
    : ControllerBase
{
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 200;

    private static readonly HashSet<string> AllowedDonationTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "financial",
        "in_kind",
        "sponsoring"
    };

    [HttpPost]
    [ProducesResponseType(typeof(DonationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create([FromQuery] Guid userId, [FromBody] Donation request,
        CancellationToken cancellationToken)
    {
        var access = await userOrganizationAccess.ResolveAsync(userId, cancellationToken);
        var resolved = access.ToActionResult();
        if (!resolved.Success)
            return resolved.Error!;

        var organizationId = resolved.OrganizationId;

        var validationError = await ValidateRequestAsync(organizationId, request, cancellationToken);
        if (validationError is not null)
            return BadRequest(new ApiMessageResponse { Message = validationError });

        var donation = new Donation
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            CreatedAt = DateTime.UtcNow
        };
        ApplyRequest(donation, request);

        dbContext.Donations.Add(donation);
        await dbContext.SaveChangesAsync(cancellationToken);

        var details = await ProjectDonationAsync(donation.Id, organizationId, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { userId, id = donation.Id }, details);
    }

    [HttpGet]
    [ProducesResponseType(typeof(DonationListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid userId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] string? donationType,
        [FromQuery] Guid? contactId,
        [FromQuery] decimal? minAmount,
        [FromQuery] decimal? maxAmount,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var access = await userOrganizationAccess.ResolveAsync(userId, cancellationToken);
        var resolved = access.ToActionResult();
        if (!resolved.Success)
            return resolved.Error!;

        var organizationId = resolved.OrganizationId;

        var paginationError = ValidatePagination(page, pageSize);
        if (paginationError is not null)
            return BadRequest(new ApiMessageResponse { Message = paginationError });

        if (!string.IsNullOrWhiteSpace(donationType) && !AllowedDonationTypes.Contains(donationType.Trim()))
            return BadRequest(new ApiMessageResponse { Message = "Invalid donation type filter." });

        if (minAmount.HasValue && maxAmount.HasValue && minAmount.Value > maxAmount.Value)
            return BadRequest(new ApiMessageResponse { Message = "minAmount cannot be greater than maxAmount." });

        if (contactId.HasValue && contactId.Value == Guid.Empty)
            return BadRequest(new ApiMessageResponse { Message = "contactId cannot be empty when provided." });

        var query = dbContext.Donations
            .AsNoTracking()
            .Where(d => d.OrganizationId == organizationId);

        if (fromDate.HasValue)
            query = query.Where(d => d.Date >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(d => d.Date <= toDate.Value);

        if (!string.IsNullOrWhiteSpace(donationType))
        {
            var normalized = donationType.Trim();
            var canonicalType = AllowedDonationTypes.First(a =>
                string.Equals(a, normalized, StringComparison.OrdinalIgnoreCase));
            query = query.Where(d => d.DonationType == canonicalType);
        }

        if (contactId.HasValue)
            query = query.Where(d => d.ContactId == contactId.Value);

        if (minAmount.HasValue)
            query = query.Where(d => d.Amount >= minAmount.Value);

        if (maxAmount.HasValue)
            query = query.Where(d => d.Amount <= maxAmount.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(d => d.Date)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new DonationResponse
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
            })
            .ToListAsync(cancellationToken);

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        return Ok(new DonationListResponse
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages
        });
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DonationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromQuery] Guid userId, [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var access = await userOrganizationAccess.ResolveAsync(userId, cancellationToken);
        var resolved = access.ToActionResult();
        if (!resolved.Success)
            return resolved.Error!;

        var organizationId = resolved.OrganizationId;

        var donation = await ProjectDonationAsync(id, organizationId, cancellationToken);
        if (donation is null)
            return NotFound(new ApiMessageResponse { Message = "Donation not found." });

        return Ok(donation);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(DonationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromQuery] Guid userId, [FromRoute] Guid id,
        [FromBody] Donation request, CancellationToken cancellationToken)
    {
        var access = await userOrganizationAccess.ResolveAsync(userId, cancellationToken);
        var resolved = access.ToActionResult();
        if (!resolved.Success)
            return resolved.Error!;

        var organizationId = resolved.OrganizationId;

        var validationError = await ValidateRequestAsync(organizationId, request, cancellationToken);
        if (validationError is not null)
            return BadRequest(new ApiMessageResponse { Message = validationError });

        var donation = await dbContext.Donations
            .FirstOrDefaultAsync(d => d.OrganizationId == organizationId && d.Id == id, cancellationToken);

        if (donation is null)
            return NotFound(new ApiMessageResponse { Message = "Donation not found." });

        ApplyRequest(donation, request);
        donation.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        var details = await ProjectDonationAsync(donation.Id, organizationId, cancellationToken);
        return Ok(details);
    }

    private static string? ValidatePagination(int page, int pageSize)
    {
        if (page < 1)
            return "page must be >= 1.";
        if (pageSize < 1 || pageSize > MaxPageSize)
            return $"pageSize must be between 1 and {MaxPageSize}.";
        return null;
    }

    private async Task<string?> ValidateRequestAsync(Guid organizationId, Donation request,
        CancellationToken cancellationToken)
    {
        if (request.ContactId == Guid.Empty)
            return "ContactId is required.";
        if (request.Amount < 0)
            return "Amount cannot be negative.";
        if (string.IsNullOrWhiteSpace(request.DonationType))
            return "DonationType is required.";
        if (!AllowedDonationTypes.Contains(request.DonationType.Trim()))
            return "Invalid donation type.";

        var contactExists = await dbContext.Contacts
            .AsNoTracking()
            .AnyAsync(c => c.OrganizationId == organizationId && c.Id == request.ContactId, cancellationToken);

        if (!contactExists)
            return "Contact not found for organization.";

        return null;
    }

    private static void ApplyRequest(Donation donation, Donation request)
    {
        donation.ContactId = request.ContactId;
        donation.Amount = request.Amount;
        donation.Date = request.Date;
        donation.DonationType = request.DonationType.Trim();
        donation.PaymentMethod = request.PaymentMethod?.Trim();
        donation.Notes = request.Notes?.Trim();
        donation.IsAnonymous = request.IsAnonymous;
    }

    private Task<DonationResponse?> ProjectDonationAsync(Guid donationId, Guid organizationId,
        CancellationToken cancellationToken)
    {
        return dbContext.Donations
            .AsNoTracking()
            .Where(d => d.OrganizationId == organizationId && d.Id == donationId)
            .Select(d => new DonationResponse
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
            })
            .FirstOrDefaultAsync(cancellationToken);
    }
}
