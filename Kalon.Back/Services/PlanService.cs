using Kalon.Back.Configuration;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Kalon.Back.Services;

public interface IPlanService
{
    string PlanName { get; }
    int? MaxContacts { get; }
    int? MaxEmailsAnnual { get; }
    int? MaxDocumentsAnnual { get; }
    int? ArchivesAnnual { get; }
    int? DonorsSearchCountLimit { get; }
    decimal SearchQueryCost { get; }
    decimal SearchDetailCost { get; }
    bool IaMailEnabled { get; }
}

public class PlanService : IPlanService
{
    private readonly IHttpContextAccessor _httpContext;
    private readonly PlanOptions _planOptions;

    public PlanService(IHttpContextAccessor httpContext, IOptions<PlanOptions> planOptions)
    {
        _httpContext = httpContext;
        _planOptions = planOptions.Value;
    }

    // ── Lecture du plan depuis le JWT ─────────────────────────────

    public string PlanName =>
        GetClaim("plan_name") ?? "Free";

    // ── Limites numériques ────────────────────────────────────────

    public int? MaxContacts =>
        ParseNullableInt(_planOptions.MaxContactsApplicationFeatureValue);

    public int? MaxEmailsAnnual =>
        ParseNullableInt(_planOptions.MaxEmailsApplicationFeatureValue);

    public int? MaxDocumentsAnnual =>
        ParseNullableInt(_planOptions.MaxDocumentsApplicationFeatureValue);

    public int? ArchivesAnnual =>
        ParseNullableInt(_planOptions.ArchiveApplicationFeatureValue);

    public int? DonorsSearchCountLimit =>
        ParseNullableInt(_planOptions.DonorsSearchApplicationFeatureValueCountLimit);

    // ── Features décimales ───────────────────────────────────────

    public decimal SearchQueryCost =>
        ParseDecimal(_planOptions.DonorsSearchApplicationFeatureValueCost, defaultValue: 0.1m);

    public decimal SearchDetailCost =>
        ParseDecimal(_planOptions.DonorsDetailsApplicationFeatureValueCost, defaultValue: 2.5m);

    // ── Features booléennes ───────────────────────────────────────

    public bool IaMailEnabled =>
        ParseBool(_planOptions.IaMailApplicationFeatureValue);


    // ── Helpers privés ────────────────────────────────────────────

    private Dictionary<string, string> GetFeatures()
    {
        var json = GetClaim("plan_features") ?? "{}";
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                ?? new Dictionary<string, string>();
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }

    private string? GetClaim(string type) =>
        _httpContext.HttpContext?.User.FindFirst(type)?.Value;

    private int? ParseNullableInt(string featureKey)
    {
        var val = GetFeatures().GetValueOrDefault(featureKey);
        if (val == null || val == "null") return null;
        return int.TryParse(val, out var i) ? i : null;
    }

    private bool ParseBool(string featureKey)
    {
        var val = GetFeatures().GetValueOrDefault(featureKey, "false");
        return val == "true";
    }

    private decimal ParseDecimal(string featureKey, decimal defaultValue = 0)
    {
        var val = GetFeatures().GetValueOrDefault(featureKey);
        if (val == null || val == "null") return defaultValue;
        return decimal.TryParse(val,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out var d) ? d : defaultValue;
    }
}