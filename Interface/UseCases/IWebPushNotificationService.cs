using DTO.Notifications;

namespace Interface.UseCases;

public interface IWebPushNotificationService
{
    string GetVapidPublicKey();
    Task<int> SendNotificationToUserAsync(int userId, PushNotificationPayload payload, CancellationToken ct = default);
    Task<int> SendNotificationToUsersAsync(IEnumerable<int> userIds, PushNotificationPayload payload, CancellationToken ct = default);
    Task<int> SendNotificationToQuinielaAsync(int quinielaId, PushNotificationPayload payload, CancellationToken ct = default);
    Task<bool> SendTestNotificationAsync(int userId, CancellationToken ct = default);
}
