using Kalon.Back.Data;
using Kalon.Back.DTOs;
using Kalon.Back.Models;
using Kalon.Back.Services;
using Kalon.Back.Services.OrganizationAccess;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kalon.Back.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "organization_master")]
public class DonationController(
    ApplicationDbContext dbContext,
    IUserOrganizationAccessService userOrganizationAccess,
    IDonationService donationService)
    : ControllerBase
{
    private const int MaxBulkCreateItems = 500;

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
    public async Task<IActionResult> Create([FromBody] DonationCreateRequest request,
        CancellationToken cancellationToken)
    {
        var userId = ResolveUserIdFromJwt();
        if (userId is null)
            return BadRequest(new ApiMessageResponse { Message = "userId is required." });

        var access = await userOrganizationAccess.ResolveAsync(userId.Value, cancellationToken);
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

        var details = await donationService.GetByIdAsync(organizationId, donation.Id, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = donation.Id }, details);
    }

    [HttpPost("bulk")]
    [ProducesResponseType(typeof(DonationBulkCreateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateBulk([FromBody] DonationBulkCreateRequest request,
        CancellationToken cancellationToken)
    {
        var userId = ResolveUserIdFromJwt();
        if (userId is null)
            return BadRequest(new ApiMessageResponse { Message = "userId is required." });

        var access = await userOrganizationAccess.ResolveAsync(userId.Value, cancellationToken);
        var resolved = access.ToActionResult();
        if (!resolved.Success)
            return resolved.Error!;

        var organizationId = resolved.OrganizationId;

        if (request.Items.Count == 0)
            return BadRequest(new ApiMessageResponse { Message = "Items is required." });

        if (request.Items.Count > MaxBulkCreateItems)
            return BadRequest(new ApiMessageResponse { Message = $"Maximum {MaxBulkCreateItems} donations per request." });

        for (var i = 0; i < request.Items.Count; i++)
        {
            var item = request.Items[i];
            var validationError = ValidateRequest(item);
            if (validationError is not null)
                return BadRequest(new ApiMessageResponse { Message = $"Item {i}: {validationError}" });
        }

        var distinctContactIds = request.Items
            .Select(i => i.ContactId)
            .Distinct()
            .ToList();

        var validContactsCount = await dbContext.Contacts
            .AsNoTracking()
            .Where(c => c.OrganizationId == organizationId && distinctContactIds.Contains(c.Id))
            .Select(c => c.Id)
            .Distinct()
            .CountAsync(cancellationToken);

        if (validContactsCount != distinctContactIds.Count)
            return BadRequest(new ApiMessageResponse { Message = "One or more contacts were not found for organization." });

        var createdIds = new List<Guid>(request.Items.Count);
        foreach (var item in request.Items)
        {
            var donation = new Donation
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                CreatedAt = DateTime.UtcNow
            };
            ApplyRequest(donation, item);
            createdIds.Add(donation.Id);
            dbContext.Donations.Add(donation);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new DonationBulkCreateResponse
        {
            CreatedCount = createdIds.Count,
            CreatedIds = createdIds
        });
    }

    [HttpGet]
    [ProducesResponseType(typeof(DonationListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAll(
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] string? donationType,
        [FromQuery] decimal? minAmount,
        [FromQuery] decimal? maxAmount,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DonationService.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var userId = ResolveUserIdFromJwt();
        if (userId is null)
            return BadRequest(new ApiMessageResponse { Message = "userId is required." });

        var access = await userOrganizationAccess.ResolveAsync(userId.Value, cancellationToken);
        var resolved = access.ToActionResult();
        if (!resolved.Success)
            return resolved.Error!;

        var organizationId = resolved.OrganizationId;

        var paginationError = donationService.ValidatePagination(page, pageSize);
        if (paginationError is not null)
            return BadRequest(new ApiMessageResponse { Message = paginationError });

        var filterError = donationService.ValidateListFilters(donationType, minAmount, maxAmount);
        if (filterError is not null)
            return BadRequest(new ApiMessageResponse { Message = filterError });

        var result = await donationService.GetAllPagedAsync(
            organizationId,
            new DonationListFilters(fromDate, toDate, donationType, minAmount, maxAmount),
            page,
            pageSize,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("contact/{contactId:guid}")]
    [ProducesResponseType(typeof(DonationByContactListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAllByContactId(
        [FromRoute] Guid contactId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] string? donationType,
        [FromQuery] decimal? minAmount,
        [FromQuery] decimal? maxAmount,
        CancellationToken cancellationToken = default)
    {
        var userId = ResolveUserIdFromJwt();
        if (userId is null)
            return BadRequest(new ApiMessageResponse { Message = "userId is required." });

        var access = await userOrganizationAccess.ResolveAsync(userId.Value, cancellationToken);
        var resolved = access.ToActionResult();
        if (!resolved.Success)
            return resolved.Error!;

        var organizationId = resolved.OrganizationId;

        if (contactId == Guid.Empty)
            return BadRequest(new ApiMessageResponse { Message = "contactId is required." });

        var filterError = donationService.ValidateListFilters(donationType, minAmount, maxAmount);
        if (filterError is not null)
            return BadRequest(new ApiMessageResponse { Message = filterError });

        var result = await donationService.GetAllByContactIdAsync(
            organizationId,
            contactId,
            new DonationListFilters(fromDate, toDate, donationType, minAmount, maxAmount),
            cancellationToken);

        if (result is null)
            return NotFound(new ApiMessageResponse { Message = "Contact not found." });

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DonationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var userId = ResolveUserIdFromJwt();
        if (userId is null)
            return BadRequest(new ApiMessageResponse { Message = "userId is required." });

        var access = await userOrganizationAccess.ResolveAsync(userId.Value, cancellationToken);
        var resolved = access.ToActionResult();
        if (!resolved.Success)
            return resolved.Error!;

        var organizationId = resolved.OrganizationId;

        var donation = await donationService.GetByIdAsync(organizationId, id, cancellationToken);
        if (donation is null)
            return NotFound(new ApiMessageResponse { Message = "Donation not found." });

        return Ok(donation);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(DonationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromRoute] Guid id,
        [FromBody] DonationCreateRequest request, CancellationToken cancellationToken)
    {
        var userId = ResolveUserIdFromJwt();
        if (userId is null)
            return BadRequest(new ApiMessageResponse { Message = "userId is required." });

        var access = await userOrganizationAccess.ResolveAsync(userId.Value, cancellationToken);
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

        var details = await donationService.GetByIdAsync(organizationId, donation.Id, cancellationToken);
        return Ok(details);
    }

    private static string? ValidateRequest(DonationCreateRequest request)
    {
        if (request.ContactId == Guid.Empty)
            return "ContactId is required.";
        if (request.Amount < 0)
            return "Amount cannot be negative.";
        if (string.IsNullOrWhiteSpace(request.DonationType))
            return "DonationType is required.";
        if (!AllowedDonationTypes.Contains(request.DonationType.Trim()))
            return "Invalid donation type.";
        return null;
    }

    private async Task<string?> ValidateRequestAsync(Guid organizationId, DonationCreateRequest request,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateRequest(request);
        if (validationError is not null)
            return validationError;

        var contactExists = await dbContext.Contacts
            .AsNoTracking()
            .AnyAsync(c => c.OrganizationId == organizationId && c.Id == request.ContactId, cancellationToken);

        if (!contactExists)
            return "Contact not found for organization.";

        return null;
    }

    private static void ApplyRequest(Donation donation, DonationCreateRequest request)
    {
        donation.ContactId = request.ContactId;
        donation.Amount = request.Amount;
        donation.Date = request.Date;
        donation.DonationType = request.DonationType.Trim();
        donation.PaymentMethod = request.PaymentMethod?.Trim();
        donation.Notes = request.Notes?.Trim();
        donation.IsAnonymous = request.IsAnonymous;
    }

    private Guid? ResolveUserIdFromJwt()
    {
        var principal = HttpContext?.User;
        if (principal is null)
            return null;

        var claimValue = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? principal.FindFirstValue("sub");
        return Guid.TryParse(claimValue, out var parsed) ? parsed : null;
    }
}
