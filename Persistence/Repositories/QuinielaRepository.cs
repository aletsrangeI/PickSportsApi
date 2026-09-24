using Domain.Entities;
using Interface.Persistence;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories;

public class QuinielaRepository : GenericRepository<Quiniela>, IQuinielaRepository
{
    public QuinielaRepository(ApplicationDbContext context) : base(context) { }

    public async Task<Quiniela?> GetByInviteCodeAsync(string inviteCode)
    {
        return await _dbSet.AsNoTracking()
            .Include(q => q.League)
                .ThenInclude(l => l.Sport)
            .Include(q => q.Owner)
            .FirstOrDefaultAsync(q => q.InviteCode.ToUpper() == inviteCode.ToUpper().Trim() && q.IsActive);
    }

    public async Task<IEnumerable<Quiniela>> GetUserQuinielasAsync(int userId)
    {
        return await _dbSet.AsNoTracking()
            .Include(q => q.League)
                .ThenInclude(l => l.Sport)
            .Include(q => q.Members)
            .Where(q => q.IsActive && q.Members.Any(m => m.UserId == userId && m.Active))
            .OrderByDescending(q => q.Created)
            .ToListAsync();
    }

    public async Task<Quiniela?> GetWithDetailsAsync(int id)
    {
        return await _dbSet.AsNoTracking()
            .Include(q => q.League)
                .ThenInclude(l => l.Sport)
            .Include(q => q.Owner)
            .Include(q => q.Members)
                .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(q => q.Id == id);
    }

    public async Task<IEnumerable<Quiniela>> GetByLeagueIdAsync(int leagueId)
    {
        return await _dbSet.AsNoTracking()
            .Where(q => q.LeagueId == leagueId && q.IsActive)
            .ToListAsync();
    }
}
