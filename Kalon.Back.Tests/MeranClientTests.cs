using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kalon.Back.Dtos;
using Kalon.Back.Services;
using Microsoft.Extensions.Options;
using Xunit;

public sealed class FakeMeranTokenProvider : IMeranTokenProvider
{
    private readonly string _token;

    public FakeMeranTokenProvider(string token)
    {
        _token = token;
    }

    public Task<string> GetBearerTokenAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_token);
    }
}

public class MeranClientTests
{
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        private readonly string _responseJson;

        public CapturingHandler(string responseJson)
        {
            _responseJson = responseJson;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseJson, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

    [Fact]
    public async Task GetUserStatusAsync_BuildsUrlAndAuthorizationHeader()
    {
        var handler = new CapturingHandler("{\"isActive\":true,\"plan\":\"basic\"}");
        var httpClient = new HttpClient(handler);
        var options = new MeranOptions
        {
            BaseUrl = "http://example.com",
            ApiClientToken = "token123"
        };

        var client = new MeranClient(httpClient, Options.Create(options), new FakeMeranTokenProvider("token123"));

        var applicationId = Guid.Parse("356c9115-ca1e-4fd7-aa89-d6b07ade1530");
        var userId = Guid.Parse("7fa94a46-e440-4fce-b52b-be011a952c49");

        var result = await client.GetUserStatusAsync(applicationId, userId);

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.Equal($"http://example.com/api/applications/{applicationId}/users/{userId}/status", handler.LastRequest.RequestUri!.ToString());

        Assert.NotNull(handler.LastRequest.Headers.Authorization);
        Assert.Equal("Bearer", handler.LastRequest.Headers.Authorization!.Scheme);
        Assert.Equal("token123", handler.LastRequest.Headers.Authorization!.Parameter);

        Assert.True(result.IsActive);
        Assert.Equal("basic", result.Plan);
    }

    [Fact]
    public async Task GetUserStatusAsync_AllowsBearerTokenValue()
    {
        var handler = new CapturingHandler("{\"isActive\":false}");
        var httpClient = new HttpClient(handler);
        var options = new MeranOptions
        {   
            BaseUrl = "http://example.com",
            ApiClientToken = "Bearer token456"
        };

        var client = new MeranClient(httpClient, Options.Create(options), new FakeMeranTokenProvider("token456"));
        await client.GetUserStatusAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.NotNull(handler.LastRequest);
        Assert.NotNull(handler.LastRequest!.Headers.Authorization);
        Assert.Equal("Bearer", handler.LastRequest.Headers.Authorization!.Scheme);
        Assert.Equal("token456", handler.LastRequest.Headers.Authorization!.Parameter);
    }
}

