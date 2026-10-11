namespace DTO.Bulletin;

public class LeagueDerbyDto
{
    public string DerbyName { get; set; } = string.Empty;
    public string MatchLabel { get; set; } = string.Empty;
    public string ScoreLabel { get; set; } = string.Empty;
    public string? WinnerAbbr { get; set; }
}

public class TeamStreakDto
{
    public string TeamName { get; set; } = string.Empty;
    public string TeamAbbr { get; set; } = string.Empty;
    public string StreakType { get; set; } = string.Empty; // "WIN_STREAK", "LOSS_STREAK", "UNBEATEN"
    public int StreakCount { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool IsBroken { get; set; }
}

public class BulletinNarrativeContextDto
{
    public int WeekNumber { get; set; }
    public string SeasonName { get; set; } = string.Empty;
    public decimal CommunityAccuracyPct { get; set; }
    public List<PodiumMemberDto> Podium { get; set; } = new();
    public BulletinFavoriteDto? Favorite { get; set; }
    public BulletinMatchAwardDto? RompeQuinielas { get; set; }
    public List<BulletinWinnerDto> SurpriseKing { get; set; } = new();
    public List<BulletinWinnerDto> Humillado { get; set; } = new();
    public List<BulletinWinnerDto> Somnifero { get; set; } = new();
    public List<BulletinWinnerDto> EmpateFallido { get; set; } = new();
    public RadarMoverDto? Climber { get; set; }
    public RadarMoverDto? Faller { get; set; }
    public List<LeagueDerbyDto> Derbies { get; set; } = new();
    public List<TeamStreakDto> Streaks { get; set; } = new();
    public List<string> FinishedMatchSummaries { get; set; } = new();
}

public class BulletinNarrativeResultDto
{
    public string Headline { get; set; } = string.Empty;
    public string Chronicle { get; set; } = string.Empty;
    public bool GeneratedByAi { get; set; }
    public string NarrativeModel { get; set; } = string.Empty;
}
