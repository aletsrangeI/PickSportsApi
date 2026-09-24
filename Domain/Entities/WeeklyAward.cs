using Domain.Common;

namespace Domain.Entities;

public class WeeklyAward : BaseAuditableEntity
{
    public int QuinielaId { get; set; }
    public int WeekId { get; set; }
    public int MemberId { get; set; }
    public string AwardType { get; set; } = null!; // MVP, REY_SORPRESAS, HUMILLADO, SOMNIFERO, EMPATE_FALLIDO, PARTIDO_DIFICIL
    public string AwardValue1 { get; set; } = null!;
    public string? AwardValue2 { get; set; }
    public string? Notes { get; set; }

    public Quiniela Quiniela { get; set; } = null!;
    public Week Week { get; set; } = null!;
    public QuinielaMember Member { get; set; } = null!;
}
