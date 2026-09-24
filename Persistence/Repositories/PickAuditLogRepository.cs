using Domain.Entities;
using Interface.Persistence;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories;

public class PickAuditLogRepository : GenericRepository<PickAuditLog>, IPickAuditLogRepository
{
    public PickAuditLogRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<PickAuditLog>> GetLogsByWeekAsync(int quinielaId, int weekId)
    {
        return await _dbSet.AsNoTracking()
            .Include(l => l.Member)
            .Include(l => l.Match)
            .Where(l => l.QuinielaId == quinielaId && l.WeekId == weekId)
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync();
    }
}
