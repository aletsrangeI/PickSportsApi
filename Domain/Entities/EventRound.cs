using Domain.Common;

namespace Domain.Entities;

public class EventRound : BaseAuditableEntity
{
    public int EventGroupId { get; set; }
    public string Title { get; set; } = null!;
    public int RoundOrder { get; set; }
    public DateTime LockDateUtc { get; set; }
    public string Status { get; set; } = "Open";
    public DateTime? ResolvedDateUtc { get; set; }

    public EventGroup EventGroup { get; set; } = null!;
    public ICollection<EventQuestion> Questions { get; set; } = new List<EventQuestion>();
}
