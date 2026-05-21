namespace Kalon.Back.DTOs;

public class NotificationDashboardResponse
{
    public List<NotificationContactItem> ContactsToRemind { get; set; } = [];
    public List<NotificationContactItem> ContactsToSendTaxReceipts { get; set; } = [];
    public int PhysicalLettersToSendCount { get; set; }
    public DateTime? TaxReceiptPeriodFrom { get; set; }
    public DateTime? TaxReceiptPeriodTo { get; set; }
}

public class NotificationContactItem
{
    public Guid ContactId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}

