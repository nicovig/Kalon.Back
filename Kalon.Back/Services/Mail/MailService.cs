using Kalon.Back.Configuration;
using Kalon.Back.Dtos;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Kalon.Back.Services.Mail;
 
public interface IMailService
{
    Task SendAsync(MailMessageDto message);
}

public class MailService : IMailService
{
    private readonly BrevoOptions _options;
    private readonly ILogger<MailService> _logger;

    public MailService(
        IOptions<BrevoOptions> options,
        ILogger<MailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(MailMessageDto message)
    {
        var email = new MimeMessage();

        // expéditeur — soit le domaine de l'asso (si configuré) soit le fallback Kalon
        var senderEmail = message.SenderEmail ?? _options.DefaultSenderEmail;
        var senderName = message.SenderName ?? _options.DefaultSenderName;

        email.From.Add(new MailboxAddress(senderName, senderEmail));
        email.To.Add(new MailboxAddress(message.ToName, message.ToEmail));

        // si l'asso envoie depuis son propre domaine, on ajoute Reply-To
        // pour que les réponses arrivent bien chez l'asso
        if (message.SenderEmail != null
            && message.SenderEmail != _options.DefaultSenderEmail)
        {
            email.ReplyTo.Add(new MailboxAddress(senderName, senderEmail));
        }

        email.Subject = message.Subject;

        var builder = new BodyBuilder
        {
            HtmlBody = message.BodyHtml,
            // version texte brut auto-générée pour les clients mail qui
            // n'affichent pas le HTML (accessibilité + délivrabilité)
            TextBody = StripHtml(message.BodyHtml)
        };

        // pièce jointe PDF (reçu fiscal, attestation...)
        if (message.AttachmentBytes != null && message.AttachmentFileName != null)
        {
            builder.Attachments.Add(
                message.AttachmentFileName,
                message.AttachmentBytes,
                ContentType.Parse("application/pdf"));
        }

        email.Body = builder.ToMessageBody();

        using var smtp = new SmtpClient();
        try
        {
            await smtp.ConnectAsync(
                _options.SmtpHost,
                _options.SmtpPort,
                SecureSocketOptions.StartTls);

            await smtp.AuthenticateAsync(
                _options.Username,
                _options.Password);

            await smtp.SendAsync(email);
        }
        finally
        {
            await smtp.DisconnectAsync(quit: true);
        }
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return "";
        return System.Text.RegularExpressions.Regex
            .Replace(html, "<.*?>", " ")
            .Replace("&nbsp;", " ")
            .Replace("&amp;", "&")
            .Replace("  ", " ")
            .Trim();
    }
}