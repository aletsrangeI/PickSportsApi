using Domain.Entities;

namespace Interface.Persistence;

public interface IPushSubscriptionRepository : IGenericRepository<PushSubscription>
{
    Task<PushSubscription?> GetByEndpointAsync(string endpoint);
    Task<IEnumerable<PushSubscription>> GetActiveSubscriptionsByUserAsync(int userId);
    Task<IEnumerable<PushSubscription>> GetActiveSubscriptionsByQuinielaAsync(int quinielaId);
    Task<bool> DeactivateEndpointAsync(string endpoint);
}

