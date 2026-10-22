using System.Net;
using System.Text;
using Kalon.Back.Configuration;
using Kalon.Back.Services;
using Microsoft.Extensions.Options;

namespace Kalon.Back.Tests;

public class SocieteComServiceTests
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
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseJson, Encoding.UTF8, "application/json")
            });
        }
    }

    private static SocieteComService CreateService(CapturingHandler handler)
    {
        return new SocieteComService(
            new HttpClient(handler),
            Options.Create(new SocieteComOptions
            {
                BaseUrl = "https://api.societe.com",
                ApiKey = "secret-key"
            }));
    }

    [Fact]
    public async Task SearchAsync_BuildsUrlAndMapsResults()
    {
        var handler = new CapturingHandler("""
            {"data":{"results":[{"siren":"123456789","nom_commercial":"Alpha","naf_code":"0150Z","naf_lib":"Elevage","cp_ville":"49000 Angers","dep":"49","status":"A"}]}}
            """);
        var service = CreateService(handler);

        var results = await service.SearchAsync("49", "0150Z", 20);

        Assert.NotNull(handler.LastRequest);
        Assert.Contains("token=secret-key", handler.LastRequest!.RequestUri!.Query);
        Assert.Contains("dep=49", handler.LastRequest.RequestUri.Query);
        Assert.Contains("ape=0150Z", handler.LastRequest.RequestUri.Query);
        Assert.Single(results);
        Assert.Equal("123456789", results[0].Siren);
        Assert.Equal("Alpha", results[0].NomCommercial);
        Assert.Equal("49", results[0].Dep);
    }

    [Fact]
    public async Task GetBilanAsync_MapsLatestBilan()
    {
        var handler = new CapturingHandler("""
            {"data":[{"annee":2023,"ca":1000000,"resultat_net":50000}]}
            """);
        var service = CreateService(handler);

        var bilan = await service.GetBilanAsync("123456789");

        Assert.Equal("123456789", bilan.Siren);
        Assert.Equal(2023, bilan.Annee);
        Assert.Equal(1000000m, bilan.ChiffreAffaires);
        Assert.Equal(50000m, bilan.ResultatNet);
        Assert.Contains("/entreprise/123456789/bilans", handler.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetContactAsync_MapsFirstDirigeant()
    {
        var handler = new CapturingHandler("""
            {"data":[{"nom":"Dupont","prenom":"Marie","telephone":"0200000000","email":"marie@example.com"}]}
            """);
        var service = CreateService(handler);

        var contact = await service.GetContactAsync("123456789");

        Assert.Equal("Dupont", contact.NomDirigeant);
        Assert.Equal("Marie", contact.PrenomDirigeant);
        Assert.Equal("0200000000", contact.Telephone);
        Assert.Equal("marie@example.com", contact.Email);
    }
}
