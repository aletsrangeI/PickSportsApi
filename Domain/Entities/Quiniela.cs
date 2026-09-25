using Domain.Common;

namespace Domain.Entities;

public class Quiniela : BaseAuditableEntity
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int LeagueId { get; set; }
    public int OwnerId { get; set; }
    public string InviteCode { get; set; } = null!;
    public decimal EntryFee { get; set; } = 0.00m;
    public decimal FirstPlacePct { get; set; } = 70.00m;
    public decimal SecondPlacePct { get; set; } = 20.00m;
    public decimal ThirdPlacePct { get; set; } = 10.00m;
    public bool IsActive { get; set; } = true;

    public League League { get; set; } = null!;
    public User Owner { get; set; } = null!;
    public ICollection<QuinielaMember> Members { get; set; } = new List<QuinielaMember>();
    public ICollection<Pick> Picks { get; set; } = new List<Pick>();
    public ICollection<WeeklyAward> WeeklyAwards { get; set; } = new List<WeeklyAward>();
    public ICollection<WeeklyBulletin> WeeklyBulletins { get; set; } = new List<WeeklyBulletin>();
    public ICollection<PickAuditLog> AuditLogs { get; set; } = new List<PickAuditLog>();
}