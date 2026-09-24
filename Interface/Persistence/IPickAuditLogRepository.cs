using Domain.Entities;

namespace Interface.Persistence;

public interface IPickAuditLogRepository : IGenericRepository<PickAuditLog>
{
    Task<IEnumerable<PickAuditLog>> GetLogsByWeekAsync(int quinielaId, int weekId);
}
