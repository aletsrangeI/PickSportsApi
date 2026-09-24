using Domain.Entities;
using Interface.Persistence;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories;

public class EspnHealthLogRepository : GenericRepository<EspnHealthLog>, IEspnHealthLogRepository
{
    public EspnHealthLogRepository(ApplicationDbContext context) : base(context) { }

    public async Task<EspnHealthLog?> GetLatestHealthStatusAsync()
    {
        return await _dbSet.AsNoTracking()
            .OrderByDescending(h => h.CheckedAt)
            .FirstOrDefaultAsync();
    }
}
