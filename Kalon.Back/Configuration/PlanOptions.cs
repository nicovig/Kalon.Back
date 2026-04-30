namespace Kalon.Back.Configuration;

public class PlanOptions
{
    public const string Section = "Plan";

    public string MaxContactsApplicationFeatureValue { get; set; } = string.Empty;

    public string MaxEmailsApplicationFeatureValue { get; set; } = string.Empty;

    public string MaxDocumentsApplicationFeatureValue { get; set; } = string.Empty;

    public string ArchiveApplicationFeatureValue { get; set; } = string.Empty;

    public string IaMailApplicationFeatureValue { get; set; } = string.Empty;

    public string DonorsSearchApplicationFeatureValueCost { get; set; } = string.Empty;

    public string DonorsDetailsApplicationFeatureValueCost { get; set; } = string.Empty;

    public string DonorsSearchApplicationFeatureValueCountLimit { get; set; } = string.Empty;
}
