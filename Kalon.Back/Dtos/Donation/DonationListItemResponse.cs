namespace Kalon.Back.Dtos.Donation;

public class DonationListItemResponse
{
    public Guid Id { get; set; }
    public Guid ContactId { get; set; }
    public string ContactDisplayName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public string DonationType { get; set; } = string.Empty;
    public string? PaymentMethod { get; set; }
    public bool IsAnonymous { get; set; }
    public DateTime CreatedAt { get; set; }
}
