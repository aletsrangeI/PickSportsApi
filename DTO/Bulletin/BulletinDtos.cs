namespace DTO.Bulletin;

public class PodiumMemberDto
{
    public int Position { get; set; }
    public int MemberId { get; set; }
    public int UserId { get; set; }
    public string Alias { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public int Hits { get; set; }
    public int TotalPicks { get; set; }
    public decimal AccuracyPct { get; set; }
    public int UpsetHits { get; set; }
    public int Humillaciones { get; set; }
}

public class BulletinWinnerDto
{
    public int MemberId { get; set; }
    public string Alias { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public string Value { get; set; } = string.Empty;
    public string? SecondaryValue { get; set; }
    public string? Notes { get; set; }
}

public class BulletinMatchAwardDto
{
    public string MatchLabel { get; set; } = string.Empty;
    public string? WinnerAbbr { get; set; }
    public decimal AccuracyPct { get; set; }
    public int CorrectPicks { get; set; }
    public int TotalPicks { get; set; }
    public string? Notes { get; set; }
}

public class BulletinAwardsDto
{
    public List<BulletinWinnerDto> Mvp { get; set; } = new();
    public List<BulletinWinnerDto> SurpriseKing { get; set; } = new();
    public List<BulletinWinnerDto> Humillado { get; set; } = new();
    public List<BulletinWinnerDto> Somnifero { get; set; } = new();
    public List<BulletinWinnerDto> EmpateFallido { get; set; } = new();
    public BulletinMatchAwardDto? RompeQuinielas { get; set; }
}

public class NextWeekInfoDto
{
    public int WeekId { get; set; }
    public int WeekNumber { get; set; }
    public string WeekName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? FirstGameUtc { get; set; }
}

public class RadarMoverDto
{
    public int MemberId { get; set; }
    public string Alias { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public int PreviousRank { get; set; }
    public int CurrentRank { get; set; }
    public int PositionsDelta { get; set; } // Positivo = subió, negativo = bajó
    public int WeeklyHits { get; set; }
    public int TiedCount { get; set; } // Otros miembros con el mismo delta
}

public class BulletinRadarDto
{
    public RadarMoverDto? Climber { get; set; }
    public RadarMoverDto? Faller { get; set; }
}

public class BulletinFavoriteDto
{
    public string TeamName { get; set; } = string.Empty;
    public string TeamAbbr { get; set; } = string.Empty;
    public string? TeamLogoUrl { get; set; }
    public string MatchLabel { get; set; } = string.Empty;
    public decimal PickPct { get; set; }
    public int PickCount { get; set; }
    public int TotalPicks { get; set; }
    public bool Won { get; set; }
}

public class BulletinPulseDto
{
    public decimal CommunityAccuracyPct { get; set; }
    public int TotalHits { get; set; }
    public int TotalPicks { get; set; }
    public BulletinFavoriteDto? Favorite { get; set; }
}

public class WeeklyBulletinDto
{
    public int QuinielaId { get; set; }
    public int WeekId { get; set; }
    public int WeekNumber { get; set; }
    public string WeekName { get; set; } = string.Empty;
    public string WeekStatus { get; set; } = string.Empty; // DRAFT, PUBLISHED, LOCKED, SCORED
    public bool IsOfficial { get; set; }
    public DateTime WeekEndDate { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public string? AdminAnnouncement { get; set; }
    public List<PodiumMemberDto> Podium { get; set; } = new();
    public BulletinAwardsDto Awards { get; set; } = new();
    public BulletinRadarDto? Radar { get; set; }
    public BulletinPulseDto? Pulse { get; set; }
    public NextWeekInfoDto? NextWeekInfo { get; set; }
}

public class UpdateAnnouncementDto
{
    public string? Announcement { get; set; }
}
