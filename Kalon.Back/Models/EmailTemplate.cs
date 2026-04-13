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
    public const string Reminder = "reminder";
    public const string ThankYou = "thank_you";
    public const string Emergency = "emergency";
    public const string Seasonal = "seasonal";
    public const string Anniversary = "anniversary";
    public const string Birthday = "birthday";
    public const string Renewal = "renewal";
    public const string Fidelisation = "fidelisation";
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