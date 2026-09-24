using Domain.Common;

namespace Domain.Entities;

public class Season : BaseAuditableEntity
{
    public int LeagueId { get; set; }
    public int Year { get; set; }
    public int SeasonType { get; set; } = 1;
    public string Name { get; set; } = null!;
    public bool IsCurrent { get; set; }
    /// <summary>Fecha de inicio de la temporada (primer partido). Requerida para fallback L2.</summary>
    public DateTime? StartDate { get; set; }
    /// <summary>Fecha de fin de la temporada (último partido). Requerida para fallback L2.</summary>
    public DateTime? EndDate { get; set; }

    public League League { get; set; } = null!;
    public ICollection<Week> Weeks { get; set; } = new List<Week>();
}
