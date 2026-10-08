using Domain.Entities;

namespace Interface.Persistence;

public interface IMatchRepository : IGenericRepository<Match>
{
    Task<Match?> GetByEspnGameIdAsync(string espnGameId);
    Task<Match?> GetByIdWithTeamsAsync(int id);
    Task<IEnumerable<Match>> GetByWeekIdAsync(int weekId);
    Task<IEnumerable<Match>> GetLiveMatchesAsync();
}
