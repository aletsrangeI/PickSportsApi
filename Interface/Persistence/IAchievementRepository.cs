using Domain.Entities;

namespace Interface.Persistence;

public interface IAchievementRepository : IGenericRepository<UserAchievement>
{
    /// <summary>Trofeos del usuario; con seasonId solo los de esa temporada.</summary>
    Task<IEnumerable<UserAchievement>> GetByUserAsync(int userId, int? seasonId = null);
    Task<IEnumerable<UserAchievement>> GetUnseenAsync(int userId);
    Task<UserAchievement?> GetByIdAsync(int id);
    Task<bool> ExistsAsync(int userId, string code, int seasonId);
}
