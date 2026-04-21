using Kalon.Back.Data;
using Kalon.Back.DTOs;
using Kalon.Back.Models;
using Kalon.Back.Services.OrganizationAccess;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kalon.Back.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "organization_master")]
public class ContactController(ApplicationDbContext dbContext, IUserOrganizationAccessService userOrganizationAccess)
    : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(ContactResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create([FromBody] Contact request,
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

        var validationError = ValidateRequest(request);
        if (validationError is not null)
            return BadRequest(new ApiMessageResponse { Message = validationError });

        var contact = new Contact
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            CreatedAt = DateTime.UtcNow
        };
        ApplyRequest(contact, request);

        dbContext.Contacts.Add(contact);
        await dbContext.SaveChangesAsync(cancellationToken);

        var result = await ProjectContacts()
            .Where(c => c.OrganizationId == organizationId && c.Id == contact.Id)
            .FirstAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = contact.Id }, result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<ContactResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var userId = ResolveUserIdFromJwt();
        if (userId is null)
            return BadRequest(new ApiMessageResponse { Message = "userId is required." });

        var access = await userOrganizationAccess.ResolveAsync(userId.Value, cancellationToken);
        var resolved = access.ToActionResult();
        if (!resolved.Success)
            return resolved.Error!;

        var organizationId = resolved.OrganizationId;

        var contacts = await ProjectContacts()
            .Where(c => c.OrganizationId == organizationId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(contacts);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ContactResponse), StatusCodes.Status200OK)]
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

        var contact = await ProjectContacts()
            .Where(c => c.OrganizationId == organizationId && c.Id == id)
            .FirstOrDefaultAsync(cancellationToken);

        if (contact is null)
            return NotFound(new ApiMessageResponse { Message = "Contact not found." });

        return Ok(contact);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ContactResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromRoute] Guid id,
        [FromBody] Contact request, CancellationToken cancellationToken)
    {
        var userId = ResolveUserIdFromJwt();
        if (userId is null)
            return BadRequest(new ApiMessageResponse { Message = "userId is required." });

        var access = await userOrganizationAccess.ResolveAsync(userId.Value, cancellationToken);
        var resolved = access.ToActionResult();
        if (!resolved.Success)
            return resolved.Error!;

        var organizationId = resolved.OrganizationId;

        var validationError = ValidateRequest(request);
        if (validationError is not null)
            return BadRequest(new ApiMessageResponse { Message = validationError });

        var contact = await dbContext.Contacts
            .Include(c => c.Address)
            .Include(c => c.Enterprise)
            .FirstOrDefaultAsync(c => c.OrganizationId == organizationId && c.Id == id, cancellationToken);

        if (contact is null)
            return NotFound(new ApiMessageResponse { Message = "Contact not found." });

        ApplyRequest(contact, request);
        contact.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        var result = await ProjectContacts()
            .Where(c => c.OrganizationId == organizationId && c.Id == contact.Id)
            .FirstAsync(cancellationToken);

        return Ok(result);
    }

    private static string? ValidateRequest(Contact request)
    {
        if (string.IsNullOrWhiteSpace(request.Firstname) || string.IsNullOrWhiteSpace(request.Lastname))
            return "Firstname and lastname are required.";
        if (!ContactKinds.IsValid(request.Kind))
            return "Invalid contact kind.";
        return null;
    }

    private static void ApplyRequest(Contact contact, Contact request)
    {
        contact.Kind = request.Kind.Trim();
        contact.Firstname = request.Firstname.Trim();
        contact.Lastname = request.Lastname.Trim();
        contact.Email = request.Email?.Trim();
        contact.Phone = request.Phone?.Trim();
        contact.JobTitle = request.JobTitle?.Trim();
        contact.BirthDate = request.BirthDate;
        contact.Gender = request.Gender?.Trim();
        contact.Notes = request.Notes?.Trim();
        contact.Department = request.Department?.Trim();
        contact.PreferredFrequencySendingReceipt = request.PreferredFrequencySendingReceipt?.Trim();
        contact.Address = request.Address;
        contact.Enterprise = request.Enterprise;
    }

    private IQueryable<ContactResponse> ProjectContacts()
    {
        return dbContext.Contacts
            .AsNoTracking()
            .Select(c => new ContactResponse
            {
                Id = c.Id,
                OrganizationId = c.OrganizationId,
                Kind = c.Kind,
                IsOut = c.IsOut,
                Firstname = c.Firstname,
                Lastname = c.Lastname,
                Email = c.Email,
                Phone = c.Phone,
                JobTitle = c.JobTitle,
                BirthDate = c.BirthDate,
                Gender = c.Gender,
                Notes = c.Notes,
                Department = c.Department,
                PreferredFrequencySendingReceipt = c.PreferredFrequencySendingReceipt,
                Address = c.Address == null
                    ? null
                    : new ContactAddress
                    {
                        Street = c.Address.Street,
                        PostalCode = c.Address.PostalCode,
                        City = c.Address.City,
                        Country = c.Address.Country,
                        Phone = c.Address.Phone,
                        Email = c.Address.Email
                    },
                Enterprise = c.Enterprise == null
                    ? null
                    : new ContactEnterprise
                    {
                        Name = c.Enterprise.Name,
                        Siret = c.Enterprise.Siret,
                        SupportKind = c.Enterprise.SupportKind,
                        Street = c.Enterprise.Street,
                        PostalCode = c.Enterprise.PostalCode,
                        City = c.Enterprise.City,
                        Country = c.Enterprise.Country,
                        ContactFirstname = c.Enterprise.ContactFirstname,
                        ContactLastname = c.Enterprise.ContactLastname,
                        ContactEmail = c.Enterprise.ContactEmail,
                        ContactPhone = c.Enterprise.ContactPhone
                    },
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt,
                TotalDonation = c.Donations.Sum(d => (decimal?)d.Amount) ?? 0m,
                FirstDonationAt = c.Donations.Min(d => (DateTime?)d.Date),
                LastDonation = c.Donations.Max(d => (DateTime?)d.Date),
                LastDonationAmount = c.Donations
                    .OrderByDescending(d => d.Date)
                    .ThenByDescending(d => d.CreatedAt)
                    .Select(d => (decimal?)d.Amount)
                    .FirstOrDefault(),
                AverageDonationAmount = c.Donations.Average(d => (decimal?)d.Amount) ?? 0m,
                DonationCount = c.Donations.Count()
            });
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
