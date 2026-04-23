using Swashbuckle.AspNetCore.Annotations;
namespace Kalon.Back.Dtos;

// DTOs/Sending/SendDocumentDto.cs
public class SendDocumentDto
{
    [SwaggerSchema(Description = "Type de document demandé. Valeurs: message, tax_receipt, membership_certificate, payment_attestation.")]
    public string DocumentType { get; set; } = string.Empty;

    [SwaggerSchema(Description = "Canal d'envoi. Valeurs: email, print.")]
    public string Channel { get; set; } = string.Empty;

    [SwaggerSchema(Description = "Sujet du message d'accompagnement (email ou courrier).")]
    public string? Subject { get; set; }

    [SwaggerSchema(Description = "Contenu HTML du message d'accompagnement (email ou courrier).")]
    public string BodyHtml { get; set; } = string.Empty;

    [SwaggerSchema(Description = "Contenu HTML injecté dans le document généré. Requis si DocumentType != message.")]
    public string? DocumentBodyHtml { get; set; }

    [SwaggerSchema(Description = "Liste des contacts destinataires. Avec DocumentType = tax_receipt, le backend choisit automatiquement le CERFA par destinataire: particulier -> cerfa_11580, entreprise -> cerfa_16216.")]
    public List<Guid> RecipientIds { get; set; } = [];

    [SwaggerSchema(Description = "Identifiant optionnel du bloc de signature.")]
    public Guid? SignatureBlockId { get; set; }

    [SwaggerSchema(Description = "Liste optionnelle des dons à rattacher au document généré.")]
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