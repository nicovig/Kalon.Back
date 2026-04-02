namespace Kalon.Back.Services;

public interface IMeranTokenProvider
{
    Task<string> GetBearerTokenAsync(CancellationToken cancellationToken = default);
}
