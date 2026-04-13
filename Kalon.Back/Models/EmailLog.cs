namespace Kalon.Back.Models;

public class EmailLog
{
    public Guid Id { get; set; }
    
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; }

    public Guid ContactId { get; set; }
    public Contact Contact { get; set; }
    public Guid? TaxReceiptId { get; set; }
    public TaxReceipt? TaxReceipt { get; set; }

    public string SentToEmail { get; set; }
    public string Subject { get; set; }
    public string Body { get; set; }

    // "sent" | "error"
    public string Status { get; set; }
    public string? ErrorMessage { get; set; }

    public DateTime SentAt { get; set; }
}