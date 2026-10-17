using Domain.Common;

namespace Domain.Entities;

public class EventGroup : BaseAuditableEntity
{
    public int? QuinielaId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public Quiniela? Quiniela { get; set; }
    public ICollection<EventRound> Rounds { get; set; } = new List<EventRound>();
}
