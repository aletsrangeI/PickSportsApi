using Domain.Entities;

namespace Interface.Persistence;

public interface IMatchRepository : IGenericRepository<Match>
{
    Task<Match?> GetByEspnGameIdAsync(string espnGameId);
    Task<IEnumerable<Match>> GetByWeekIdAsync(int weekId);
    Task<IEnumerable<Match>> GetLiveMatchesAsync();
}
