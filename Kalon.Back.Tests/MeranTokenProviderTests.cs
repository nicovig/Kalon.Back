using Kalon.Back.Configuration;
using Kalon.Back.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text;

namespace Kalon.Back.Tests;

public class MeranTokenProviderTests
{
    private sealed class StubFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public StubFactory(HttpMessageHandler handler)
        {
            _handler = handler;
        }

        public HttpClient CreateClient(string name)
        {
            return new HttpClient(_handler, disposeHandler: false);
        }
    }

    private sealed class OAuthTokenHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://idp.example.com/token", request.RequestUri!.ToString());
            var json = "{\"access_token\":\"oauth-access\",\"expires_in\":3600,\"token_type\":\"Bearer\"}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }

    [Fact]
    public async Task GetBearerTokenAsync_ClientCredentials_ReturnsAccessToken()
    {
        using var handler = new OAuthTokenHandler();
        var factory = new StubFactory(handler);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Application:ApplicationId"] = "kalon-app-id"
            })
            .Build();
        var options = new MeranOptions
        {
            TokenEndpoint = "https://idp.example.com/token",
            ClientId = "kalon",
            ClientSecret = "secret",
            Scope = "meran.api"
        };
        var provider = new MeranTokenProvider(Options.Create(options), factory, configuration);

        var t1 = await provider.GetBearerTokenAsync();
        var t2 = await provider.GetBearerTokenAsync();

        Assert.Equal("oauth-access", t1);
        Assert.Equal("oauth-access", t2);
    }

    [Fact]
    public async Task GetBearerTokenAsync_StaticToken_StripsBearerPrefix()
    {
        var factory = new StubFactory(new HttpClientHandler());
        var configuration = new ConfigurationBuilder().Build();
        var options = new MeranOptions
        {
            TokenEndpoint = "",
            ClientId = "",
            ClientSecret = "",
            ApiClientToken = "Bearer static-token"
        };
        var provider = new MeranTokenProvider(Options.Create(options), factory, configuration);
        var t = await provider.GetBearerTokenAsync();
        Assert.Equal("static-token", t);
    }

    [Fact]
    public async Task GetBearerTokenAsync_NoAuth_Throws()
    {
        var factory = new StubFactory(new HttpClientHandler());
        var configuration = new ConfigurationBuilder().Build();
        var options = new MeranOptions();
        var provider = new MeranTokenProvider(Options.Create(options), factory, configuration);
        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetBearerTokenAsync());
    }
}
