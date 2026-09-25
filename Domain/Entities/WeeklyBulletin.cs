using Domain.Common;

namespace Domain.Entities;

public class WeeklyBulletin : BaseAuditableEntity
{
    public int QuinielaId { get; set; }
    public int WeekId { get; set; }
    public string? AdminAnnouncement { get; set; }
    public DateTime PublishedAtUtc { get; set; }
    public bool IsPublished { get; set; }

    public Quiniela Quiniela { get; set; } = null!;
    public Week Week { get; set; } = null!;
}
