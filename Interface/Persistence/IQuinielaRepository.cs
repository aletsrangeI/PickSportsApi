using Domain.Entities;

namespace Interface.Persistence;

public interface IQuinielaRepository : IGenericRepository<Quiniela>
{
    Task<Quiniela?> GetByInviteCodeAsync(string inviteCode);
    Task<IEnumerable<Quiniela>> GetUserQuinielasAsync(int userId);
    Task<Quiniela?> GetWithDetailsAsync(int id);
    Task<IEnumerable<Quiniela>> GetByLeagueIdAsync(int leagueId);
}

