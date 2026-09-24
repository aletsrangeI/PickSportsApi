using Domain.Entities;
using Interface.Persistence;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories;

public class WeeklyAwardRepository : GenericRepository<WeeklyAward>, IWeeklyAwardRepository
{
    public WeeklyAwardRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<WeeklyAward>> GetAwardsByWeekAsync(int quinielaId, int weekId)
    {
        return await _dbSet.AsNoTracking()
            .Include(a => a.Member)
                .ThenInclude(m => m.User)
            .Where(a => a.QuinielaId == quinielaId && a.WeekId == weekId)
            .ToListAsync();
    }

    public async Task<IEnumerable<WeeklyAward>> GetAllAwardsForQuinielaAsync(int quinielaId)
    {
        return await _dbSet.AsNoTracking()
            .Include(a => a.Member)
                .ThenInclude(m => m.User)
            .Include(a => a.Week)
            .Where(a => a.QuinielaId == quinielaId)
            .OrderByDescending(a => a.Week.WeekNumber)
            .ThenBy(a => a.AwardType)
            .ToListAsync();
    }

    public async Task DeleteAwardsForWeekAsync(int quinielaId, int weekId)
    {
        var existing = await _dbSet
            .Where(a => a.QuinielaId == quinielaId && a.WeekId == weekId)
            .ToListAsync();

        if (existing.Any())
        {
            _dbSet.RemoveRange(existing);
            await _context.SaveChangesAsync();
        }
    }
}
