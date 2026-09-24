using Domain.Entities;
using Interface.Persistence;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories;

public class PickRepository : GenericRepository<Pick>, IPickRepository
{
    public PickRepository(ApplicationDbContext context) : base(context) { }

    public async Task<Pick?> GetPickAsync(int quinielaId, int memberId, int matchId)
    {
        return await _dbSet
            .FirstOrDefaultAsync(p => p.QuinielaId == quinielaId && p.MemberId == memberId && p.MatchId == matchId);
    }

    public async Task<IEnumerable<Pick>> GetMemberPicksForWeekAsync(int quinielaId, int memberId, int weekId)
    {
        return await _dbSet.AsNoTracking()
            .Include(p => p.Match)
            .Where(p => p.QuinielaId == quinielaId && p.MemberId == memberId && p.Match.WeekId == weekId)
            .ToListAsync();
    }

    public async Task<IEnumerable<Pick>> GetAllPicksForWeekAsync(int quinielaId, int weekId)
    {
        return await _dbSet.AsNoTracking()
            .Include(p => p.Match)
            .Include(p => p.Member)
            .Where(p => p.QuinielaId == quinielaId && p.Match.WeekId == weekId)
            .ToListAsync();
    }

    public async Task<IEnumerable<Pick>> GetAllPicksForQuinielaAsync(int quinielaId)
    {
        return await _dbSet.AsNoTracking()
            .Include(p => p.Match)
            .Include(p => p.Member)
            .Where(p => p.QuinielaId == quinielaId)
            .OrderBy(p => p.Match.DateUtc)
            .ToListAsync();
    }

    public async Task<IEnumerable<Pick>> GetMemberHistoricalPicksAsync(int quinielaId, int memberId)
    {
        return await _dbSet.AsNoTracking()
            .Include(p => p.Match)
            .Where(p => p.QuinielaId == quinielaId && p.MemberId == memberId)
            .OrderBy(p => p.Match.DateUtc)
            .ToListAsync();
    }

    public async Task<bool> UpsertPickAsync(Pick pick)
    {
        var existing = await _dbSet
            .FirstOrDefaultAsync(p => p.QuinielaId == pick.QuinielaId && p.MemberId == pick.MemberId && p.MatchId == pick.MatchId);

        if (existing != null)
        {
            existing.PickAbbr = pick.PickAbbr;
            existing.IsAutoFilled = pick.IsAutoFilled;
            existing.LastModified = DateTime.UtcNow;
            _dbSet.Update(existing);
        }
        else
        {
            await _dbSet.AddAsync(pick);
        }

        return await _context.SaveChangesAsync() > 0;
    }

    public async Task<int> InsertMissingAutoFilledPicksAsync(IEnumerable<Pick> picks)
    {
        // REGLA DE ORO: Insert If Not Exists. Jamás sobreescribir picks humanos existentes.
        int inserted = 0;
        foreach (var pick in picks)
        {
            bool exists = await _dbSet.AnyAsync(p =>
                p.QuinielaId == pick.QuinielaId &&
                p.MemberId == pick.MemberId &&
                p.MatchId == pick.MatchId);

            if (!exists)
            {
                pick.IsAutoFilled = true;
                pick.Created = DateTime.UtcNow;
                await _dbSet.AddAsync(pick);
                inserted++;
            }
        }

        if (inserted > 0)
        {
            await _context.SaveChangesAsync();
        }

        return inserted;
    }

    public async Task<bool> InsertRangeAsync(IEnumerable<Pick> picks)
    {
        await _dbSet.AddRangeAsync(picks);
        return await _context.SaveChangesAsync() > 0;
    }
}
