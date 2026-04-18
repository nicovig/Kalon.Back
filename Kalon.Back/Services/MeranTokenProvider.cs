using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Kalon.Back.Services;

public interface IMeranTokenProvider
{
    Task<string> GetBearerTokenAsync(CancellationToken cancellationToken = default);
}

public class MeranTokenProvider : IMeranTokenProvider
{
    private readonly MeranOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _cachedAccessToken;
    private DateTimeOffset _cachedExpiresAtUtc;

    public MeranTokenProvider(IOptions<MeranOptions> options, IHttpClientFactory httpClientFactory)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<string> GetBearerTokenAsync(CancellationToken cancellationToken = default)
    {
        if (UsesClientCredentials())
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                if (!string.IsNullOrEmpty(_cachedAccessToken) && DateTimeOffset.UtcNow < _cachedExpiresAtUtc)
                    return _cachedAccessToken;

                var (accessToken, expiresInSeconds) = await FetchClientCredentialsAsync(cancellationToken);
                _cachedAccessToken = accessToken;
                var bufferSeconds = Math.Min(120, Math.Max(0, expiresInSeconds / 10));
                _cachedExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(Math.Max(30, expiresInSeconds - bufferSeconds));
                return _cachedAccessToken;
            }
            finally
            {
                _gate.Release();
            }
        }

        if (!string.IsNullOrWhiteSpace(_options.ApiClientToken))
            return NormalizeToRawBearer(_options.ApiClientToken);

        throw new InvalidOperationException(
            "MeranOptions: configure TokenEndpoint + ClientId + ClientSecret for OAuth2 client credentials, or set ApiClientToken.");
    }

    private bool UsesClientCredentials()
    {
        return !string.IsNullOrWhiteSpace(_options.TokenEndpoint)
               && !string.IsNullOrWhiteSpace(_options.ClientId)
               && !string.IsNullOrWhiteSpace(_options.ClientSecret);
    }

    private async Task<(string AccessToken, int ExpiresInSeconds)> FetchClientCredentialsAsync(CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("MeranOAuth");
        var pairs = new List<KeyValuePair<string, string>>
        {
            new("grant_type", "client_credentials"),
            new("client_id", _options.ClientId!),
            new("client_secret", _options.ClientSecret!)
        };
        if (!string.IsNullOrWhiteSpace(_options.Scope))
            pairs.Add(new KeyValuePair<string, string>("scope", _options.Scope!));

        using var content = new FormUrlEncodedContent(pairs);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.TokenEndpoint) { Content = content };
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;

        if (!root.TryGetProperty("access_token", out var tokenProp))
            throw new InvalidOperationException("Meran token response missing access_token.");

        var accessToken = tokenProp.GetString();
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new InvalidOperationException("Meran token response access_token is empty.");

        var expiresIn = 3600;
        if (root.TryGetProperty("expires_in", out var expProp))
        {
            if (expProp.ValueKind == JsonValueKind.Number && expProp.TryGetInt32(out var n))
                expiresIn = n;
        }

        return (accessToken.Trim(), expiresIn);
    }

    private static string NormalizeToRawBearer(string value)
    {
        var v = value.Trim();
        if (v.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return v.Substring("Bearer ".Length).Trim();
        return v;
    }
}
