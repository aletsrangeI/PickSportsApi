using Domain.Entities;

namespace Interface.Persistence;

public interface ISportRepository : IGenericRepository<Sport>
{
    Task<Sport?> GetByCodeAsync(string code);
}
