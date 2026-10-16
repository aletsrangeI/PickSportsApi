namespace DTO.Challenges;

public class CreateChallengeDto
{
    public List<int> InvitedMemberIds { get; set; } = new();
    public string ChallengeType { get; set; } = string.Empty;
    public string PayoutMode { get; set; } = "WINNER_TAKES_ALL";
    public string StakesDescription { get; set; } = string.Empty;
    public int? TargetMatchId { get; set; }

    public string? CreatorPredictedWinnerAbbr { get; set; }
    public int? CreatorPredictedHomeScore { get; set; }
    public int? CreatorPredictedAwayScore { get; set; }
    public int? CreatorPredictedTotalGoals { get; set; }

    // Accesores de compatibilidad
    public string? PredictedWinnerAbbr
    {
        get => CreatorPredictedWinnerAbbr;
        set => CreatorPredictedWinnerAbbr = value;
    }

    public int? PredictedHomeScore
    {
        get => CreatorPredictedHomeScore;
        set => CreatorPredictedHomeScore = value;
    }

    public int? PredictedAwayScore
    {
        get => CreatorPredictedAwayScore;
        set => CreatorPredictedAwayScore = value;
    }

    public int? PredictedTotalGoals
    {
        get => CreatorPredictedTotalGoals;
        set => CreatorPredictedTotalGoals = value;
    }
}

public class RespondChallengeDto
{
    public string Action { get; set; } = "ACCEPT"; // ACCEPT | DECLINE

    public string? Response
    {
        get => Action;
        set => Action = value ?? string.Empty;
    }

    public string? PredictedWinnerAbbr { get; set; }
    public int? PredictedHomeScore { get; set; }
    public int? PredictedAwayScore { get; set; }
    public int? PredictedTotalGoals { get; set; }
}

public class ChallengeParticipantDto
{
    public int Id { get; set; }
    public int ChallengeId { get; set; }
    public int MemberId { get; set; }
    public string Alias { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public bool IsCreator { get; set; }
    public string Status { get; set; } = "PENDING"; // ACCEPTED, PENDING, DECLINED, EXPIRED
    public string? PredictedWinnerAbbr { get; set; }
    public int? PredictedHomeScore { get; set; }
    public int? PredictedAwayScore { get; set; }
    public int? PredictedTotalGoals { get; set; }
    public string Outcome { get; set; } = "NONE"; // NONE, WINNER, LOSER, NO_WINNER_BUST
    public string? CurrentLiveValue { get; set; }
    public bool IsBusted { get; set; }
    public int? EliminatedAtMatchId { get; set; }
    public string? EliminatedAtMatchLabel { get; set; }
    public bool IsDebtSettled { get; set; }
    public DateTime? SettledAtUtc { get; set; }
    public DateTime? RespondedAtUtc { get; set; }
}

public class ChallengeDto
{
    public int Id { get; set; }
    public int QuinielaId { get; set; }
    public int WeekId { get; set; }
    public string WeekName { get; set; } = string.Empty;
    public int CreatorMemberId { get; set; }
    public string CreatorAlias { get; set; } = string.Empty;
    public string? CreatorAvatarUrl { get; set; }
    public string ChallengeType { get; set; } = string.Empty;
    public string PayoutMode { get; set; } = "WINNER_TAKES_ALL";
    public string StakesDescription { get; set; } = string.Empty;
    public int? TargetMatchId { get; set; }
    public string? TargetMatchLabel { get; set; }
    public string? TargetMatchScore { get; set; }
    public string Status { get; set; } = "PENDING_ACCEPTANCE";
    public string? ResolutionSummary { get; set; }
    public int? ActualTotalGoals { get; set; }

    public int? CurrentTotalGoals
    {
        get => ActualTotalGoals;
        set => ActualTotalGoals = value;
    }

    public List<string> DoubleLifeEvents { get; set; } = new();
    public List<string> DifferentialMatches { get; set; } = new();
    public List<string> TakenOptions { get; set; } = new();
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public List<ChallengeParticipantDto> Participants { get; set; } = new();
}

public sealed class ChallengeResolutionResult
{
    public string FinalStatus { get; init; } = "RESOLVED_WITH_WINNER"; // RESOLVED_WITH_WINNER | RESOLVED_NO_WINNER
    public IReadOnlyList<int> WinnerMemberIds { get; init; } = Array.Empty<int>();
    public IReadOnlyList<int> LoserMemberIds { get; init; } = Array.Empty<int>();
    public IReadOnlyDictionary<int, string> ParticipantOutcomes { get; init; } = new Dictionary<int, string>();
    public IReadOnlyDictionary<int, int?> EliminatedAtMatchIds { get; init; } = new Dictionary<int, int?>();
    public IReadOnlyList<string> DoubleLifeEvents { get; init; } = Array.Empty<string>();
    public string ResolutionSummary { get; init; } = string.Empty;
    public int? ActualTotalGoals { get; init; }
}
