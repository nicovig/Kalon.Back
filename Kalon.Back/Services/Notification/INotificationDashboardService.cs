using Kalon.Back.DTOs;

namespace Kalon.Back.Services.Notification;

public interface INotificationDashboardService
{
    Task<NotificationDashboardResponse> GetDashboardAsync(Guid organizationId, CancellationToken cancellationToken);
}
