using Domain.Entities;

namespace Interface.Persistence;

public interface IEspnHealthLogRepository : IGenericRepository<EspnHealthLog>
{
    Task<EspnHealthLog?> GetLatestHealthStatusAsync();
}
