using Common;
using DTO.League;
using DTO.Quiniela;

namespace Interface.UseCases;

public interface IQuinielaApplication
{
    Task<Response<QuinielaResponseDto>> CreateAsync(int userId, CreateQuinielaDto request);
    Task<Response<IEnumerable<QuinielaResponseDto>>> GetUserQuinielasAsync(int userId);
    Task<Response<QuinielaDetailDto>> GetByIdAsync(int quinielaId, int userId);
    Task<Response<QuinielaMemberDto>> JoinAsync(int userId, JoinQuinielaDto request);
    Task<Response<QuinielaMemberDto>> UpdatePaymentStatusAsync(int quinielaId, int memberId, bool paidFee, int requestingUserId);
    Task<Response<IEnumerable<LeagueDto>>> GetActiveLeaguesAsync();
    Task<Response<IEnumerable<MigratedMemberClaimLinkDto>>> GetClaimLinksAsync(int quinielaId, int requestingUserId, string? originUrl = null);
}
