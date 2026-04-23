namespace Kalon.Back.Dtos;

// DTOs/Sending/SendDocumentDto.cs
public class SendDocumentDto
{
    // "message" | "cerfa_11580" | "cerfa_16216"
    // | "membership_certificate" | "payment_attestation"
    public string DocumentType { get; set; } = string.Empty;

    // "email" | "print"
    public string Channel { get; set; } = string.Empty;

    // sujet du message d'accompagnement (email ou lettre)
    public string? Subject { get; set; }

    // texte d'accompagnement (email ou lettre)
    public string BodyHtml { get; set; } = string.Empty;

    // texte injecté dans le document généré (cerfa / attestation / certificat)
    // ignoré pour DocumentType == "message"
    public string? DocumentBodyHtml { get; set; }

    public List<Guid> RecipientIds { get; set; } = [];

    // optionnel — signature choisie dans ContentBlock
    public Guid? SignatureBlockId { get; set; }

    // requis selon le type de document
    // ids des donations à rattacher au document
    public List<Guid>? DonationIds { get; set; }
}

// résultat retourné après un envoi
public class SendDocumentResultDto
{
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
    public List<SendDocumentErrorDto> Errors { get; set; } = new();
}

public class SendDocumentErrorDto
{
    public Guid ContactId { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

// résultat retourné après une impression
public class PrintDocumentResultDto
{
    public byte[] PdfBytes { get; set; } = [];
    public int PageCount { get; set; }
    public List<Guid> GeneratedDocumentIds { get; set; } = new();
}