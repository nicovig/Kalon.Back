namespace Kalon.Back.Services;

public class MeranOptions
{
    public string BaseUrl { get; set; } = string.Empty;

    public string? TokenEndpoint { get; set; }

    public string? ClientId { get; set; }

    public string? ClientSecret { get; set; }

    public string? Scope { get; set; }

    public string ApiClientToken { get; set; } = string.Empty;
}

