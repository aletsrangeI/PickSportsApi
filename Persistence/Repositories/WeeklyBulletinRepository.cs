using Domain.Entities;
using Interface.Persistence;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories;

public class WeeklyBulletinRepository : GenericRepository<WeeklyBulletin>, IWeeklyBulletinRepository
{
    public WeeklyBulletinRepository(ApplicationDbContext context) : base(context) { }

    public async Task<WeeklyBulletin?> GetByQuinielaAndWeekAsync(int quinielaId, int weekId)
    {
        return await _dbSet
            .FirstOrDefaultAsync(b => b.QuinielaId == quinielaId && b.WeekId == weekId && b.Active);
    }

    public async Task<IEnumerable<WeeklyBulletin>> GetAllForQuinielaAsync(int quinielaId)
    {
        return await _dbSet.AsNoTracking()
            .Include(b => b.Week)
            .Where(b => b.QuinielaId == quinielaId && b.Active)
            .OrderByDescending(b => b.Week.WeekNumber)
            .ToListAsync();
    }
}
