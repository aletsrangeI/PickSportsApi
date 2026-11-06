using Domain.Entities;
using Interface.Persistence;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories;

public class DuelRepository : GenericRepository<DuelFixture>, IDuelRepository
{
    public DuelRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<DuelFixture>> GetFixturesAsync(int quinielaId)
    {
        return await _dbSet.AsNoTracking()
            .Include(f => f.HomeMember).ThenInclude(m => m.User)
            .Include(f => f.AwayMember).ThenInclude(m => m!.User)
            .Where(f => f.QuinielaId == quinielaId && f.Active)
            .OrderBy(f => f.WeekNumber).ThenBy(f => f.Round).ThenBy(f => f.Id)
            .ToListAsync();
    }

    public async Task<IEnumerable<DuelFixture>> GetFixturesByWeekNumberAsync(int quinielaId, int weekNumber)
    {
        return await _dbSet
            .Where(f => f.QuinielaId == quinielaId && f.WeekNumber == weekNumber && f.Active)
            .OrderBy(f => f.Id)
            .ToListAsync();
    }

    public async Task<DuelFixture?> GetFixtureAsync(int fixtureId)
    {
        return await _dbSet.AsNoTracking()
            .Include(f => f.HomeMember).ThenInclude(m => m.User)
            .Include(f => f.AwayMember).ThenInclude(m => m!.User)
            .FirstOrDefaultAsync(f => f.Id == fixtureId && f.Active);
    }

    public async Task<bool> InsertFixturesAsync(IEnumerable<DuelFixture> fixtures)
    {
        var list = fixtures.ToList();
        // BaseEntity.Active vale false por defecto y todas las lecturas filtran Active: sin esto el calendario quedaria invisible.
        foreach (var fixture in list) fixture.Active = true;

        await _dbSet.AddRangeAsync(list);
        return await _context.SaveChangesAsync() > 0;
    }

    public Task<bool> UpdateFixtureAsync(DuelFixture fixture) => UpdateAsync(fixture);

    public async Task DeleteFixturesAsync(int quinielaId)
    {
        var existing = await _dbSet.Where(f => f.QuinielaId == quinielaId).ToListAsync();
        if (existing.Count == 0) return;
        _dbSet.RemoveRange(existing);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> AnyResolvedAsync(int quinielaId)
    {
        return await _dbSet.AsNoTracking()
            .AnyAsync(f => f.QuinielaId == quinielaId && f.Result != null && f.Result != "BYE" && f.Active);
    }

    public async Task<QuinielaDuelSeason?> GetSeasonInfoAsync(int quinielaId)
    {
        return await _context.Set<QuinielaDuelSeason>()
            .FirstOrDefaultAsync(s => s.QuinielaId == quinielaId && s.Active);
    }

    public async Task<bool> UpsertSeasonInfoAsync(QuinielaDuelSeason info)
    {
        var set = _context.Set<QuinielaDuelSeason>();
        var existing = await set.FirstOrDefaultAsync(s => s.QuinielaId == info.QuinielaId);
        if (existing == null)
        {
            info.Active = true;
            await set.AddAsync(info);
        }
        else if (!ReferenceEquals(existing, info))
        {
            existing.Seed = info.Seed;
            existing.GeneratedAt = info.GeneratedAt;
            existing.ChampionMemberId = info.ChampionMemberId;
            existing.ClosedAt = info.ClosedAt;
            existing.CupName = info.CupName;
            existing.PunishmentText = info.PunishmentText;
        }

        return await _context.SaveChangesAsync() > 0;
    }

    public async Task<IEnumerable<QuinielaDuelSeason>> GetClosedSeasonInfosAsync(IEnumerable<int> quinielaIds)
    {
        var ids = quinielaIds.ToList();
        return await _context.Set<QuinielaDuelSeason>().AsNoTracking()
            .Include(s => s.Quiniela).ThenInclude(q => q.Season)
            .Include(s => s.ChampionMember).ThenInclude(m => m!.User)
            .Where(s => ids.Contains(s.QuinielaId) && s.ClosedAt != null && s.ChampionMemberId != null && s.Active)
            .OrderByDescending(s => s.ClosedAt)
            .ToListAsync();
    }
}
