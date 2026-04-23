using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Kalon.Back.Data;
using Kalon.Back.DTOs;
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
    [ProducesResponseType(typeof(OrganizationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrganizationResponseDto>> Get()
    {
        var orgId = GetOrganizationId();
        var org = await _db.Organizations
            .Include(o => o.Logo)
            .Include(o => o.ContactStatusSettings)
            .FirstOrDefaultAsync(o => o.Id == orgId);

        if (org is null)
            return NotFound(new ApiMessageResponse { Message = "Organisation introuvable." });
        return Ok(ToResponseDto(org));
    }

    // PUT api/organization
    // met à jour les infos de l'organisation
    [HttpPut]
    [ProducesResponseType(typeof(OrganizationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrganizationResponseDto>> Update(
        [FromBody] OrganizationUpdateRequestDto organization)
    {
        var orgId = GetOrganizationId();
        var org = await _db.Organizations
            .Include(o => o.Logo)
            .Include(o => o.ContactStatusSettings)
            .FirstOrDefaultAsync(o => o.Id == orgId);

        if (org is null)
            return NotFound(new ApiMessageResponse { Message = "Organisation introuvable." });

        if (organization.FiscalStatus != null
            && !FiscalStatus.IsValid(organization.FiscalStatus))
            return BadRequest(new ApiMessageResponse { Message = "Statut fiscal invalide." });

        org.Name = organization.Name;
        org.Street = organization.Street;
        org.PostalCode = organization.PostalCode;
        org.City = organization.City;
        org.Country = organization.Country;
        org.Email = organization.Email;
        org.Phone = organization.Phone;
        org.Website = organization.Website;
        org.RNA = organization.RNA;
        org.SIRET = organization.SIRET;
        org.FiscalStatus = organization.FiscalStatus ?? org.FiscalStatus;
        org.DefaultReceiptFrequency = organization.DefaultReceiptFrequency;
        org.SenderEmail = organization.SenderEmail;
        org.SenderName = organization.SenderName;
        org.Description = organization.Description;
        org.FoundedYear = organization.FoundedYear;
        org.ActivitySector = organization.ActivitySector;
        org.AudienceDescription = organization.AudienceDescription;

        await _db.SaveChangesAsync();
        return Ok(ToResponseDto(org));
    }

    // PUT api/organization/status-settings
    // met à jour les paramètres de statut des profils
    [HttpPut("status-settings")]
    [ProducesResponseType(typeof(ContactStatusSettingsResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ContactStatusSettingsResponseDto>> UpdateStatusSettings(
        [FromBody] OrganizationStatusSettingsUpsertRequestDto settings)
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
            _db.ContactStatusSettings.Add(contactStatusSettings);
        }
        else
        {
            contactStatusSettings.NewDurationDays     = settings.NewDurationDays;
            contactStatusSettings.ToRemindAfterMonths = settings.ToRemindAfterMonths;
            contactStatusSettings.InactiveAfterMonths = settings.InactiveAfterMonths;
            contactStatusSettings.UpdatedAt           = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return Ok(ToStatusSettingsResponseDto(contactStatusSettings));
    }

    private Guid GetOrganizationId()
    {
        var claim = User.FindFirst("organization_id")?.Value;
        if (claim is null || !Guid.TryParse(claim, out var organizationId))
            throw new UnauthorizedAccessException("organization_id claim is missing.");
        return organizationId;
    }

    private static OrganizationResponseDto ToResponseDto(Organization org) =>
        new()
        {
            Id = org.Id,
            Name = org.Name,
            Street = org.Street,
            PostalCode = org.PostalCode,
            City = org.City,
            Country = org.Country,
            Email = org.Email,
            Phone = org.Phone,
            Website = org.Website,
            RNA = org.RNA,
            SIRET = org.SIRET,
            FiscalStatus = org.FiscalStatus,
            DefaultReceiptFrequency = org.DefaultReceiptFrequency,
            CreatedAt = org.CreatedAt,
            SenderEmail = org.SenderEmail,
            SenderName = org.SenderName,
            Description = org.Description,
            FoundedYear = org.FoundedYear,
            ActivitySector = org.ActivitySector,
            AudienceDescription = org.AudienceDescription,
            Logo = org.Logo is null
                ? null
                : new OrganizationLogoResponseDto
                {
                    Id = org.Logo.Id,
                    FileName = org.Logo.FileName,
                    StoredPath = org.Logo.StoredPath,
                    MimeType = org.Logo.MimeType,
                    FileSizeBytes = org.Logo.FileSizeBytes,
                    CreatedAt = org.Logo.CreatedAt,
                    UpdatedAt = org.Logo.UpdatedAt
                },
            ContactStatusSettings = org.ContactStatusSettings is null
                ? null
                : ToStatusSettingsResponseDto(org.ContactStatusSettings)
        };

    private static ContactStatusSettingsResponseDto ToStatusSettingsResponseDto(ContactStatusSettings settings) =>
        new()
        {
            Id = settings.Id,
            NewDurationDays = settings.NewDurationDays,
            ToRemindAfterMonths = settings.ToRemindAfterMonths,
            InactiveAfterMonths = settings.InactiveAfterMonths,
            CreatedAt = settings.CreatedAt,
            UpdatedAt = settings.UpdatedAt
        };
}