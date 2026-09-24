using Domain.Entities;
using Interface.Persistence;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories;

public class LeagueRepository : GenericRepository<League>, ILeagueRepository
{
    public LeagueRepository(ApplicationDbContext context) : base(context) { }

    public async Task<League?> GetByCodeAsync(string code)
    {
        return await _dbSet.AsNoTracking()
            .Include(l => l.Sport)
            .FirstOrDefaultAsync(l => l.Code.ToLower() == code.ToLower());
    }

    public async Task<IEnumerable<League>> GetBySportIdAsync(int sportId)
    {
        return await _dbSet.AsNoTracking()
            .Where(l => l.SportId == sportId && l.Active)
            .ToListAsync();
    }

    public async Task<IEnumerable<League>> GetActiveLeaguesWithSportAsync()
    {
        return await _dbSet.AsNoTracking()
            .Include(l => l.Sport)
            .Where(l => l.Active)
            .OrderBy(l => l.Sport.Name)
            .ThenBy(l => l.Name)
            .ToListAsync();
    }
}
