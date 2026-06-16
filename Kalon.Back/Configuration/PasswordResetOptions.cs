namespace Kalon.Back.Configuration;

public class PasswordResetOptions
{
    public const string Section = "PasswordReset";

    public string FrontendResetUrl { get; set; } = "http://localhost:4300/reset-password";

    public int TokenExpirationMinutes { get; set; } = 60;
}
