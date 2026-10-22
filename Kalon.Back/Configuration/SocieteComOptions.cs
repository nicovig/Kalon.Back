namespace Kalon.Back.Configuration;

public class SocieteComOptions
{
    public const string Section = "SocieteCom";

    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public decimal TotalCreditsPurchased { get; set; }
    public decimal AlertThreshold { get; set; } = 100;
}
