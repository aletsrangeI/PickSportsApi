namespace DTO.Liguilla;

public class LiguillaTeamDto
{
    public int TeamId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string Abbreviation { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string? PrimaryColor { get; set; }
    public int Seed { get; set; }
}

public class LiguillaNodeDto
{
    public int Id { get; set; }
    public string Round { get; set; } = string.Empty; // "QF", "SF", "FINAL"
    public int NodeNumber { get; set; }
    public LiguillaTeamDto? TeamA { get; set; }
    public LiguillaTeamDto? TeamB { get; set; }
    public LiguillaTeamDto? WinnerTeam { get; set; }
    public bool IsResolved { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public DateTime? KickoffUtc { get; set; }
}

public class SubmitLiguillaBracketDto
{
    public int QfWinner1TeamId { get; set; }
    public int QfWinner2TeamId { get; set; }
    public int QfWinner3TeamId { get; set; }
    public int QfWinner4TeamId { get; set; }
    public int SfWinner1TeamId { get; set; }
    public int SfWinner2TeamId { get; set; }
    public int ChampionTeamId { get; set; }
}

public class LiguillaBracketDetailDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Alias { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public bool IsLocked { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public int TotalScore { get; set; }

    public int QfWinner1TeamId { get; set; }
    public int QfWinner2TeamId { get; set; }
    public int QfWinner3TeamId { get; set; }
    public int QfWinner4TeamId { get; set; }

    public int SfWinner1TeamId { get; set; }
    public int SfWinner2TeamId { get; set; }

    public int ChampionTeamId { get; set; }

    public int QfPoints { get; set; }
    public int SfPoints { get; set; }
    public int ChampionPoints { get; set; }
    public int BonusPoints { get; set; }

    public List<int> ShatteredTeamIds { get; set; } = new();
}

public class LiguillaOverviewDto
{
    public bool IsAvailable { get; set; }
    public string Status { get; set; } = "HIDDEN"; // "HIDDEN", "WAITING_PLAY_IN", "OPEN_FOR_PICKS", "LOCKED_IN_PROGRESS", "FINISHED"
    public DateTime? LockDeadlineUtc { get; set; }
    public List<LiguillaTeamDto> QualifiedTeams { get; set; } = new();
    public List<LiguillaNodeDto> Nodes { get; set; } = new();
    public LiguillaBracketDetailDto? UserBracket { get; set; }
}

public class LiguillaStandingItemDto
{
    public int Rank { get; set; }
    public int UserId { get; set; }
    public string Alias { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public int TotalPoints { get; set; }
    public int QfPoints { get; set; }
    public int SfPoints { get; set; }
    public int ChampionPoints { get; set; }
    public int BonusPoints { get; set; }
    public bool ChampionPickedCorrectly { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
}

public class LiguillaStandingsDto
{
    public int QuinielaId { get; set; }
    public string SeasonName { get; set; } = string.Empty;
    public List<LiguillaStandingItemDto> Standings { get; set; } = new();
}

public class ResolveLiguillaNodeRequestDto
{
    public int NodeId { get; set; }
    public int WinnerTeamId { get; set; }
}
