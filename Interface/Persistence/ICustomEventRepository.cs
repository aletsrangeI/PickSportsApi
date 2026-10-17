using Domain.Entities;

namespace Interface.Persistence;

public interface ICustomEventRepository
{
    Task<EventGroup> GetOrCreateEventGroupForQuinielaAsync(int quinielaId, string defaultTitle = "Eventos");
    Task<EventGroup?> GetEventGroupByQuinielaIdAsync(int quinielaId);
    Task<IEnumerable<EventRound>> GetRoundsByQuinielaIdAsync(int quinielaId);
    Task<EventRound?> GetRoundByIdWithDetailsAsync(int roundId);
    Task<EventRound?> GetRoundByIdAsync(int roundId);
    Task<bool> InsertRoundAsync(EventRound round);
    Task<bool> UpdateRoundAsync(EventRound round);
    Task<bool> InsertQuestionAsync(EventQuestion question);
    Task<bool> InsertOptionsAsync(IEnumerable<EventOption> options);
    Task<bool> UpsertPicksAsync(IEnumerable<EventPick> picks);
    Task<IEnumerable<EventPick>> GetAllEventPicksForQuinielaAsync(int quinielaId);
    Task<IEnumerable<EventPick>> GetPicksByRoundIdAsync(int roundId);
    Task<int> GetMaxRoundOrderByQuinielaIdAsync(int quinielaId);
    Task<int> GetMaxQuestionOrderByRoundIdAsync(int roundId);
}
