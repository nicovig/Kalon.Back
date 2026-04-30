namespace Kalon.Back.Models;

public class OrganizationLogo
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; }

    public string FileName { get; set; }       // nom original du fichier uploadé
    public string StoredPath { get; set; }     // /var/kalon/logos/{orgId}/{guid}.png
    public string MimeType { get; set; }       // "image/png" | "image/jpeg"
    public long FileSizeBytes { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }   // si le logo est remplacé
}