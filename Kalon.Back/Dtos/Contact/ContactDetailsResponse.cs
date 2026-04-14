namespace Kalon.Back.Dtos.Contact;

public class ContactDetailsResponse : ContactListItemResponse
{
    public string? JobTitle { get; set; }
    public DateTime? BirthDate { get; set; }
    public string? Gender { get; set; }
    public string? Notes { get; set; }
    public string? PreferredFrequencySendingReceipt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
