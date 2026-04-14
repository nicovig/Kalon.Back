namespace Kalon.Back.Dtos.Contact;

public class ContactUpsertRequest
{
    public string Kind { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Firstname { get; set; } = string.Empty;
    public string Lastname { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? JobTitle { get; set; }
    public DateTime? BirthDate { get; set; }
    public string? Gender { get; set; }
    public string? Notes { get; set; }
    public string? Department { get; set; }
    public string? PreferredFrequencySendingReceipt { get; set; }
}
