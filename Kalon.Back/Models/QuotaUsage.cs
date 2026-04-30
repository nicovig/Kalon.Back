namespace Kalon.Back.Models;

public class QuotaUsage
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; }

    // "contacts" | "documents" | "emails" | "search_queries" | "search_details"
    public string QuotaType { get; set; }

    // "2026" pour les quotas annuels
    // "2026-04" pour les quotas mensuels (recherche mécènes)
    public string Period { get; set; }

    public int Count { get; set; }
    public DateTime LastUpdatedAt { get; set; }
}

public static class QuotaTypes
{
    public const string Contacts = "max_contacts";
    public const string Emails = "max_annual_emails";
    public const string Documents = "max_annual_documents";
    public const string Archives = "annual_archives";
    public const string MailAI = "mail_ai";
    public const string DonorsSearchCost = "donors_search_cost";
    public const string DonorsSearchDetailsCost = "donors_detail_cost";
    public const string DonorsSearchMonthlyLimit = "monthly_donors_search_limit";

    public static string GetPeriod(string quotaType)
    {
        var now = DateTime.UtcNow;
        return quotaType is DonorsSearchMonthlyLimit
            ? $"{now.Year}-{now.Month:D2}"  // mensuel
            : $"{now.Year}";                 // annuel
    }
}
