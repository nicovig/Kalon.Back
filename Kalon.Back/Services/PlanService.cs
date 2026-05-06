using Kalon.Back.Configuration;
using Microsoft.Extensions.Options;
using System.Globalization;
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

    private Dictionary<string, string?> GetFeatures()
    {
        var json = GetClaim("plan_features") ?? "{}";
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return new Dictionary<string, string?>();

            var features = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                features[property.Name] = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString(),
                    JsonValueKind.Number => property.Value.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Null => null,
                    _ => property.Value.GetRawText()
                };
            }

            return features;
        }
        catch
        {
            return new Dictionary<string, string?>();
        }
    }

    private string? GetClaim(string type) =>
        _httpContext.HttpContext?.User.FindFirst(type)?.Value;

    private int? ParseNullableInt(string featureKey)
    {
        var val = NormalizeFeatureValue(GetFeatures().GetValueOrDefault(featureKey));
        if (string.IsNullOrWhiteSpace(val))
            return null;
        return int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : null;
    }

    private bool ParseBool(string featureKey)
    {
        var val = NormalizeFeatureValue(GetFeatures().GetValueOrDefault(featureKey));
        return string.Equals(val, "true", StringComparison.OrdinalIgnoreCase);
    }

    private decimal ParseDecimal(string featureKey, decimal defaultValue = 0)
    {
        var val = NormalizeFeatureValue(GetFeatures().GetValueOrDefault(featureKey));
        if (string.IsNullOrWhiteSpace(val))
            return defaultValue;

        return decimal.TryParse(val,
            NumberStyles.Any,
            CultureInfo.InvariantCulture,
            out var d) ? d : defaultValue;
    }

    private static string? NormalizeFeatureValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim();

        if (normalized.Length >= 2 && normalized[0] == '"' && normalized[^1] == '"')
        {
            try
            {
                normalized = JsonSerializer.Deserialize<string>(normalized) ?? normalized[1..^1];
            }
            catch
            {
                normalized = normalized[1..^1];
            }
        }

        if (string.Equals(normalized, "null", StringComparison.OrdinalIgnoreCase))
            return null;

        return normalized;
    }
}