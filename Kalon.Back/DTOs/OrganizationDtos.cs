using System.ComponentModel.DataAnnotations;
using Kalon.Back.Models;

namespace Kalon.Back.DTOs;

public class OrganizationUpdateRequestDto
{
    [Required]
    public string Name { get; set; } = string.Empty;
    public string? Street { get; set; }
    public string? PostalCode { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Website { get; set; }
    [Required]
    public string RNA { get; set; } = string.Empty;
    [Required]
    public string SIRET { get; set; } = string.Empty;
    public string? FiscalStatus { get; set; }
    [Required]
    public ReceiptFrequency DefaultReceiptFrequency { get; set; } = ReceiptFrequency.Annually;
    public string? SenderEmail { get; set; }
    public string? SenderName { get; set; }
    public string? Description { get; set; }
    public int? FoundedYear { get; set; }
    public string? ActivitySector { get; set; }
    public string? AudienceDescription { get; set; }
    public List<string>? SendingPreferences { get; set; }
}

public class OrganizationStatusSettingsUpsertRequestDto
{
    public int NewDurationDays { get; set; } = 30;
    public int ToRemindAfterMonths { get; set; } = 12;
    public int InactiveAfterMonths { get; set; } = 24;
}

public class OrganizationResponseDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Street { get; set; }
    public string? PostalCode { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Website { get; set; }
    public string RNA { get; set; } = string.Empty;
    public string SIRET { get; set; } = string.Empty;
    public string FiscalStatus { get; set; } = string.Empty;
    public ReceiptFrequency DefaultReceiptFrequency { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? SenderEmail { get; set; }
    public string? SenderName { get; set; }
    public string? Description { get; set; }
    public int? FoundedYear { get; set; }
    public string? ActivitySector { get; set; }
    public string? AudienceDescription { get; set; }
    public List<string> SendingPreferences { get; set; } = [];
    public OrganizationLogoResponseDto? Logo { get; set; }
    public ContactStatusSettingsResponseDto? ContactStatusSettings { get; set; }
}

public class OrganizationLogoResponseDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string StoredPath { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class ContactStatusSettingsResponseDto
{
    public Guid Id { get; set; }
    public int NewDurationDays { get; set; }
    public int ToRemindAfterMonths { get; set; }
    public int InactiveAfterMonths { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
