using Domain.Entities;
using Interface.Persistence;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories;

public class TeamRepository : GenericRepository<Team>, ITeamRepository
{
    public TeamRepository(ApplicationDbContext context) : base(context) { }

    public async Task<Team?> GetByEspnIdAsync(int leagueId, string espnTeamId)
    {
        return await _dbSet.AsNoTracking()
            .FirstOrDefaultAsync(t => t.LeagueId == leagueId && t.EspnTeamId == espnTeamId);
    }

    public async Task<IEnumerable<Team>> GetByLeagueIdAsync(int leagueId)
    {
        return await _dbSet.AsNoTracking()
            .Where(t => t.LeagueId == leagueId && t.Active)
            .OrderBy(t => t.DisplayName)
            .ToListAsync();
    }
}
