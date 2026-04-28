namespace Kalon.Back.Services.Mail;
using System.Text.Json;
using Anthropic.SDK;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;
using Kalon.Back.Configuration;
using Kalon.Back.DTOs;
using Kalon.Back.Models;
using Microsoft.Extensions.Options;
public interface IAiMailGeneratorService
{
    Task<AiMailResultDto> GenerateAsync(AiMailRequestDto request, Organization org);
}

public class AiMailGeneratorService : IAiMailGeneratorService
{
    private readonly AnthropicClient _client;
    private readonly ILogger<AiMailGeneratorService> _logger;

    public AiMailGeneratorService(
        IOptions<AnthropicOptions> options,
        ILogger<AiMailGeneratorService> logger)
    {
        _client = new AnthropicClient(options.Value.ApiKey);
        _logger = logger;
    }

    public async Task<AiMailResultDto> GenerateAsync(
        AiMailRequestDto request, Organization org)
    {
        // ── Couche 1 — System prompt (jamais visible par l'utilisateur) ──
        var systemPrompt = """
            Tu es un expert en communication associative française.
            Tu rédiges des mails chaleureux, authentiques et efficaces pour des associations.
            Tu utilises toujours un ton humain, jamais corporate.
            Tu n'inventes jamais d'informations sur l'association.
            Tu utilises les variables {{prenom}}, {{nom}}, {{total_dons}},
            {{date_dernier_don}}, {{mois_depuis_dernier_don}}, {{nom_association}}
            là où c'est pertinent et naturel.
            Tu réponds UNIQUEMENT avec un JSON valide, sans markdown ni explication.
            Format attendu : { "subject": "...", "bodyHtml": "..." }
            Le bodyHtml contient uniquement des balises <p>, <strong>, <em>, <br>.
            """;

        // ── Couche 2 — Contexte asso (automatique depuis les paramètres) ─
        var orgContext = BuildOrgContext(org);

        // ── Couche 3 — Intention utilisateur ─────────────────────────────
        var emailTypeLabel = TranslateEmailType(request.EmailType);
        var userPrompt = $"""
            Rédige un mail de type "{emailTypeLabel}".

            Contexte de l'association :
            {orgContext}

            Instruction de l'utilisateur :
            {request.UserContext}

            Longueur : 3 à 5 paragraphes maximum.
            """;

        var message = await _client.Messages.GetClaudeMessageAsync(
            new MessageParameters
            {
                Model = AnthropicModels.Claude45Haiku,
                MaxTokens = 1024,
                System = new List<SystemMessage>
                {
            new SystemMessage(systemPrompt)
                },
                Messages = new List<Message>
                {
            new() { Role = RoleType.User, Content = new List<ContentBase>
            {
                new TextContent { Text = userPrompt }
            }}
                }
            });

        var raw = message.Content.OfType<TextContent>().FirstOrDefault() ?? "";
        try
        {
            // nettoyer les éventuels backticks markdown
            var cleaned = raw
                .Replace("```json", "")
                .Replace("```", "")
                .Trim();

            var result = JsonSerializer.Deserialize<AiMailResultDto>(cleaned,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result is null || string.IsNullOrEmpty(result.BodyHtml))
                throw new InvalidOperationException("Réponse IA invalide.");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur parsing réponse IA : {Raw}", raw);
            throw new InvalidOperationException(
                "La génération IA a échoué. Réessayez ou rédigez manuellement.");
        }
    }

    private static string BuildOrgContext(Organization org)
    {
        var parts = new List<string>();

        parts.Add($"Nom : {org.Name}");

        if (org.FoundedYear.HasValue)
            parts.Add($"Fondée en : {org.FoundedYear}");

        if (!string.IsNullOrEmpty(org.Description))
            parts.Add($"Mission : {org.Description}");

        if (!string.IsNullOrEmpty(org.ActivitySector))
            parts.Add($"Secteur : {org.ActivitySector}");

        if (!string.IsNullOrEmpty(org.AudienceDescription))
            parts.Add($"Public cible : {org.AudienceDescription}");

        return string.Join("\n", parts);
    }

    private static string TranslateEmailType(string type) => type switch
    {
        "chill_reminder" => "relance douce",
        "thank_you_reminder" => "remerciement",
        "urgency_reminder" => "appel d'urgence",
        "seasonal_reminder" => "message saisonnier",
        "adhesion_renewal_reminder" => "renouvellement d'adhésion",
        "fidelity_reminder" => "fidélisation",
        "birthday_reminder" => "anniversaire du profil",
        "anniversary_reminder" => "anniversaire de la contribution financière",
        _              => "communication générale"
    };
}