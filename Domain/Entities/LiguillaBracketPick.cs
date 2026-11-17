using Domain.Common;

namespace Domain.Entities;

/// <summary>Pick de un equipo que avanza en una ronda (modelo generico por ronda, SPEC-027).</summary>
public class LiguillaBracketPick : BaseAuditableEntity
{
    public int BracketId { get; set; }
    public LiguillaBracket Bracket { get; set; } = null!;

    /// <summary>Codigo de ronda de la estrategia: "QF", "SF", "FINAL", "R1", "WC", "R16"...</summary>
    public string RoundCode { get; set; } = string.Empty;
    public int Slot { get; set; }
    public int TeamId { get; set; }
}
