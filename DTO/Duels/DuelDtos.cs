namespace DTO.Duels;

public class DuelParticipantDto
{
    public int MemberId { get; set; }
    public int UserId { get; set; }
    public string Alias { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    /// <summary>Titulo honorifico equipado (nombre, no codigo).</summary>
    public string? EquippedTitle { get; set; }
}

public class DuelFixtureDto
{
    public int Id { get; set; }
    public int WeekNumber { get; set; }
    public int Round { get; set; }
    public int Leg { get; set; }
    public DuelParticipantDto Home { get; set; } = null!;
    /// <summary>Null = Bye (Home descansa).</summary>
    public DuelParticipantDto? Away { get; set; }
    public int HomeHits { get; set; }
    public int AwayHits { get; set; }
    /// <summary>"HOME" | "AWAY" | "DRAW" | "BYE"; null = pendiente.</summary>
    public string? Result { get; set; }
    public bool IsResolved { get; set; }
    public bool WonByAdvantage { get; set; }
    public int? HomeRankBefore { get; set; }
    public int? AwayRankBefore { get; set; }
}

public class DuelScheduleDto
{
    public int QuinielaId { get; set; }
    public int TotalWeeks { get; set; }
    public int RoundsPerLeg { get; set; }
    public int Legs { get; set; }
    public List<int> DuelWeeks { get; set; } = new();
    public List<int> RestWeeks { get; set; } = new();
    public DateTime? GeneratedAt { get; set; }
    public bool IsClosed { get; set; }
    public int? ChampionMemberId { get; set; }
    public string? CupName { get; set; }
    public string? PunishmentText { get; set; }
}

public class DuelStandingDto
{
    public int Rank { get; set; }
    public DuelParticipantDto Participant { get; set; } = null!;
    public int Played { get; set; }
    public int Won { get; set; }
    public int Drawn { get; set; }
    public int Lost { get; set; }
    public int Points { get; set; }
    public int SeasonHits { get; set; }
}

public class DuelMatchupMatchDto
{
    public int MatchId { get; set; }
    public string HomeTeam { get; set; } = string.Empty;
    public string AwayTeam { get; set; } = string.Empty;
    public string? HomePick { get; set; }
    public string? AwayPick { get; set; }
    /// <summary>True si los dos pronosticaron distinto (pick diferencial).</summary>
    public bool Differential { get; set; }
    public bool IsFinished { get; set; }
    /// <summary>"HOME" | "AWAY" | null: quien acerto, una vez terminado el partido.</summary>
    public string? Winner { get; set; }
}

public class DuelMatchupDto
{
    public DuelFixtureDto Fixture { get; set; } = null!;
    public List<DuelMatchupMatchDto> Matches { get; set; } = new();
    /// <summary>"HOME" | "AWAY" | null: quien gana el empate por Ventaja del Retador (calculado en vivo).</summary>
    public string? ChallengerAdvantage { get; set; }
}

public class HallOfFameEntryDto
{
    public int QuinielaId { get; set; }
    public string QuinielaName { get; set; } = string.Empty;
    public int? SeasonId { get; set; }
    public string? SeasonName { get; set; }
    public DuelParticipantDto Champion { get; set; } = null!;
    /// <summary>Record G-E-P, p. ej. "12-3-2".</summary>
    public string Record { get; set; } = string.Empty;
    public int Points { get; set; }
    public string? CupName { get; set; }
    public string? PunishmentText { get; set; }
    public string Title { get; set; } = "Gladiador Legendario";
    /// <summary>True si quien consulta es el campeon (puede editar la copa).</summary>
    public bool CanEditCup { get; set; }
}

public class UpdateDuelCupDto
{
    public string? CupName { get; set; }
    public string? PunishmentText { get; set; }
}
