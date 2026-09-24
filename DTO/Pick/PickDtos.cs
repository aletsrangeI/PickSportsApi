using DTO.Espn;

namespace DTO.Pick;

public class SubmitPickRequestDto
{
    public int MatchId { get; set; }
    public string PickAbbr { get; set; } = null!;
}

public class PickDto
{
    public int Id { get; set; }
    public int QuinielaId { get; set; }
    public int MemberId { get; set; }
    public string? MemberAlias { get; set; }
    public int MatchId { get; set; }
    public string PickAbbr { get; set; } = null!;
    public bool IsAutoFilled { get; set; }
    public bool? IsHit { get; set; }
    public bool IsUpsetHit { get; set; }
    public bool IsHumillacion { get; set; }
    public bool IsSomnifero { get; set; }
    public bool IsEmpateFallido { get; set; }
    public DateTime Created { get; set; }
    public DateTime? LastModified { get; set; }
}

public class QuinielaMemberPickDto
{
    public int MemberId { get; set; }
    public string Alias { get; set; } = null!;
    public string? DisplayName { get; set; }
    public string Role { get; set; } = null!;
    public int TotalHits { get; set; }
    public int CurrentStreak { get; set; }
}

public class QuinielaPicksResponseDto
{
    public int QuinielaId { get; set; }
    public int WeekId { get; set; }
    public int WeekNumber { get; set; }
    public string WeekName { get; set; } = null!;
    public string Status { get; set; } = null!; // DRAFT, PUBLISHED, LOCKED, SCORED
    public DateTime? FirstGameUtc { get; set; }
    public DateTime? LockedAt { get; set; }
    public bool IsLocked { get; set; }
    public bool IsRevealed { get; set; }
    public bool AllowsDraw { get; set; } = true;
    public int CurrentUserMemberId { get; set; }
    public IEnumerable<MatchDto> Matches { get; set; } = new List<MatchDto>();
    public IEnumerable<QuinielaMemberPickDto> Members { get; set; } = new List<QuinielaMemberPickDto>();
    public IEnumerable<PickDto> Picks { get; set; } = new List<PickDto>();
}

public class LockAndAutofillResultDto
{
    public int QuinielaId { get; set; }
    public int WeekId { get; set; }
    public string WeekStatus { get; set; } = "LOCKED";
    public int AutofilledPicksCount { get; set; }
    public int MembersAffectedCount { get; set; }
    public string Message { get; set; } = string.Empty;
}
