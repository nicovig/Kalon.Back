namespace Kalon.Back.Dtos.Donation;

public class DonationPagedResponse
{
    public IReadOnlyList<DonationListItemResponse> Items { get; set; } = Array.Empty<DonationListItemResponse>();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}
