using Domain.Entities;

namespace Interface.Persistence;

public interface IChallengeRepository : IGenericRepository<QuinielaChallenge>
{
    Task<QuinielaChallenge?> GetByIdWithParticipantsAsync(int challengeId, CancellationToken ct = default);
    Task<List<QuinielaChallenge>> GetChallengesByWeekAsync(int quinielaId, int weekId, CancellationToken ct = default);
    Task<List<QuinielaChallenge>> GetPendingInvitationsByMemberAsync(int quinielaId, int memberId, CancellationToken ct = default);
    Task<List<QuinielaChallenge>> GetActiveOrPendingChallengesByWeekAsync(int quinielaId, int weekId, CancellationToken ct = default);
    Task<QuinielaChallengeParticipant?> GetParticipantAsync(int challengeId, int memberId, CancellationToken ct = default);
    Task AddParticipantAsync(QuinielaChallengeParticipant participant, CancellationToken ct = default);
}
