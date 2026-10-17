using Domain.Common;

namespace Domain.Entities;

public class EventQuestion : BaseAuditableEntity
{
    public int EventRoundId { get; set; }
    public string QuestionText { get; set; } = null!;
    public string? Description { get; set; }
    public int Points { get; set; } = 1;
    public string QuestionType { get; set; } = "SingleChoice";
    public int QuestionOrder { get; set; }

    public EventRound EventRound { get; set; } = null!;
    public ICollection<EventOption> Options { get; set; } = new List<EventOption>();
    public ICollection<EventPick> Picks { get; set; } = new List<EventPick>();
}
