using Common;
using DTO.CustomEvents;

namespace Interface.UseCases;

public interface ICustomEventApplication
{
    Task<Response<IEnumerable<EventRoundSummaryDto>>> GetRoundsAsync(int quinielaId, int userId);
    Task<Response<EventRoundDetailDto>> GetRoundDetailAsync(int quinielaId, int roundId, int userId);
    Task<Response<EventRoundSummaryDto>> CreateRoundAsync(int quinielaId, int userId, CreateEventRoundDto request);
    Task<Response<EventRoundSummaryDto>> UpdateRoundAsync(int quinielaId, int roundId, int userId, UpdateEventRoundDto request);
    Task<Response<EventQuestionDto>> AddQuestionAsync(int quinielaId, int roundId, int userId, CreateEventQuestionDto request);
    Task<Response<EventRoundDetailDto>> SubmitPicksAsync(int quinielaId, int roundId, int userId, SubmitEventPicksDto request);
    Task<Response<EventRoundDetailDto>> ResolveRoundAsync(int quinielaId, int roundId, int userId, ResolveEventRoundDto request);
    Task<Response<EventStandingsResponseDto>> GetStandingsAsync(int quinielaId, int? roundId, int userId = 0);
}
