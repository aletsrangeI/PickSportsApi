using Domain.Entities;
using Interface.Persistence;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories;

public class AchievementRepository : GenericRepository<UserAchievement>, IAchievementRepository
{
    public AchievementRepository(ApplicationDbContext context) : base(context) { }

    /// <summary>
    /// Inserta y guarda. Si el guardado falla (p. ej. el indice unico (UserId, Code, SeasonId) por una calificacion
    /// concurrente), la entidad se desvincula del rastreador antes de relanzar: de lo contrario el Save() final de
    /// ScoreWeekAsync la reintentaria y fallaria la calificacion completa de la jornada.
    /// </summary>
    public override async Task<bool> InsertAsync(UserAchievement entity)
    {
        try
        {
            return await base.InsertAsync(entity);
        }
        catch (DbUpdateException)
        {
            _context.Entry(entity).State = EntityState.Detached;
            throw;
        }
    }

    public async Task<IEnumerable<UserAchievement>> GetByUserAsync(int userId, int? seasonId = null)
    {
        var query = _dbSet.AsNoTracking().Where(a => a.UserId == userId && a.Active);
        if (seasonId.HasValue)
        {
            query = query.Where(a => a.SeasonId == seasonId.Value);
        }

        return await query.OrderBy(a => a.UnlockedAt).ToListAsync();
    }

    public async Task<IEnumerable<UserAchievement>> GetUnseenAsync(int userId)
    {
        return await _dbSet.AsNoTracking()
            .Where(a => a.UserId == userId && !a.IsSeen && a.Active)
            .OrderBy(a => a.UnlockedAt)
            .ToListAsync();
    }

    public async Task<UserAchievement?> GetByIdAsync(int id)
    {
        return await _dbSet.FirstOrDefaultAsync(a => a.Id == id && a.Active);
    }

    public async Task<bool> ExistsAsync(int userId, string code, int seasonId)
    {
        return await _dbSet.AsNoTracking()
            .AnyAsync(a => a.UserId == userId && a.Code == code && a.SeasonId == seasonId);
    }
}
