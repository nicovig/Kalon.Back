namespace Kalon.Back.Dtos;

// DTOs/Sending/SendDocumentDto.cs
public class SendDocumentDto
{
    // "reminder" | "cerfa_11580" | "cerfa_16216"
    // | "membership_certificate" | "payment_attestation"
    public string DocumentType { get; set; }

    // "email" | "print"
    public string Channel { get; set; }

    // null si Channel == "print" et DocumentType == "reminder"
    public string? Subject { get; set; }

    // HTML sérialisé depuis TipTap
    // pour les Cerfa : c'est l'encart libre (message personnalisé)
    // pour les relances : c'est le corps complet du mail
    public string BodyHtml { get; set; }

    public List<Guid> RecipientIds { get; set; }

    // optionnel — signature choisie dans ContentBlock
    public Guid? SignatureBlockId { get; set; }

    // requis si DocumentType != "reminder"
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
    public string ContactName { get; set; }
    public string Reason { get; set; }
}

// résultat retourné après une impression
public class PrintDocumentResultDto
{
    public byte[] PdfBytes { get; set; }
    public int PageCount { get; set; }
    public List<Guid> GeneratedDocumentIds { get; set; } = new();
}