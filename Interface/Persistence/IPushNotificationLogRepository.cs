using Domain.Entities;

namespace Interface.Persistence;

public interface IPushNotificationLogRepository : IGenericRepository<PushNotificationLog>
{
    Task<bool> HasNotificationBeenSentTodayAsync(
        string notificationType,
        int weekId,
        int quinielaId,
        int? userId,
        string dateLocal,
        CancellationToken ct = default);

    Task<bool> HasWeekOpenedBeenSentAsync(
        int weekId,
        int quinielaId,
        CancellationToken ct = default);
}
