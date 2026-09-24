using Domain.Common;

namespace Domain.Entities;

public class PickAuditLog : BaseEntity
{
    public int QuinielaId { get; set; }
    public int WeekId { get; set; }
    public int MemberId { get; set; }
    public int MatchId { get; set; }
    public string PickAbbr { get; set; } = null!;
    public string Source { get; set; } = "MANUAL"; // MANUAL, AUTOFILL
    public string Action { get; set; } = null!; // created, updated, missing-autofilled
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public Quiniela Quiniela { get; set; } = null!;
    public Week Week { get; set; } = null!;
    public QuinielaMember Member { get; set; } = null!;
    public Match Match { get; set; } = null!;
}
