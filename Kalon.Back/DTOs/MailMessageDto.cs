namespace Kalon.Back.Dtos;

public class MailMessageDto
{
    public string ToEmail { get; set; }
    public string ToName { get; set; }
    public string Subject { get; set; }
    public string BodyHtml { get; set; }

    // expéditeur affiché — vient de Organization.SenderEmail
    // si null → fallback sur DefaultSenderEmail de la config
    public string? SenderEmail { get; set; }
    public string? SenderName { get; set; }

    // pièce jointe PDF optionnelle (pour les reçus fiscaux)
    public byte[]? AttachmentBytes { get; set; }
    public string? AttachmentFileName { get; set; }
}