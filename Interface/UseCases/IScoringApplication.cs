using Common;
using DTO.Scoring;

namespace Interface.UseCases;

public interface IScoringApplication
{
    Task<Response<QuinielaStandingsResponseDto>> GetStandingsAsync(int quinielaId, int weekId);
    Task<Response<ScoreWeekResultDto>> ScoreWeekAsync(int quinielaId, int weekId, int userId);
    Task<Response<IEnumerable<WeeklyAwardDto>>> GetAwardsAsync(int quinielaId, int? weekId);
}
