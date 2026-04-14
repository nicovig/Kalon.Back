using Kalon.Back.Data;
using Kalon.Back.Dtos.Contact;
using Kalon.Back.Models;
using Kalon.Back.Services.OrganizationAccess;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kalon.Back.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ContactController(ApplicationDbContext dbContext, IUserOrganizationAccessService userOrganizationAccess)
    : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromQuery] Guid userId, [FromBody] ContactUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var access = await userOrganizationAccess.ResolveAsync(userId, cancellationToken);
        var resolved = access.ToActionResult();
        if (!resolved.Success)
            return resolved.Error!;

        var organizationId = resolved.OrganizationId;

        var validationError = ValidateRequest(request);
        if (validationError is not null)
            return BadRequest(new { message = validationError });

        var contact = new Contact
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            CreatedAt = DateTime.UtcNow
        };
        ApplyRequest(contact, request);

        dbContext.Contacts.Add(contact);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { userId, id = contact.Id }, ToDetailsResponse(contact));
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid userId, CancellationToken cancellationToken)
    {
        var access = await userOrganizationAccess.ResolveAsync(userId, cancellationToken);
        var resolved = access.ToActionResult();
        if (!resolved.Success)
            return resolved.Error!;

        var organizationId = resolved.OrganizationId;

        var contacts = await dbContext.Contacts
            .AsNoTracking()
            .Where(c => c.OrganizationId == organizationId)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new ContactListItemResponse
            {
                Id = c.Id,
                Kind = c.Kind,
                Status = c.Status,
                Firstname = c.Firstname,
                Lastname = c.Lastname,
                Email = c.Email,
                Phone = c.Phone,
                Department = c.Department,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(contacts);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById([FromQuery] Guid userId, [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var access = await userOrganizationAccess.ResolveAsync(userId, cancellationToken);
        var resolved = access.ToActionResult();
        if (!resolved.Success)
            return resolved.Error!;

        var organizationId = resolved.OrganizationId;

        var contact = await dbContext.Contacts
            .AsNoTracking()
            .Where(c => c.OrganizationId == organizationId && c.Id == id)
            .Select(c => new ContactDetailsResponse
            {
                Id = c.Id,
                Kind = c.Kind,
                Status = c.Status,
                Firstname = c.Firstname,
                Lastname = c.Lastname,
                Email = c.Email,
                Phone = c.Phone,
                Department = c.Department,
                CreatedAt = c.CreatedAt,
                JobTitle = c.JobTitle,
                BirthDate = c.BirthDate,
                Gender = c.Gender,
                Notes = c.Notes,
                PreferredFrequencySendingReceipt = c.PreferredFrequencySendingReceipt,
                UpdatedAt = c.UpdatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (contact is null)
            return NotFound(new { message = "Contact not found." });

        return Ok(contact);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update([FromQuery] Guid userId, [FromRoute] Guid id,
        [FromBody] ContactUpsertRequest request, CancellationToken cancellationToken)
    {
        var access = await userOrganizationAccess.ResolveAsync(userId, cancellationToken);
        var resolved = access.ToActionResult();
        if (!resolved.Success)
            return resolved.Error!;

        var organizationId = resolved.OrganizationId;

        var validationError = ValidateRequest(request);
        if (validationError is not null)
            return BadRequest(new { message = validationError });

        var contact = await dbContext.Contacts
            .FirstOrDefaultAsync(c => c.OrganizationId == organizationId && c.Id == id, cancellationToken);

        if (contact is null)
            return NotFound(new { message = "Contact not found." });

        ApplyRequest(contact, request);
        contact.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToDetailsResponse(contact));
    }

    private static string? ValidateRequest(ContactUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Firstname) || string.IsNullOrWhiteSpace(request.Lastname))
            return "Firstname and lastname are required.";
        if (!ContactKinds.IsValid(request.Kind))
            return "Invalid contact kind.";
        if (!ContactStatuses.IsValid(request.Status))
            return "Invalid contact status.";
        return null;
    }

    private static void ApplyRequest(Contact contact, ContactUpsertRequest request)
    {
        contact.Kind = request.Kind.Trim();
        contact.Status = request.Status.Trim();
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
    }

    private static ContactDetailsResponse ToDetailsResponse(Contact c) => new()
    {
        Id = c.Id,
        Kind = c.Kind,
        Status = c.Status,
        Firstname = c.Firstname,
        Lastname = c.Lastname,
        Email = c.Email,
        Phone = c.Phone,
        Department = c.Department,
        CreatedAt = c.CreatedAt,
        JobTitle = c.JobTitle,
        BirthDate = c.BirthDate,
        Gender = c.Gender,
        Notes = c.Notes,
        PreferredFrequencySendingReceipt = c.PreferredFrequencySendingReceipt,
        UpdatedAt = c.UpdatedAt
    };
}
