using Domain.Common;

namespace Domain.Entities;

public class Team : BaseAuditableEntity
{
    public int LeagueId { get; set; }
    public string EspnTeamId { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Abbreviation { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string? LogoUrl { get; set; }
    public string? PrimaryColor { get; set; }

    public League League { get; set; } = null!;
    public ICollection<Match> HomeMatches { get; set; } = new List<Match>();
    public ICollection<Match> AwayMatches { get; set; } = new List<Match>();
}
