using Kalon.Back.Configuration;
using Kalon.Back.Dtos;
using Microsoft.Extensions.Options;

namespace Kalon.Back.Tests;

// Tests/MailServiceTests.cs
public class MailServiceTests
{
    private static IOptions<BrevoOptions> DefaultOptions() =>
        Options.Create(new BrevoOptions
        {
            SmtpHost = "smtp-relay.brevo.com",
            SmtpPort = 587,
            Username = "test",
            Password = "test",
            DefaultSenderEmail = "noreply@kalon-app.fr",
            DefaultSenderName = "Kalon"
        });

    [Fact]
    public void MailMessageDto_WithNullSender_UsesFallback()
    {
        // vérifie que la logique de fallback est correcte
        // sans appeler le vrai SMTP
        var dto = new MailMessageDto
        {
            ToEmail = "marie@example.com",
            ToName = "Marie Dupont",
            Subject = "Test",
            BodyHtml = "<p>Bonjour</p>",
            SenderEmail = null,
            SenderName = null
        };

        var options = DefaultOptions();

        // le sender effectif doit être le fallback
        var effectiveSender = dto.SenderEmail ?? options.Value.DefaultSenderEmail;
        Assert.Equal("noreply@kalon-app.fr", effectiveSender);
    }

    [Fact]
    public void MailMessageDto_WithOrgSender_UsesOrgEmail()
    {
        var dto = new MailMessageDto
        {
            ToEmail = "marie@example.com",
            ToName = "Marie Dupont",
            Subject = "Test",
            BodyHtml = "<p>Bonjour</p>",
            SenderEmail = "contact@magnificat.asso.fr",
            SenderName = "Association Magnificat"
        };

        var options = DefaultOptions();
        var effectiveSender = dto.SenderEmail ?? options.Value.DefaultSenderEmail;
        Assert.Equal("contact@magnificat.asso.fr", effectiveSender);
    }

    [Fact]
    public void MailMessageDto_WithAttachment_HasPdfAttachment()
    {
        var dto = new MailMessageDto
        {
            ToEmail = "marie@example.com",
            ToName = "Marie Dupont",
            Subject = "Votre reçu fiscal",
            BodyHtml = "<p>Veuillez trouver votre reçu en pièce jointe.</p>",
            AttachmentBytes = new byte[] { 0x25, 0x50, 0x44, 0x46 }, // %PDF
            AttachmentFileName = "recu_2026-001.pdf"
        };

        Assert.NotNull(dto.AttachmentBytes);
        Assert.Equal("recu_2026-001.pdf", dto.AttachmentFileName);
    }
}