using Domain.Entities;
using Interface.Persistence;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories;

public class SeasonWrappedSnapshotRepository : GenericRepository<SeasonWrappedSnapshot>, ISeasonWrappedSnapshotRepository
{
    public SeasonWrappedSnapshotRepository(ApplicationDbContext context) : base(context) { }

    public async Task<SeasonWrappedSnapshot?> GetByKeyAsync(int quinielaId, int seasonId, int userId)
    {
        return await _dbSet.FirstOrDefaultAsync(s =>
            s.QuinielaId == quinielaId && s.SeasonId == seasonId && s.UserId == userId && s.Active);
    }

    public async Task<IEnumerable<SeasonWrappedSnapshot>> GetBySeasonAsync(int quinielaId, int seasonId)
    {
        return await _dbSet.AsNoTracking()
            .Where(s => s.QuinielaId == quinielaId && s.SeasonId == seasonId && s.Active)
            .ToListAsync();
    }
}
