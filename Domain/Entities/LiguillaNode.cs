using Domain.Common;

namespace Domain.Entities;

public class LiguillaNode : BaseAuditableEntity
{
    public int SeasonId { get; set; }
    public string Round { get; set; } = string.Empty; // "QF", "SF", "FINAL"
    public int NodeNumber { get; set; } // 1..4 QF, 5..6 SF, 7 FINAL

    public int? TeamAId { get; set; }
    public int? TeamBId { get; set; }
    public Team? TeamA { get; set; }
    public Team? TeamB { get; set; }

    public int? SeedA { get; set; }
    public int? SeedB { get; set; }

    public int? WinnerTeamId { get; set; }
    public Team? WinnerTeam { get; set; }

    public bool IsResolved { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public DateTime? KickoffUtc { get; set; }

    public Season Season { get; set; } = null!;
}
