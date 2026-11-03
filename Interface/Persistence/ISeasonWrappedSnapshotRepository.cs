using Domain.Entities;

namespace Interface.Persistence;

public interface ISeasonWrappedSnapshotRepository : IGenericRepository<SeasonWrappedSnapshot>
{
    Task<SeasonWrappedSnapshot?> GetByKeyAsync(int quinielaId, int seasonId, int userId);
    Task<IEnumerable<SeasonWrappedSnapshot>> GetBySeasonAsync(int quinielaId, int seasonId);
}
