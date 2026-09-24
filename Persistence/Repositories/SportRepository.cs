using Domain.Entities;
using Interface.Persistence;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories;

public class SportRepository : GenericRepository<Sport>, ISportRepository
{
    public SportRepository(ApplicationDbContext context) : base(context) { }

    public async Task<Sport?> GetByCodeAsync(string code)
    {
        return await _dbSet.AsNoTracking().FirstOrDefaultAsync(s => s.Code.ToUpper() == code.ToUpper());
    }
}
