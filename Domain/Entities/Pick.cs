using Domain.Common;

namespace Domain.Entities;

public class Pick : BaseAuditableEntity
{
    public int QuinielaId { get; set; }
    public int MemberId { get; set; }
    public int MatchId { get; set; }
    public string PickAbbr { get; set; } = null!; // LocalAbbr, AwayAbbr o 'EMPATE'
    public bool IsAutoFilled { get; set; }
    public bool? IsHit { get; set; }
    public bool IsUpsetHit { get; set; }
    public bool IsHumillacion { get; set; }
    public bool IsSomnifero { get; set; }
    public bool IsEmpateFallido { get; set; }

    public Quiniela Quiniela { get; set; } = null!;
    public QuinielaMember Member { get; set; } = null!;
    public Match Match { get; set; } = null!;
}
