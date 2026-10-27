using System.Net.Http.Json;
using System.Text.Json;
using Kalon.Back.Configuration;
using Kalon.Back.DTOs;
using Microsoft.Extensions.Options;

namespace Kalon.Back.Services;

public class MeranClient(HttpClient httpClient, IOptions<MeranOptions> options, IMeranTokenProvider meranTokenProvider)
{
    private readonly MeranOptions _options = options.Value;
    private readonly IMeranTokenProvider _meranTokenProvider = meranTokenProvider;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<MeranMembershipStatus> GetUserStatusAsync(Guid applicationId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
            throw new InvalidOperationException("MeranOptions:BaseUrl is not configured.");

        var baseUrl = _options.BaseUrl.TrimEnd('/');
        var url = $"{baseUrl}/api/applications/{applicationId}/users/{userId}/status";

        var bearer = await _meranTokenProvider.GetBearerTokenAsync(cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearer);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var meranResponse = await JsonSerializer.DeserializeAsync<MeranMembershipStatus>(
            contentStream,
            JsonOptions,
            cancellationToken);

        if (meranResponse is null)
            throw new InvalidOperationException("Authentication web service status response is empty.");

        return meranResponse;
    }

    public async Task<Guid> CreateUserAsync(
        Guid applicationId,
        MeranCreateUserRequest createRequest,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
            throw new InvalidOperationException("MeranOptions:BaseUrl is not configured.");

        var baseUrl = _options.BaseUrl.TrimEnd('/');
        var url = $"{baseUrl}/api/applications/{applicationId}/users";

        var bearer = await _meranTokenProvider.GetBearerTokenAsync(cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(createRequest)
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearer);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var created = await JsonSerializer.DeserializeAsync<MeranCreateUserResponse>(
            contentStream,
            JsonOptions,
            cancellationToken);

        if (created is null || created.Id == Guid.Empty)
            throw new InvalidOperationException("Meran create user response is empty.");

        return created.Id;
    }
}

