using Domain.Entities;
using Interface.Persistence;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories;

public class MatchRepository : GenericRepository<Match>, IMatchRepository
{
    public MatchRepository(ApplicationDbContext context) : base(context) { }

    public async Task<Match?> GetByEspnGameIdAsync(string espnGameId)
    {
        return await _dbSet
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .FirstOrDefaultAsync(m => m.EspnGameId == espnGameId);
    }

    public async Task<IEnumerable<Match>> GetByWeekIdAsync(int weekId)
    {
        return await _dbSet.AsNoTracking()
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Where(m => m.WeekId == weekId)
            .OrderBy(m => m.DateUtc)
            .ToListAsync();
    }

    public async Task<IEnumerable<Match>> GetLiveMatchesAsync()
    {
        return await _dbSet
            .Include(m => m.Week)
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Where(m => m.StatusState == "in")
            .ToListAsync();
    }
}
