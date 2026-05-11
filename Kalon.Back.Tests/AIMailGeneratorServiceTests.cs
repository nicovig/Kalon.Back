using System.Reflection;
using Kalon.Back.Configuration;
using Kalon.Back.DTOs;
using Kalon.Back.Models;
using Kalon.Back.Services.Mail;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Kalon.Back.Tests;

public class AiMailGeneratorServiceTests
{
    private sealed class TestableAiMailGeneratorService : AiMailGeneratorService
    {
        private readonly string _rawResponse;
        private readonly Exception? _exception;

        public TestableAiMailGeneratorService(string rawResponse, Exception? exception = null)
            : base(
                Options.Create(new AnthropicOptions { ApiKey = "test-key" }),
                NullLogger<AiMailGeneratorService>.Instance)
        {
            _rawResponse = rawResponse;
            _exception = exception;
        }

        protected override Task<string> GenerateRawResponseAsync(string systemPrompt, string userPrompt)
        {
            if (_exception is not null)
                throw _exception;
            return Task.FromResult(_rawResponse);
        }
    }

    [Fact]
    public void Constructor_CreatesService()
    {
        var service = new AiMailGeneratorService(
            Options.Create(new AnthropicOptions { ApiKey = "test-key" }),
            NullLogger<AiMailGeneratorService>.Instance);

        Assert.NotNull(service);
    }

    [Fact]
    public void TranslateEmailType_ReturnsExpectedLabel()
    {
        var method = typeof(AiMailGeneratorService)
            .GetMethod("TranslateEmailType", BindingFlags.NonPublic | BindingFlags.Static);

        var result = method!.Invoke(null, ["thank_you_reminder"]);

        Assert.Equal("remerciement", result);
    }

    [Fact]
    public void BuildOrgContext_ContainsKnownFields()
    {
        var method = typeof(AiMailGeneratorService)
            .GetMethod("BuildOrgContext", BindingFlags.NonPublic | BindingFlags.Static);
        var org = new Organization
        {
            Name = "Association Kalon",
            FoundedYear = 2020,
            Description = "Aide locale",
            ActivitySector = "Social",
            AudienceDescription = "Familles"
        };

        var result = (string)method!.Invoke(null, [org])!;

        Assert.Contains("Association Kalon", result);
        Assert.Contains("2020", result);
        Assert.Contains("Aide locale", result);
        Assert.Contains("Social", result);
        Assert.Contains("Familles", result);
    }

    [Fact]
    public async Task GenerateAsync_ReturnsResult_WhenRawJsonIsValid()
    {
        var service = new TestableAiMailGeneratorService("{\"subject\":\"Objet\",\"bodyHtml\":\"<p>Bonjour</p>\"}");
        var request = new AiMailRequestDto
        {
            EmailType = "thank_you_reminder",
            UserContext = "Merci pour votre soutien"
        };
        var org = new Organization { Name = "Association Kalon" };

        var result = await service.GenerateAsync(request, org);

        Assert.Equal("Objet", result.Subject);
        Assert.Equal("<p>Bonjour</p>", result.BodyHtml);
    }

    [Fact]
    public async Task GenerateAsync_ReturnsResult_WhenJsonIsWrappedInMarkdown()
    {
        var service = new TestableAiMailGeneratorService("```json\n{\"subject\":\"Objet\",\"bodyHtml\":\"<p>Bonjour</p>\"}\n```");
        var request = new AiMailRequestDto
        {
            EmailType = "thank_you_reminder",
            UserContext = "Merci pour votre soutien"
        };
        var org = new Organization { Name = "Association Kalon" };

        var result = await service.GenerateAsync(request, org);

        Assert.Equal("Objet", result.Subject);
        Assert.Equal("<p>Bonjour</p>", result.BodyHtml);
    }

    [Fact]
    public async Task GenerateAsync_Throws_WhenJsonIsInvalid()
    {
        var service = new TestableAiMailGeneratorService("{\"subject\":\"Objet\"}");
        var request = new AiMailRequestDto
        {
            EmailType = "thank_you_reminder",
            UserContext = "Merci pour votre soutien"
        };
        var org = new Organization { Name = "Association Kalon" };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.GenerateAsync(request, org));

        Assert.Equal("La génération IA a échoué. Réessayez ou rédigez manuellement.", ex.Message);
    }
}
