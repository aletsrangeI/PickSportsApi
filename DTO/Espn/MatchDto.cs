namespace DTO.Espn;

public class MatchDto
{
    public int Id { get; set; }
    public int WeekId { get; set; }
    public string EspnGameId { get; set; } = null!;
    public DateTime DateUtc { get; set; }
    public TeamDto HomeTeam { get; set; } = null!;
    public TeamDto AwayTeam { get; set; } = null!;
    /// <summary>"pre" | "in" | "post" | "postponed"</summary>
    public string StatusState { get; set; } = "pre";
    public string? StatusDesc { get; set; }
    public int HomeScore { get; set; }
    public int AwayScore { get; set; }
    public string? WinnerAbbr { get; set; }
    public DateTime? PostponedToDate { get; set; }
    public string? Venue { get; set; }
    public string? City { get; set; }
    public DateTime LastSyncUtc { get; set; }
}
