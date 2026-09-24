using Domain.Entities;
using Interface.Persistence;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories;

public class SeasonRepository : GenericRepository<Season>, ISeasonRepository
{
    public SeasonRepository(ApplicationDbContext context) : base(context) { }

    public async Task<Season?> GetCurrentSeasonAsync(int leagueId)
    {
        return await _dbSet.AsNoTracking()
            .FirstOrDefaultAsync(s => s.LeagueId == leagueId && s.IsCurrent);
    }
}
