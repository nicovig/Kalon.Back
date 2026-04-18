namespace Kalon.Back.Configuration;

public class BrevoOptions
{
    public const string Section = "Brevo";

    public string SmtpHost { get; set; } = "smtp-relay.brevo.com";
    public int SmtpPort { get; set; } = 587;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string DefaultSenderEmail { get; set; } = "noreply@kalon-app.fr";
    public string DefaultSenderName { get; set; } = "Kalon";
}