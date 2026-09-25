using Domain.Entities;

namespace Interface.Persistence;

public interface IWeeklyBulletinRepository : IGenericRepository<WeeklyBulletin>
{
    Task<WeeklyBulletin?> GetByQuinielaAndWeekAsync(int quinielaId, int weekId);
    Task<IEnumerable<WeeklyBulletin>> GetAllForQuinielaAsync(int quinielaId);
}
