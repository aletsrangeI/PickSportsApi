using Domain.Entities;

namespace Interface.Persistence;

public interface ITeamRepository : IGenericRepository<Team>
{
    Task<Team?> GetByEspnIdAsync(int leagueId, string espnTeamId);
    Task<IEnumerable<Team>> GetByLeagueIdAsync(int leagueId);
}
