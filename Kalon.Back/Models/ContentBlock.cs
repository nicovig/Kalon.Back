namespace Kalon.Back.Models;

public class ContentBlock
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; }

    public string Name { get; set; }           // ex: "Signature président", "Intro remerciement"

    // "text" | "image" | "signature"
    // text   → bloc de texte avec variables possibles
    // image  → tampon, signature scannée, image décorative
    // signature → image manuscrite (même principe que image mais sémantique différente)
    public string Kind { get; set; }

    // contenu selon le Kind :
    // si text      → le texte avec variables {{...}}
    // si image     → null (voir StoredPath)
    // si signature → null (voir StoredPath)
    public string? Content { get; set; }

    // chemin fichier si Kind == "image" | "signature"
    // /var/kalon/blocks/{orgId}/{guid}.png
    public string? StoredPath { get; set; }
    public string? MimeType { get; set; }

    // utilisable dans les emails, les reçus fiscaux, ou les deux
    public bool UsableInEmail { get; set; } = true;
    public bool UsableInReceipt { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}