namespace Kalon.Back.Models;

public class SearchCredit
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;

    public string Source { get; set; } = string.Empty;

    public decimal CreditsGranted { get; set; }
    public decimal CreditsUsed { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public class SearchCreditLog
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid SearchCreditId { get; set; }

    public string OperationType { get; set; } = string.Empty;

    public decimal CreditsConsumed { get; set; }
    public string? Siren { get; set; }
    public DateTime CreatedAt { get; set; }
}

public static class SearchCreditSources
{
    public const string MonthlyGrant = "monthly_grant";
    public const string Purchased = "purchased";

    public static readonly IReadOnlyList<string> All =
    [
        MonthlyGrant,
        Purchased
    ];

    public static bool IsValid(string? value) =>
        value is not null && All.Contains(value);
}

public static class SearchOperationTypes
{
    public const string Search = "search";
    public const string Bilan = "bilan";
    public const string Contact = "contact";

    public const decimal SearchCost = 0.1m;
    public const decimal BilanCost = 2.0m;
    public const decimal ContactCost = 0.5m;
    public const decimal DetailCost = BilanCost + ContactCost;

    public static readonly IReadOnlyList<string> All =
    [
        Search,
        Bilan,
        Contact
    ];

    public static bool IsValid(string? value) =>
        value is not null && All.Contains(value);
}
