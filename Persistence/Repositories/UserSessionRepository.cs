using Domain.Entities;
using Interface.Persistence;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories;

public class UserSessionRepository : GenericRepository<UserSession>, IUserSessionRepository
{
    public UserSessionRepository(ApplicationDbContext context) : base(context) { }

    public async Task<UserSession?> GetByRefreshTokenAsync(string refreshToken)
    {
        return await _dbSet.FirstOrDefaultAsync(s =>
            s.RefreshToken == refreshToken || s.PreviousRefreshToken == refreshToken);
    }

    public async Task PruneAsync(int userId, int maxSessions, DateTime nowUtc)
    {
        var sessions = await _dbSet
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.LastUsedAt)
            .ToListAsync();

        var stale = sessions
            .Where((s, index) => s.ExpiresAt <= nowUtc || index >= maxSessions)
            .ToList();

        if (stale.Count == 0) return;

        _dbSet.RemoveRange(stale);
        await _context.SaveChangesAsync();
    }
}
