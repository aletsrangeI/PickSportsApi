namespace DTO.Migration;

public class XlsxConfig
{
    public int SeasonYear { get; set; } = 2026;
    public int SeasonType { get; set; } = 1;
    public string BaseSiteUrl { get; set; } = string.Empty;
    public int CurrentWeek { get; set; } = 10;
    public string FormId { get; set; } = string.Empty;
}

public class XlsxWeek
{
    public int WeekNumber { get; set; }
    public string StartDateRaw { get; set; } = string.Empty;
    public string EndDateRaw { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class XlsxControlJornada
{
    public int WeekNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? PublishedAt { get; set; }
    public DateTime? LockedAt { get; set; }
    public DateTime? ScoredAt { get; set; }
    public string? LastAction { get; set; }
    public string? LastNote { get; set; }
}

public class XlsxMatch
{
    public int SeasonYear { get; set; } = 2026;
    public int SeasonType { get; set; } = 1;
    public int WeekNumber { get; set; }
    public string EspnGameId { get; set; } = string.Empty;
    public DateTime DateUtc { get; set; }
    public string HomeTeamEspnId { get; set; } = string.Empty;
    public string HomeAbbr { get; set; } = string.Empty;
    public string HomeName { get; set; } = string.Empty;
    public string AwayTeamEspnId { get; set; } = string.Empty;
    public string AwayAbbr { get; set; } = string.Empty;
    public string AwayName { get; set; } = string.Empty;
    public string StatusState { get; set; } = "pre";
    public string? StatusDesc { get; set; }
    public int HomeScore { get; set; }
    public int AwayScore { get; set; }
    public string? WinnerAbbr { get; set; }
    public string? Broadcast { get; set; }
    public string? Venue { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
}

public class XlsxPick
{
    public string PlayerAlias { get; set; } = string.Empty;
    public string EspnGameId { get; set; } = string.Empty;
    public string PickAbbr { get; set; } = string.Empty;
}

public class XlsxStanding
{
    public string PlayerAlias { get; set; } = string.Empty;
    public int Hits { get; set; }
    public int TotalPicks { get; set; }
    public decimal Pct { get; set; }
}

public class XlsxMigrationData
{
    public XlsxConfig Config { get; set; } = new();
    public List<XlsxWeek> Weeks { get; set; } = new();
    public List<XlsxControlJornada> ControlJornadas { get; set; } = new();
    public List<XlsxMatch> Matches { get; set; } = new();
    public List<XlsxPick> Picks { get; set; } = new();
    public List<XlsxStanding> Standings { get; set; } = new();
}
