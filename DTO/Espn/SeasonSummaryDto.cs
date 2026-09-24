namespace DTO.Espn;

public class SeasonSummaryDto
{
    public int Id { get; set; }
    public int LeagueId { get; set; }
    public string Name { get; set; } = null!;
    public int Year { get; set; }
    public bool IsCurrent { get; set; }
    public bool IsFinished { get; set; }
    public string Status { get; set; } = "ACTIVE"; // ACTIVE, UPCOMING, FINISHED
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int WeeksCount { get; set; }
}
