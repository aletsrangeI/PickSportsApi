using Domain.Entities;

namespace Interface.Persistence;

public interface ISeasonRepository : IGenericRepository<Season>
{
    Task<Season?> GetCurrentSeasonAsync(int leagueId);
}
