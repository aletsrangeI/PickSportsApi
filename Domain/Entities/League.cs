using Domain.Common;

namespace Domain.Entities;

public class League : BaseAuditableEntity
{
    public int SportId { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Country { get; set; }
    public string? LogoUrl { get; set; }
    public string EspnApiPath { get; set; } = null!;
    /// <summary>Número esperado de jornadas en la temporada (usado en fallback L3).</summary>
    public int WeeksCount { get; set; } = 17;
    /// <summary>Duración de cada jornada en días (usado en fallback L3).</summary>
    public int WeekDurationDays { get; set; } = 7;
    /// <summary>Clave del deporte para construir URLs ESPN: "soccer" | "football".</summary>
    public string SportKey { get; set; } = "soccer";

    /// <summary>
    /// Indica si la jornada pertenece a la fase final (Liguilla/Playoffs): su número supera las jornadas regulares.
    /// Las quinielas de temporada regular no publican, autollenan ni califican estas jornadas (SPEC-020).
    /// </summary>
    public bool IsPlayoffWeek(int weekNumber) => WeeksCount > 0 && weekNumber > WeeksCount;

    public Sport Sport { get; set; } = null!;
    public ICollection<Team> Teams { get; set; } = new List<Team>();
    public ICollection<Season> Seasons { get; set; } = new List<Season>();
    public ICollection<Quiniela> Quinielas { get; set; } = new List<Quiniela>();
}
