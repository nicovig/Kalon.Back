using Kalon.Back.Data;
using Kalon.Back.DTOs;
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
    [ProducesResponseType(typeof(Contact), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create([FromQuery] Guid userId, [FromBody] Contact request,
        CancellationToken cancellationToken)
    {
        var access = await userOrganizationAccess.ResolveAsync(userId, cancellationToken);
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

        return CreatedAtAction(nameof(GetById), new { userId, id = contact.Id }, contact);
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<Contact>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAll([FromQuery] Guid userId, CancellationToken cancellationToken)
    {
        var access = await userOrganizationAccess.ResolveAsync(userId, cancellationToken);
        var resolved = access.ToActionResult();
        if (!resolved.Success)
            return resolved.Error!;

        var organizationId = resolved.OrganizationId;

        var contacts = await dbContext.Contacts
            .Include(c => c.Address)
            .Include(c => c.Enterprise)
            .AsNoTracking()
            .Where(c => c.OrganizationId == organizationId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(contacts);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Contact), StatusCodes.Status200OK)]
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

        var contact = await dbContext.Contacts
            .Include(c => c.Address)
            .Include(c => c.Enterprise)
            .AsNoTracking()
            .Where(c => c.OrganizationId == organizationId && c.Id == id)
            .FirstOrDefaultAsync(cancellationToken);

        if (contact is null)
            return NotFound(new ApiMessageResponse { Message = "Contact not found." });

        return Ok(contact);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(Contact), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromQuery] Guid userId, [FromRoute] Guid id,
        [FromBody] Contact request, CancellationToken cancellationToken)
    {
        var access = await userOrganizationAccess.ResolveAsync(userId, cancellationToken);
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

        return Ok(contact);
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
}
