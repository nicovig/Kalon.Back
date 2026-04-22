namespace Kalon.Back.DTOs;

public class GeneratedDocumentLightResponse
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string? OrderNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class GeneratedDocumentDetailsResponse
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string? OrderNumber { get; set; }
    public decimal? TaxReductionRate { get; set; }
    public string SnapshotOrgName { get; set; } = string.Empty;
    public string SnapshotContactDisplayName { get; set; } = string.Empty;
    public decimal SnapshotAmount { get; set; }
    public DateTime SnapshotDonationDate { get; set; }
    public string SnapshotDonationType { get; set; } = string.Empty;
    public string? PdfPath { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? GeneratedAt { get; set; }
    public string? SentToEmail { get; set; }
    public DateTime? SentAt { get; set; }
    public string? SendError { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class MailLogLightResponse
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid ContactId { get; set; }
    public bool IsEmail { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class MailLogListResponse
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public bool IsEmail { get; set; }
    public string SendAt { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class MailLogDetailsResponse
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid ContactId { get; set; }
    public string ContactDisplayName { get; set; } = string.Empty;
    public string? ContactEmail { get; set; }
    public Guid? GeneratedDocumentId { get; set; }
    public string? GeneratedDocumentType { get; set; }
    public string? GeneratedDocumentOrderNumber { get; set; }
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

