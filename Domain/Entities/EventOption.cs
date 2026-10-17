using Domain.Common;

namespace Domain.Entities;

public class EventOption : BaseAuditableEntity
{
    public int EventQuestionId { get; set; }
    public string OptionText { get; set; } = null!;
    public string? ImageUrl { get; set; }
    public bool? IsCorrect { get; set; }
    public int OptionOrder { get; set; }

    public EventQuestion EventQuestion { get; set; } = null!;
    public ICollection<EventPick> Picks { get; set; } = new List<EventPick>();
}
