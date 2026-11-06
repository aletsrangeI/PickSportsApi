using Domain.Common;

namespace Domain.Entities;

/// <summary>
/// SPEC-013: trofeo desbloqueado por un usuario en una temporada. Unico por (UserId, Code, SeasonId).
/// IsSeen = false hasta que la PWA muestra el toast y confirma con /acknowledge.
/// </summary>
public class UserAchievement : BaseAuditableEntity
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>Codigo del catalogo (AchievementCatalog), p. ej. PRIMERA_SANGRE.</summary>
    public string Code { get; set; } = null!;

    public DateTime UnlockedAt { get; set; }

    /// <summary>Quiniela en la que se desbloqueo.</summary>
    public int QuinielaId { get; set; }
    public Quiniela Quiniela { get; set; } = null!;

    public int SeasonId { get; set; }
    public Season Season { get; set; } = null!;

    public bool IsSeen { get; set; }
    public DateTime? SeenAt { get; set; }
}
