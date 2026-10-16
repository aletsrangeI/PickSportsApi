using DTO.Challenges;

namespace Interface.UseCases;

public interface IChallengeApplication
{
    Task<List<ChallengeDto>> GetChallengesByWeekAsync(int quinielaId, int weekId, int currentUserId, CancellationToken ct = default);
    Task<List<ChallengeDto>> GetPendingInvitationsAsync(int quinielaId, int currentUserId, CancellationToken ct = default);
    Task<ChallengeDto> CreateChallengeAsync(int quinielaId, int weekId, int currentUserId, CreateChallengeDto dto, CancellationToken ct = default);
    Task<ChallengeDto> RespondChallengeAsync(int quinielaId, int challengeId, int currentUserId, RespondChallengeDto dto, CancellationToken ct = default);
    Task<ChallengeDto> SettleDebtAsync(int quinielaId, int challengeId, int targetMemberId, int currentUserId, bool isAdmin, CancellationToken ct = default);
    Task<bool> CancelChallengeAsync(int quinielaId, int challengeId, int currentUserId, CancellationToken ct = default);
    Task ResolveWeekChallengesAsync(int quinielaId, int weekId, CancellationToken ct = default);
    Task ExpireUnacceptedChallengesAsync(int quinielaId, int weekId, CancellationToken ct = default);
}
