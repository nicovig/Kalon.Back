namespace Kalon.Back.DTOs;

public class ContentBlockUpsertRequest
{
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string? Content { get; set; }
    public string? StoredPath { get; set; }
    public string? MimeType { get; set; }
    public bool UsableInEmail { get; set; } = true;
    public bool UsableInReceipt { get; set; } = true;
}

public class ContentBlockResponse
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string? Content { get; set; }
    public string? StoredPath { get; set; }
    public string? MimeType { get; set; }
    public bool UsableInEmail { get; set; }
    public bool UsableInReceipt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
