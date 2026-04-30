namespace Kalon.Back.DTOs;

public class GeneratedDocumentSummary
{
    public Guid Id { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string? OrderNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? PdfPath { get; set; }
}

public class DonationResponse
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid ContactId { get; set; }
    public string ContactDisplayName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public string DonationType { get; set; } = string.Empty;
    public string? PaymentMethod { get; set; }
    public string? Notes { get; set; }
    public bool IsAnonymous { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public GeneratedDocumentSummary? GeneratedDocument { get; set; }
}

public class DonationListResponse
{
    public List<DonationResponse> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}
