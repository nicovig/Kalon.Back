namespace Kalon.Back.Models;

public class EmailTemplate
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; }

    public string Name { get; set; }
    public string Subject { get; set; }
    public string Body { get; set; }

    public string EmailTemplateType { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public static class EmailTemplateTypes
{
    public const string Reminder = "chill_reminder";
    public const string ThankYou = "thank_you_reminder";
    public const string Emergency = "urgency_reminder";
    public const string Seasonal = "seasonal_reminder";
    public const string Renewal = "adhesion_renewal_reminder";
    public const string Fidelisation = "fidelity_reminder";
    public const string Anniversary = "anniversary_reminder";
    public const string Birthday = "birthday_reminder";
    public const string Other = "other";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Reminder,
        ThankYou,
        Emergency,
        Seasonal,
        Anniversary,
        Birthday,
        Renewal,
        Fidelisation,
        Other
    };

    public static bool IsValid(string? value) =>
        value is not null && All.Contains(value);
}