namespace Kalon.Back.Dtos.Donation;

public class DonationDetailsResponse : DonationListItemResponse
{
    public string? Notes { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
