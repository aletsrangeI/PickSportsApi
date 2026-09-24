using Domain.Entities;

namespace Interface.Persistence;

public interface IWeekRepository : IGenericRepository<Week>
{
    Task<Week?> GetBySeasonAndNumberAsync(int seasonId, int weekNumber);
    Task<IEnumerable<Week>> GetBySeasonIdAsync(int seasonId);
    Task<Week?> GetCurrentWeekAsync(int seasonId);
}
