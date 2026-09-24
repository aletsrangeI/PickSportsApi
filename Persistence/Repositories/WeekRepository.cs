using Domain.Entities;
using Interface.Persistence;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories;

public class WeekRepository : GenericRepository<Week>, IWeekRepository
{
    public WeekRepository(ApplicationDbContext context) : base(context) { }

    public async Task<Week?> GetBySeasonAndNumberAsync(int seasonId, int weekNumber)
    {
        return await _dbSet.AsNoTracking()
            .Include(w => w.Matches)
                .ThenInclude(m => m.HomeTeam)
            .Include(w => w.Matches)
                .ThenInclude(m => m.AwayTeam)
            .FirstOrDefaultAsync(w => w.SeasonId == seasonId && w.WeekNumber == weekNumber);
    }

    public async Task<IEnumerable<Week>> GetBySeasonIdAsync(int seasonId)
    {
        return await _dbSet.AsNoTracking()
            .Where(w => w.SeasonId == seasonId && w.Active)
            .OrderBy(w => w.WeekNumber)
            .ToListAsync();
    }

    public async Task<Week?> GetCurrentWeekAsync(int seasonId)
    {
        // La semana activa preferentemente PUBLISHED o LOCKED, o la más cercana a hoy
        var now = DateTime.UtcNow;
        var publishedOrLocked = await _dbSet.AsNoTracking()
            .Include(w => w.Matches)
            .Where(w => w.SeasonId == seasonId && (w.Status == "PUBLISHED" || w.Status == "LOCKED"))
            .OrderBy(w => w.WeekNumber)
            .FirstOrDefaultAsync();

        if (publishedOrLocked != null) return publishedOrLocked;

        return await _dbSet.AsNoTracking()
            .Include(w => w.Matches)
            .Where(w => w.SeasonId == seasonId && w.Active)
            .OrderBy(w => Math.Abs((w.StartDate - now).TotalDays))
            .FirstOrDefaultAsync();
    }
}
