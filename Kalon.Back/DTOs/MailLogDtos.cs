namespace Kalon.Back.DTOs;

public class MailLogUpsertRequest
{
    public Guid ContactId { get; set; }
    public Guid? GeneratedDocumentId { get; set; }
    public bool IsEmail { get; set; } = true;
    public string? SentToEmail { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTime? PrintedAt { get; set; }
    public DateTime? MailedAt { get; set; }
    public string? MailedBy { get; set; }
}

public class MailLogResponse
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid ContactId { get; set; }
    public Guid? GeneratedDocumentId { get; set; }
    public bool IsEmail { get; set; }
    public string? SentToEmail { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTime? PrintedAt { get; set; }
    public DateTime? MailedAt { get; set; }
    public string? MailedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}
