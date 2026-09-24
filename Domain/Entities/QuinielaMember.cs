using Domain.Common;

namespace Domain.Entities;

public class QuinielaMember : BaseAuditableEntity
{
    public int QuinielaId { get; set; }
    public int UserId { get; set; }
    public string Alias { get; set; } = null!;
    public string Role { get; set; } = "MEMBER"; // OWNER, ADMIN, MEMBER
    public bool PaidFee { get; set; }
    public int TotalHits { get; set; }
    public int TotalUpsets { get; set; }
    public int TotalHumillaciones { get; set; }
    public int CurrentStreak { get; set; }
    public int BestStreak { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public Quiniela Quiniela { get; set; } = null!;
    public User User { get; set; } = null!;
    public ICollection<Pick> Picks { get; set; } = new List<Pick>();
    public ICollection<WeeklyAward> WeeklyAwards { get; set; } = new List<WeeklyAward>();
}
