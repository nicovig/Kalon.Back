namespace Kalon.Back.Dtos.Donation;

public class DonationUpsertRequest
{
    public Guid ContactId { get; set; }
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public string DonationType { get; set; } = string.Empty;
    public string? PaymentMethod { get; set; }
    public string? Notes { get; set; }
    public bool IsAnonymous { get; set; }
}
