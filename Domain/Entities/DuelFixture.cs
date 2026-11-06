using Domain.Common;

namespace Domain.Entities;

/// <summary>
/// SPEC-013 Etapa 2: un duelo semanal 1 vs 1 de una quiniela. Solo existe en fechas de duelo
/// (las semanas de descanso no tienen fixtures). AwayMemberId nulo = Bye.
/// Indice unico (QuinielaId, WeekNumber, HomeMemberId).
/// </summary>
public class DuelFixture : BaseAuditableEntity
{
    public int QuinielaId { get; set; }
    public Quiniela Quiniela { get; set; } = null!;

    /// <summary>Numero de jornada regular (Week.WeekNumber) en la que se juega.</summary>
    public int WeekNumber { get; set; }

    /// <summary>Ronda dentro de la vuelta (1-based).</summary>
    public int Round { get; set; }

    /// <summary>Vuelta (1-based). Las vueltas pares invierten la localia.</summary>
    public int Leg { get; set; }

    public int HomeMemberId { get; set; }
    public QuinielaMember HomeMember { get; set; } = null!;

    /// <summary>Null = Bye (el miembro descansa esa fecha).</summary>
    public int? AwayMemberId { get; set; }
    public QuinielaMember? AwayMember { get; set; }

    public int HomeHits { get; set; }
    public int AwayHits { get; set; }

    /// <summary>"HOME" | "AWAY" | "DRAW" | "BYE"; null = pendiente.</summary>
    public string? Result { get; set; }

    /// <summary>Posicion en la Tabla General al empezar la jornada (null si aun no habia tabla).</summary>
    public int? HomeRankBefore { get; set; }
    public int? AwayRankBefore { get; set; }

    /// <summary>True si un empate de aciertos se resolvio por la Ventaja del Retador.</summary>
    public bool WonByAdvantage { get; set; }

    public DateTime? ResolvedAt { get; set; }
}
