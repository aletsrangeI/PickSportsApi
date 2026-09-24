using Domain.Common;

namespace Domain.Entities;

public class Sport : BaseAuditableEntity
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public bool HasDraw { get; set; }
    public string? Icon { get; set; }

    public ICollection<League> Leagues { get; set; } = new List<League>();
}
