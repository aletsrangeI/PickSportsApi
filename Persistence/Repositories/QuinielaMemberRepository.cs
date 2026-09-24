using Domain.Entities;
using Interface.Persistence;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories;

public class QuinielaMemberRepository : GenericRepository<QuinielaMember>, IQuinielaMemberRepository
{
    public QuinielaMemberRepository(ApplicationDbContext context) : base(context) { }

    public async Task<QuinielaMember?> GetMembershipAsync(int quinielaId, int userId)
    {
        return await _dbSet.AsNoTracking()
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.QuinielaId == quinielaId && m.UserId == userId && m.Active);
    }

    public async Task<IEnumerable<QuinielaMember>> GetMembersAsync(int quinielaId)
    {
        return await _dbSet.AsNoTracking()
            .Include(m => m.User)
            .Where(m => m.QuinielaId == quinielaId && m.Active)
            .OrderByDescending(m => m.TotalHits)
            .ThenByDescending(m => m.TotalUpsets)
            .ThenBy(m => m.TotalHumillaciones)
            .ToListAsync();
    }

    public async Task<IEnumerable<QuinielaMember>> GetByUserIdAsync(int userId)
    {
        return await _dbSet
            .Include(m => m.Quiniela)
            .Include(m => m.User)
            .Where(m => m.UserId == userId && m.Active)
            .ToListAsync();
    }
}
