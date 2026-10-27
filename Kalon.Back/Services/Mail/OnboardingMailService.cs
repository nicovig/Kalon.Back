using Kalon.Back.Dtos;

namespace Kalon.Back.Services.Mail;

public record WelcomeMailData
{
    public string Firstname { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string AssociationName { get; init; } = string.Empty;
    public string TemporaryPassword { get; init; } = string.Empty;
}

public interface IOnboardingMailService
{
    Task SendWelcomeAsync(WelcomeMailData data, CancellationToken ct);
}

public class OnboardingMailService(IMailService mailService) : IOnboardingMailService
{
    private const string AppUrl = "https://app.kalon-app.fr";

    public async Task SendWelcomeAsync(WelcomeMailData data, CancellationToken ct)
    {
        var body = $"""
            <!DOCTYPE html>
            <html lang="fr">
            <head><meta charset="UTF-8"><meta name="viewport" content="width=device-width,initial-scale=1"></head>
            <body style="margin:0;padding:0;background:#f4f6f8;font-family:'Nunito',Arial,sans-serif;">
              <table width="100%" cellpadding="0" cellspacing="0" style="background:#f4f6f8;padding:40px 0;">
                <tr><td align="center">
                  <table width="600" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,.08);">
                    
                    <!-- Header violet -->
                    <tr><td style="background:#6c3fc5;padding:32px 40px;text-align:center;">
                      <h1 style="margin:0;color:#ffffff;font-size:28px;font-weight:700;letter-spacing:-0.5px;">Kalon</h1>
                      <p style="margin:8px 0 0;color:#e0d4f7;font-size:14px;">Le CRM pensé pour les associations</p>
                    </td></tr>

                    <!-- Corps -->
                    <tr><td style="padding:40px;">
                      <p style="margin:0 0 16px;font-size:16px;color:#1a1a2e;">Bonjour <strong>{data.Firstname}</strong>,</p>
                      <p style="margin:0 0 24px;font-size:15px;color:#444;line-height:1.6;">
                        Votre espace Kalon pour <strong>{data.AssociationName}</strong> est prêt. 
                        Voici vos identifiants de connexion :
                      </p>

                      <!-- Bloc credentials -->
                      <table width="100%" cellpadding="0" cellspacing="0" style="background:#f0ebfc;border-radius:8px;margin-bottom:32px;">
                        <tr><td style="padding:24px 28px;">
                          <p style="margin:0 0 12px;font-size:13px;color:#6c3fc5;font-weight:700;text-transform:uppercase;letter-spacing:.8px;">Vos identifiants</p>
                          <p style="margin:0 0 8px;font-size:15px;color:#1a1a2e;">
                            📧 <strong>Email :</strong> {data.Email}
                          </p>
                          <p style="margin:0;font-size:15px;color:#1a1a2e;">
                            🔑 <strong>Mot de passe :</strong> <span style="font-family:monospace;background:#fff;padding:2px 8px;border-radius:4px;font-size:15px;">{data.TemporaryPassword}</span>
                          </p>
                        </td></tr>
                      </table>

                      <!-- CTA -->
                      <table width="100%" cellpadding="0" cellspacing="0" style="margin-bottom:32px;">
                        <tr><td align="center">
                          <a href="{AppUrl}" style="display:inline-block;background:#6c3fc5;color:#ffffff;text-decoration:none;font-size:16px;font-weight:700;padding:14px 40px;border-radius:8px;">
                            Accéder à mon espace →
                          </a>
                        </td></tr>
                      </table>

                      <!-- Conseils rapides -->
                      <p style="margin:0 0 12px;font-size:14px;font-weight:700;color:#1a1a2e;">Pour bien démarrer :</p>
                      <ul style="margin:0 0 24px;padding-left:20px;font-size:14px;color:#444;line-height:1.8;">
                        <li>Complétez le profil de votre association dans <strong>Paramètres</strong></li>
                        <li>Importez vos contacts via le modèle Excel disponible dans l'app</li>
                        <li>Personnalisez vos modèles d'emails de relance</li>
                      </ul>

                      <p style="margin:0;font-size:14px;color:#888;">
                        Une question ? Répondez simplement à cet email — je suis Nicolas, créateur de Kalon, et je réponds personnellement.
                      </p>
                    </td></tr>

                    <!-- Footer -->
                    <tr><td style="background:#f4f6f8;padding:24px 40px;text-align:center;border-top:1px solid #e8e8f0;">
                      <p style="margin:0;font-size:12px;color:#aaa;">
                        Kalon · L'Atelier Numérique de Nico · SIRET 105 566 731 00011<br>
                        <a href="{AppUrl}" style="color:#6c3fc5;text-decoration:none;">kalon-app.fr</a>
                      </p>
                    </td></tr>

                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;

        await mailService.SendAsync(new MailMessageDto
        {
            ToEmail = data.Email,
            ToName = data.Firstname,
            Subject = $"Bienvenue sur Kalon — vos accès {data.AssociationName}",
            BodyHtml = body
        });
    }
}