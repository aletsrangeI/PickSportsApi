using Domain.Common;

namespace Domain.Entities;

public class Week : BaseAuditableEntity
{
    public int SeasonId { get; set; }
    public int WeekNumber { get; set; }
    public string Name { get; set; } = null!;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = "DRAFT"; // DRAFT, PUBLISHED, LOCKED, SCORED
    public DateTime? FirstGameUtc { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime? LockedAt { get; set; }
    public DateTime? ScoredAt { get; set; }

    public Season Season { get; set; } = null!;
    public ICollection<Match> Matches { get; set; } = new List<Match>();
    public ICollection<WeeklyAward> WeeklyAwards { get; set; } = new List<WeeklyAward>();
    public ICollection<WeeklyBulletin> WeeklyBulletins { get; set; } = new List<WeeklyBulletin>();
    public ICollection<PickAuditLog> AuditLogs { get; set; } = new List<PickAuditLog>();
}
