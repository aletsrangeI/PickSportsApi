using Domain.Common;

namespace Domain.Entities;

public class QuinielaChallenge : BaseAuditableEntity
{
    public int QuinielaId { get; set; }
    public int WeekId { get; set; }
    public int CreatorMemberId { get; set; }

    /// <summary>
    /// Tipos soportados: WEEKLY_RANK, GENERAL_RANK, MATCH_WINNER, EXACT_SCORE, SUDDEN_DEATH, TOTAL_GOALS
    /// </summary>
    public string ChallengeType { get; set; } = null!;

    /// <summary>
    /// Modalidades: WINNER_TAKES_ALL ("El Rey Cobra"), LAST_PLACE_PAYS ("El Último Paga")
    /// </summary>
    public string PayoutMode { get; set; } = "WINNER_TAKES_ALL";

    /// <summary>
    /// Descripción social de la apuesta (máx 200 caracteres, ej: "Unos tacos y un refresco").
    /// </summary>
    public string StakesDescription { get; set; } = null!;

    /// <summary>
    /// ID del partido objeto del reto (requerido para MATCH_WINNER y EXACT_SCORE).
    /// </summary>
    public int? TargetMatchId { get; set; }

    /// <summary>
    /// Estados: PENDING_ACCEPTANCE, ACTIVE, UNACCEPTED_EXPIRED, CANCELLED, RESOLVED_WITH_WINNER, RESOLVED_NO_WINNER
    /// </summary>
    public string Status { get; set; } = "PENDING_ACCEPTANCE";

    /// <summary>
    /// Crónica o resumen explicativo de la resolución (incluye notas editoriales o de burla).
    /// </summary>
    public string? ResolutionSummary { get; set; }

    /// <summary>
    /// Registro serializado en JSON de eventos de Doble Vida en SUDDEN_DEATH.
    /// </summary>
    public string? DoubleLifeEventsJson { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAtUtc { get; set; }

    // Propiedades de navegación
    public Quiniela Quiniela { get; set; } = null!;
    public Week Week { get; set; } = null!;
    public QuinielaMember CreatorMember { get; set; } = null!;
    public Match? TargetMatch { get; set; }
    public ICollection<QuinielaChallengeParticipant> Participants { get; set; } = new List<QuinielaChallengeParticipant>();
}
