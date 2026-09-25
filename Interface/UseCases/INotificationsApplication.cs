using Common;
using DTO.Notifications;

namespace Interface.UseCases;

public interface INotificationsApplication
{
    Task<Response<bool>> SubscribeAsync(int userId, PushSubscriptionRequestDto request);
    Task<Response<bool>> UnsubscribeAsync(int userId, UnsubscribePushRequestDto request);
    Task<Response<VapidPublicKeyDto>> GetVapidPublicKeyAsync();
    Task<Response<bool>> SendTestNotificationAsync(int userId, int? quinielaId = null);
}
