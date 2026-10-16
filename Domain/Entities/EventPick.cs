using Domain.Common;

namespace Domain.Entities;

public class EventPick : BaseAuditableEntity
{
    public int EventQuestionId { get; set; }
    public int EventOptionId { get; set; }
    public int QuinielaMemberId { get; set; }
    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;
    public bool? IsHit { get; set; }
    public int PointsEarned { get; set; }

    public EventQuestion EventQuestion { get; set; } = null!;
    public EventOption EventOption { get; set; } = null!;
    public QuinielaMember QuinielaMember { get; set; } = null!;
}
