using Domain.Entities;

namespace Interface.Persistence;

public interface IWeeklyAwardRepository : IGenericRepository<WeeklyAward>
{
    Task<IEnumerable<WeeklyAward>> GetAwardsByWeekAsync(int quinielaId, int weekId);
    Task<IEnumerable<WeeklyAward>> GetAllAwardsForQuinielaAsync(int quinielaId);
    Task DeleteAwardsForWeekAsync(int quinielaId, int weekId);
}
