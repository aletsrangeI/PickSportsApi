namespace DTO.CustomEvents;

public class EventRoundSummaryDto
{
    public int Id { get; set; }
    public int EventGroupId { get; set; }
    public int QuinielaId { get; set; }
    public string Title { get; set; } = null!;
    public int RoundOrder { get; set; }
    public DateTime LockDateUtc { get; set; }
    public string Status { get; set; } = "Open"; // Draft | Open | Locked | Resolved
    public DateTime? ResolvedDateUtc { get; set; }
    public bool IsLocked => Status == "Locked" || Status == "Resolved" || DateTime.UtcNow >= LockDateUtc;
    public int QuestionsCount { get; set; }
    public int UserPicksCount { get; set; }
    public int TotalPossiblePoints { get; set; }
    public int UserPointsEarned { get; set; }
}

public class EventOptionDto
{
    public int Id { get; set; }
    public int EventQuestionId { get; set; }
    public string OptionText { get; set; } = null!;
    public string? ImageUrl { get; set; }
    public bool? IsCorrect { get; set; }
    public int OptionOrder { get; set; }
    public int PicksCount { get; set; }
    public decimal PickPercentage { get; set; }
}

public class EventQuestionDto
{
    public int Id { get; set; }
    public int EventRoundId { get; set; }
    public string QuestionText { get; set; } = null!;
    public string? Description { get; set; }
    public int Points { get; set; } = 1;
    public string QuestionType { get; set; } = "SingleChoice";
    public int QuestionOrder { get; set; }
    public List<EventOptionDto> Options { get; set; } = new();
    public int? UserSelectedOptionId { get; set; }
    public bool? UserIsHit { get; set; }
    public int UserPointsEarned { get; set; }
}

public class EventMemberPickDto
{
    public int MemberId { get; set; }
    public string MemberAlias { get; set; } = null!;
    public string? AvatarUrl { get; set; }
    public int EventQuestionId { get; set; }
    public int EventOptionId { get; set; }
    public string OptionText { get; set; } = null!;
    public bool? IsHit { get; set; }
    public int PointsEarned { get; set; }
    public DateTime SubmittedAtUtc { get; set; }
}

public class EventRoundDetailDto
{
    public int Id { get; set; }
    public int EventGroupId { get; set; }
    public int QuinielaId { get; set; }
    public string Title { get; set; } = null!;
    public int RoundOrder { get; set; }
    public DateTime LockDateUtc { get; set; }
    public string Status { get; set; } = "Open";
    public DateTime? ResolvedDateUtc { get; set; }
    public bool IsLocked => Status == "Locked" || Status == "Resolved" || DateTime.UtcNow >= LockDateUtc;
    public bool IsRevealed => IsLocked;
    public int? CurrentUserMemberId { get; set; }
    public bool IsOwnerOrAdmin { get; set; }
    public List<EventQuestionDto> Questions { get; set; } = new();
    public List<EventMemberPickDto> AllMemberPicks { get; set; } = new();
}

public class CreateEventRoundDto
{
    public string Title { get; set; } = null!;
    public DateTime LockDateUtc { get; set; }
    public int? RoundOrder { get; set; }
}

public class UpdateEventRoundDto
{
    public string Title { get; set; } = null!;
    public DateTime LockDateUtc { get; set; }
    public string? Status { get; set; }
}

public class CreateEventOptionItemDto
{
    public string OptionText { get; set; } = null!;
    public string? ImageUrl { get; set; }
    public int OptionOrder { get; set; }
}

public class CreateEventQuestionDto
{
    public string QuestionText { get; set; } = null!;
    public string? Description { get; set; }
    public int Points { get; set; } = 1;
    public string QuestionType { get; set; } = "SingleChoice";
    public int? QuestionOrder { get; set; }
    public List<CreateEventOptionItemDto> Options { get; set; } = new();
}

public class EventPickSelectionDto
{
    public int EventQuestionId { get; set; }
    public int EventOptionId { get; set; }
}

public class SubmitEventPicksDto
{
    public List<EventPickSelectionDto> Picks { get; set; } = new();
}

public class QuestionResolutionItemDto
{
    public int EventQuestionId { get; set; }
    public int WinningOptionId { get; set; }
}

public class ResolveEventRoundDto
{
    public List<QuestionResolutionItemDto> Resolutions { get; set; } = new();
}

public class EventStandingRowDto
{
    public int Rank { get; set; }
    public int MemberId { get; set; }
    public string Alias { get; set; } = null!;
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public int Points { get; set; }
    public int Hits { get; set; }
    public int TotalQuestions { get; set; }
    public int CurrentStreak { get; set; }
    public int BestStreak { get; set; }
}

public class EventStandingsResponseDto
{
    public int QuinielaId { get; set; }
    public int? RoundId { get; set; }
    public string? RoundTitle { get; set; }
    public string? RoundStatus { get; set; }
    public List<EventStandingRowDto> RoundStandings { get; set; } = new();
    public List<EventStandingRowDto> GeneralStandings { get; set; } = new();
}
