namespace Kalon.Back.Dtos.Auth;

public class MeranMembershipStatusResponse
{
    public Guid ApplicationId { get; set; }
    public Guid ApplicationUserId { get; set; }
    public Guid SubscriptionId { get; set; }
    public Guid ApplicationPlanId { get; set; }
    public bool IsActive { get; set; }
    public string? Plan { get; set; }
    public string? SubscriptionStatus { get; set; }
    public DateTime? TrialEndAt { get; set; }
    public DateTime? CurrentPeriodEnd { get; set; }
}
