using Domain.Common;

namespace Domain.Entities;

public class LiguillaBracket : BaseAuditableEntity
{
    public int QuinielaId { get; set; }
    public Quiniela Quiniela { get; set; } = null!;

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public bool IsLocked { get; set; }
    public DateTime SubmittedAtUtc { get; set; }
    public int TotalScore { get; set; }

    // Pronósticos de Cuartos de Final (4 equipos ganadores)
    public int QfWinner1TeamId { get; set; }
    public int QfWinner2TeamId { get; set; }
    public int QfWinner3TeamId { get; set; }
    public int QfWinner4TeamId { get; set; }

    // Pronósticos de Semifinales (2 equipos ganadores)
    public int SfWinner1TeamId { get; set; }
    public int SfWinner2TeamId { get; set; }

    // Campeón pronosticado
    public int ChampionTeamId { get; set; }

    // Desglose de puntos
    public int QfPoints { get; set; }
    public int SfPoints { get; set; }
    public int ChampionPoints { get; set; }
    public int BonusPoints { get; set; }
}
