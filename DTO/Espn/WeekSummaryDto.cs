namespace DTO.Espn;

public class WeekSummaryDto
{
    public int Id { get; set; }
    public int WeekNumber { get; set; }
    public string Name { get; set; } = null!;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = null!;
    public int MatchesCount { get; set; }
    public int PostponedCount { get; set; }
}
