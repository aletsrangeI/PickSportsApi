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
    /// <summary>SPEC-020: abre la quiniela de la siguiente temporada copiando miembros en cero y archiva la actual.</summary>
    Task<Response<QuinielaResponseDto>> StartNextSeasonAsync(int quinielaId, int userId, StartNextSeasonDto request);
    /// <summary>SPEC-013: enciende o apaga la gamificacion de la quiniela (Owner/Admin; exige SeasonId).</summary>
    Task<Response<QuinielaResponseDto>> SetGamificationAsync(int quinielaId, int userId, bool enabled);
    Task<Response<IEnumerable<LeagueDto>>> GetActiveLeaguesAsync();
    Task<Response<IEnumerable<MigratedMemberClaimLinkDto>>> GetClaimLinksAsync(int quinielaId, int requestingUserId, string? originUrl = null);
}
