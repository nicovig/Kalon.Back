using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Kalon.Back.Data;
using Kalon.Back.Models;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "organization_master")]
public class OrganizationController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public OrganizationController(ApplicationDbContext db)
    {
        _db = db;
    }

    // GET api/organization
    // retourne l'organisation de l'utilisateur connecté
    [HttpGet]
    public async Task<ActionResult<Organization>> Get()
    {
        var orgId = GetOrganizationId();
        var org = await _db.Organizations
            .Include(o => o.Logo)
            .Include(o => o.ContactStatusSettings)
            .FirstOrDefaultAsync(o => o.Id == orgId);

        if (org is null) return NotFound();
        return Ok(org);
    }

    // PUT api/organization
    // met à jour les infos de l'organisation
    [HttpPut]
    public async Task<ActionResult<Organization>> Update(
        [FromBody] Organization organization)
    {
        var orgId = GetOrganizationId();
        var org = await _db.Organizations
            .Include(o => o.Logo)
            .Include(o => o.ContactStatusSettings)
            .FirstOrDefaultAsync(o => o.Id == orgId);

        if (org is null) return NotFound();

        if (organization.FiscalStatus != null
            && !FiscalStatus.IsValid(organization.FiscalStatus))
            return BadRequest("Statut fiscal invalide.");


        await _db.SaveChangesAsync();
        return Ok(org);
    }

    // PUT api/organization/status-settings
    // met à jour les paramètres de statut des profils
    [HttpPut("status-settings")]
    public async Task<ActionResult<ContactStatusSettings>> UpdateStatusSettings(
        [FromBody] ContactStatusSettings settings)
    {
        var orgId = GetOrganizationId();
        var contactStatusSettings = await _db.ContactStatusSettings
            .FirstOrDefaultAsync(s => s.OrganizationId == orgId);

        if (contactStatusSettings is null)
        {
            // création à la volée si pas encore de settings
            contactStatusSettings = new ContactStatusSettings
            {
                OrganizationId = orgId,
                NewDurationDays = settings.NewDurationDays,
                ToRemindAfterMonths = settings.ToRemindAfterMonths,
                InactiveAfterMonths = settings.InactiveAfterMonths,
                CreatedAt = DateTime.UtcNow
            };
            _db.ContactStatusSettings.Add(settings);
        }
        else
        {
            contactStatusSettings.NewDurationDays     = settings.NewDurationDays;
            contactStatusSettings.ToRemindAfterMonths = settings.ToRemindAfterMonths;
            contactStatusSettings.InactiveAfterMonths = settings.InactiveAfterMonths;
            contactStatusSettings.UpdatedAt           = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return Ok(settings);
    }

    private Guid GetOrganizationId()
    {
        var claim = User.FindFirst("organization_id")?.Value;
        if (claim is null || !Guid.TryParse(claim, out var organizationId))
            throw new UnauthorizedAccessException("organization_id claim is missing.");
        return organizationId;
    }
}