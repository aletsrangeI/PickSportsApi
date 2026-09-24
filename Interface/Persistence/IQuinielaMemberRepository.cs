using Domain.Entities;

namespace Interface.Persistence;

public interface IQuinielaMemberRepository : IGenericRepository<QuinielaMember>
{
    Task<QuinielaMember?> GetMembershipAsync(int quinielaId, int userId);
    Task<IEnumerable<QuinielaMember>> GetMembersAsync(int quinielaId);
}
