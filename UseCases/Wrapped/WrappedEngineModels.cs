namespace UseCases.Wrapped;

// SPEC-014 - CONTRATO read-only para A1 (motor) y A2 (aplicacion). El motor es PURO: sin IUnitOfWork ni EF.

public record WrappedMemberInfo(
    int MemberId,
    int UserId,
    string DisplayName,
    string? AvatarUrl,
    int TotalPoints,
    int TotalHits,
    int TotalUpsets,
    int TotalHumillaciones,
    int BestStreak);

public record WrappedMatchInfo(
    int MatchId,
    int WeekId,
    int WeekNumber,
    DateTime DateUtc,
    DateTime? WeekFirstGameUtc,
    string HomeAbbr,
    string AwayAbbr,
    string HomeName,
    string AwayName,
    int HomeScore,
    int AwayScore,
    string? WinnerAbbr,
    bool IsUpset,
    string? DerbyName);

public record WrappedPickInfo(
    int MemberId,
    int MatchId,
    string PickAbbr,
    bool IsAutoFilled,
    bool? IsHit,
    bool IsUpsetHit,
    bool IsHumillacion,
    bool IsSomnifero,
    bool IsEmpateFallido,
    DateTime SubmittedUtc,
    // Momento en que se guardó el pick por primera vez. SubmittedUtc es la última modificación, que el cálculo de
    // puntos también actualiza DESPUÉS del partido; por eso no sirve para medir con cuánta anticipación se votó.
    DateTime? CreatedUtc = null);

public record WrappedAwardInfo(int MemberId, int WeekId, string AwardType);

public record WrappedEngineInput(
    int SeasonId,
    string SeasonName,
    int TargetMemberId,
    IReadOnlyList<WrappedMemberInfo> Members,
    IReadOnlyList<WrappedMatchInfo> Matches,
    IReadOnlyList<WrappedPickInfo> Picks,
    IReadOnlyList<WrappedAwardInfo> Awards);
