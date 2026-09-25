using Domain.Entities;
using Interface.Persistence;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories;

public class PushNotificationLogRepository : GenericRepository<PushNotificationLog>, IPushNotificationLogRepository
{
    public PushNotificationLogRepository(ApplicationDbContext context) : base(context) { }

    public async Task<bool> HasNotificationBeenSentTodayAsync(
        string notificationType,
        int weekId,
        int quinielaId,
        int? userId,
        string dateLocal,
        CancellationToken ct = default)
    {
        return await _dbSet.AnyAsync(l =>
            l.NotificationType == notificationType &&
            l.WeekId == weekId &&
            l.QuinielaId == quinielaId &&
            l.UserId == userId &&
            l.DateLocal == dateLocal, ct);
    }

    public async Task<bool> HasWeekOpenedBeenSentAsync(
        int weekId,
        int quinielaId,
        CancellationToken ct = default)
    {
        return await _dbSet.AnyAsync(l =>
            l.NotificationType == "WEEK_OPENED" &&
            l.WeekId == weekId &&
            l.QuinielaId == quinielaId, ct);
    }
}
