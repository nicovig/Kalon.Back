using Kalon.Back.Dtos.Auth;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Kalon.Back.Services;

public class MeranClient(HttpClient httpClient, IOptions<MeranOptions> options, IMeranTokenProvider meranTokenProvider)
{
    private readonly MeranOptions _options = options.Value;
    private readonly IMeranTokenProvider _meranTokenProvider = meranTokenProvider;

    public async Task<MeranMembershipStatusResponse> GetUserStatusAsync(Guid applicationId, Guid userId, CancellationToken cancellationToken = default)
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
        var meranResponse = await JsonSerializer.DeserializeAsync<MeranMembershipStatusResponse>(
            contentStream,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            },
            cancellationToken);

        if (meranResponse is null)
            throw new InvalidOperationException("Authentication web service status response is empty.");

        return meranResponse;
    }
}

