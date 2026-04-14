using System.ComponentModel.DataAnnotations;

namespace Kalon.Back.Models;

public class Organization
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Street { get; set; }

    public string? PostalCode { get; set; }

    public string? City { get; set; }

    public string? Country { get; set; }

    [Required]
    [MaxLength(255)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? Website { get; set; }

    [Required]
    public Guid UserId { get; set; }

    public User User { get; set; }

    [Required]
    public string RNA { get; set; } = string.Empty;

    [Required]
    public string SIRET { get; set; } = string.Empty;


    public string FiscalStatus { get; set; } = string.Empty;

    [Required]
    public ReceiptFrequency DefaultReceiptFrequency { get; set; } = ReceiptFrequency.Annually;

    public DateTime CreatedAt { get; set; }

    // navigation Logo — null si pas encore uploadé
    public OrganizationLogo? Logo { get; set; }

    public ContactStatusSettings? ContactStatusSettings { get; set; }

    // navigation collections
    public ICollection<Contact> Contacts { get; set; } = new List<Contact>();
    public ICollection<EmailTemplate> EmailTemplates { get; set; } = new List<EmailTemplate>();
    public ICollection<MailLog> MailLogs { get; set; } = new List<MailLog>();
    public ICollection<GeneratedDocument> GeneratedDocuments { get; set; } = new List<GeneratedDocument>();
    public ICollection<ContentBlock> ContentBlocks { get; set; } = new List<ContentBlock>();
}

public enum ReceiptFrequency
{
    Monthly,
    Quarterly,
    HalfYearly,
    Annually,
    OneTime,
}

public static class FiscalStatus
{
    public const string GeneralInterest  = "general_interest";  // réduction IR 66% — Cerfa 11580
    public const string PublicUtility    = "public_utility";    // réduction IR 66% — Cerfa 11580
    public const string AidOrganization  = "aid_organization";  // réduction IR 75% — loi Coluche

    public static readonly IReadOnlyList<string> All = new[]
    {
        GeneralInterest,
        PublicUtility,
        AidOrganization
    };

    public static bool IsValid(string? value) =>
        value is not null && All.Contains(value);
}