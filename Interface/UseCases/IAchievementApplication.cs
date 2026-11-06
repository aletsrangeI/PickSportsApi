using Common;
using DTO.Gamification;

namespace Interface.UseCases;

public interface IAchievementApplication
{
    /// <summary>
    /// Evalua el catalogo para los miembros de la quiniela con los picks de la temporada de la jornada
    /// e inserta los trofeos nuevos. No hace Save (lo hace ScoreWeekAsync).
    /// </summary>
    Task EvaluateWeekAsync(int quinielaId, int weekId);

    /// <summary>Otorga un trofeo concreto si el usuario aun no lo tiene en esa temporada. No hace Save.</summary>
    Task<bool> GrantAsync(int userId, string code, int quinielaId, int seasonId);

    Task<Response<IEnumerable<AchievementDto>>> GetUnseenAsync(int userId);
    Task<Response<AchievementDto>> AcknowledgeAsync(int userId, int achievementId);
    Task<Response<AchievementShowcaseDto>> GetShowcaseAsync(int userId, int? seasonId);
    Task<Response<AchievementShowcaseDto>> EquipTitleAsync(int userId, string? code);
}
