using Domain.Common;

namespace Domain.Entities;

public class QuinielaChallengeParticipant : BaseAuditableEntity
{
    public int ChallengeId { get; set; }
    public int MemberId { get; set; }

    /// <summary>
    /// Estados: ACCEPTED, PENDING, DECLINED, EXPIRED
    /// </summary>
    public string Status { get; set; } = "PENDING";

    /// <summary>
    /// Pronóstico de ganador de partido para MATCH_WINNER (Abreviatura de equipo o "EMPATE").
    /// </summary>
    public string? PredictedWinnerAbbr { get; set; }

    /// <summary>
    /// Pronóstico de goles locales para EXACT_SCORE.
    /// </summary>
    public int? PredictedHomeScore { get; set; }

    /// <summary>
    /// Pronóstico de goles visitantes para EXACT_SCORE.
    /// </summary>
    public int? PredictedAwayScore { get; set; }

    /// <summary>
    /// Pronóstico de suma total de goles para TOTAL_GOALS.
    /// </summary>
    public int? PredictedTotalGoals { get; set; }

    /// <summary>
    /// Resultado: NONE, WINNER, LOSER, NO_WINNER_BUST
    /// </summary>
    public string Outcome { get; set; } = "NONE";

    /// <summary>
    /// Partido donde fue eliminado el participante en SUDDEN_DEATH.
    /// </summary>
    public int? EliminatedAtMatchId { get; set; }

    /// <summary>
    /// Sello de honor: True si el ganador o admin marcó la apuesta como cumplida/pagada.
    /// </summary>
    public bool IsDebtSettled { get; set; }

    public DateTime? SettledAtUtc { get; set; }
    public DateTime? RespondedAtUtc { get; set; }

    // Propiedades de navegación
    public QuinielaChallenge Challenge { get; set; } = null!;
    public QuinielaMember Member { get; set; } = null!;
    public Match? EliminatedAtMatch { get; set; }
}
