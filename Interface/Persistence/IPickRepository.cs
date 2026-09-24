using Domain.Entities;

namespace Interface.Persistence;

public interface IPickRepository : IGenericRepository<Pick>
{
    Task<Pick?> GetPickAsync(int quinielaId, int memberId, int matchId);
    Task<IEnumerable<Pick>> GetMemberPicksForWeekAsync(int quinielaId, int memberId, int weekId);
    Task<IEnumerable<Pick>> GetAllPicksForWeekAsync(int quinielaId, int weekId);
    Task<IEnumerable<Pick>> GetAllPicksForQuinielaAsync(int quinielaId);
    Task<IEnumerable<Pick>> GetMemberHistoricalPicksAsync(int quinielaId, int memberId);
    Task<bool> UpsertPickAsync(Pick pick);
    Task<int> InsertMissingAutoFilledPicksAsync(IEnumerable<Pick> picks);
    Task<bool> InsertRangeAsync(IEnumerable<Pick> picks);
}
