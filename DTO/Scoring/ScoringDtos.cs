namespace DTO.Scoring;

public class MemberStandingDto
{
    public int Rank { get; set; }
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
    public int Somniferos { get; set; }
    public int EmpatesFallidos { get; set; }
    public int CurrentStreak { get; set; }
    public int BestStreak { get; set; }
}

public class QuinielaStandingsResponseDto
{
    public int QuinielaId { get; set; }
    public int WeekId { get; set; }
    public int WeekNumber { get; set; }
    public string WeekName { get; set; } = string.Empty;
    public string WeekStatus { get; set; } = string.Empty; // DRAFT, PUBLISHED, LOCKED, SCORED
    public int FinishedMatchesCount { get; set; }
    public int TotalMatchesCount { get; set; }
    public List<MemberStandingDto> WeeklyStandings { get; set; } = new();
    public List<MemberStandingDto> GeneralStandings { get; set; } = new();
}

public class WeeklyAwardDto
{
    public int Id { get; set; }
    public int QuinielaId { get; set; }
    public int WeekId { get; set; }
    public int MemberId { get; set; }
    public string MemberAlias { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public string AwardType { get; set; } = string.Empty; // MVP, REY_SORPRESAS, HUMILLADO, SOMNIFERO, EMPATE_FALLIDO, PARTIDO_DIFICIL
    public string AwardValue1 { get; set; } = string.Empty;
    public string? AwardValue2 { get; set; }
    public string? Notes { get; set; }
}

public class ScoreWeekResultDto
{
    public int QuinielaId { get; set; }
    public int WeekId { get; set; }
    public int ScoredMatchesCount { get; set; }
    public int PicksEvaluatedCount { get; set; }
    public int AwardsGeneratedCount { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class WhatsAppTextReportDto
{
    public string ReportType { get; set; } = string.Empty; // REMINDER, SUMMARY, PRIZE_POOL, PLAYER
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
