using Kalon.Back.Models;

namespace Kalon.Back.DTOs;

public class ContactResponse
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public bool IsOut { get; set; }
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
    public ContactAddress? Address { get; set; }
    public ContactEnterprise? Enterprise { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public decimal TotalDonation { get; set; }
    public DateTime? FirstDonationAt { get; set; }
    public DateTime? LastDonation { get; set; }
    public decimal? LastDonationAmount { get; set; }
    public decimal AverageDonationAmount { get; set; }
    public int DonationCount { get; set; }
}
