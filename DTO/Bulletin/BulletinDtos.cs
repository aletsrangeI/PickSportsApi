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
    public NextWeekInfoDto? NextWeekInfo { get; set; }
}

public class UpdateAnnouncementDto
{
    public string? Announcement { get; set; }
}
