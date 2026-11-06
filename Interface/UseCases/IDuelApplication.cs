using Common;
using DTO.Duels;

namespace Interface.UseCases;

public interface IDuelApplication
{
    /// <summary>
    /// Resuelve los duelos de la jornada ya calificada (idempotente). No hace nada en fechas de descanso.
    /// Si es la ultima fecha con duelos, cierra la temporada de duelos y corona al campeon.
    /// Se invoca desde GamificationApplication.ResolveWeekAsync (no hace Save: lo hace ScoreWeekAsync, aunque los repositorios de duelos guardan por su cuenta).
    /// </summary>
    Task ResolveWeekAsync(int quinielaId, int weekId);

    Task<Response<DuelScheduleDto>> ScheduleAsync(int quinielaId, int userId);
    Task<Response<DuelScheduleDto>> GetScheduleAsync(int quinielaId, int userId);
    Task<Response<IEnumerable<DuelFixtureDto>>> GetFixturesAsync(int quinielaId, int userId, int? weekNumber);
    Task<Response<IEnumerable<DuelStandingDto>>> GetStandingsAsync(int quinielaId, int userId);
    Task<Response<DuelMatchupDto>> GetMatchupAsync(int quinielaId, int userId, int fixtureId);
    Task<Response<IEnumerable<HallOfFameEntryDto>>> GetHallOfFameAsync(int userId);
    Task<Response<HallOfFameEntryDto>> UpdateCupAsync(int quinielaId, int userId, UpdateDuelCupDto request);
}
