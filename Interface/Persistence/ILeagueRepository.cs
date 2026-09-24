using Domain.Entities;

namespace Interface.Persistence;

public interface ILeagueRepository : IGenericRepository<League>
{
    Task<League?> GetByCodeAsync(string code);
    Task<IEnumerable<League>> GetBySportIdAsync(int sportId);
    Task<IEnumerable<League>> GetActiveLeaguesWithSportAsync();
}
