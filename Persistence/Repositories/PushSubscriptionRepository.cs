using Domain.Entities;
using Interface.Persistence;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories;

public class PushSubscriptionRepository : GenericRepository<PushSubscription>, IPushSubscriptionRepository
{
    public PushSubscriptionRepository(ApplicationDbContext context) : base(context) { }

    public async Task<PushSubscription?> GetByEndpointAsync(string endpoint)
    {
        return await _dbSet.FirstOrDefaultAsync(s => s.Endpoint == endpoint);
    }

    public async Task<IEnumerable<PushSubscription>> GetActiveSubscriptionsByUserAsync(int userId)
    {
        return await _dbSet
            .Where(s => s.UserId == userId && s.Active)
            .ToListAsync();
    }

    public async Task<IEnumerable<PushSubscription>> GetActiveSubscriptionsByQuinielaAsync(int quinielaId)
    {
        return await _context.QuinielaMembers.AsNoTracking()
            .Where(m => m.QuinielaId == quinielaId && m.Active)
            .SelectMany(m => m.User.PushSubscriptions.Where(s => s.Active))
            .Distinct()
            .ToListAsync();
    }

    public async Task<bool> DeactivateEndpointAsync(string endpoint)
    {
        var sub = await _dbSet.FirstOrDefaultAsync(s => s.Endpoint == endpoint);
        if (sub != null)
        {
            sub.Active = false;
            return await _context.SaveChangesAsync() > 0;
        }
        return false;
    }
}
