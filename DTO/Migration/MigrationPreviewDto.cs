namespace DTO.Migration;

public class MigrationPlayerPreviewDto
{
    public string Alias { get; set; } = null!;
    public int ExpectedHits { get; set; }
    public int TotalPicks { get; set; }
    public decimal Percentage { get; set; }
    public bool IsExistingUser { get; set; }
    public int? ExistingUserId { get; set; }
    public string? ExistingDisplayName { get; set; }
}

public class MigrationWeekSummaryDto
{
    public int WeekNumber { get; set; }
    public string Status { get; set; } = null!;
    public int MatchesCount { get; set; }
    public int PicksCount { get; set; }
}

public class MigrationMatchSampleDto
{
    public string GameId { get; set; } = null!;
    public int WeekNumber { get; set; }
    public string HomeTeam { get; set; } = null!;
    public string AwayTeam { get; set; } = null!;
    public string StatusState { get; set; } = null!;
    public int? HomeScore { get; set; }
    public int? AwayScore { get; set; }
    public string? WinnerAbbr { get; set; }
}

public class MigrationPreviewDto
{
    public int SeasonYear { get; set; } = 2026;
    public int SeasonType { get; set; } = 1;
    public string LeagueCode { get; set; } = "mex.1";
    public string SuggestedQuinielaName { get; set; } = "Liga MX Clausura 2026";
    public int TotalParticipants { get; set; }
    public int TotalMatches { get; set; }
    public int TotalWeeks { get; set; }
    public int TotalPicks { get; set; }
    public int CurrentWeekNumber { get; set; } = 10;
    public List<MigrationPlayerPreviewDto> Participants { get; set; } = new();
    public List<MigrationWeekSummaryDto> WeeksSummary { get; set; } = new();
    public List<MigrationMatchSampleDto> SampleMatches { get; set; } = new();
}
