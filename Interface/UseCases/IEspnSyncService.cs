namespace Interface.UseCases;

public interface IEspnSyncService
{
    /// <summary>Sincroniza los partidos de una jornada específica desde ESPN.</summary>
    Task<SyncResult> SyncWeekAsync(int weekId, CancellationToken ct = default);

    /// <summary>
    /// Sincroniza TODAS las jornadas de una temporada con fallback en cascada:
    /// L1 (calendar endpoint) → L2 (scoreboard date-range) → L3 (config stubs).
    /// </summary>
    Task<SyncResult> SyncFullSeasonAsync(int seasonId, CancellationToken ct = default);

    /// <summary>Importa partidos desde un JSON pegado manualmente del navegador.</summary>
    Task<SyncResult> ImportManualJsonAsync(int weekId, string rawJson);
}

public record SyncResult(
    bool IsSuccess,
    int WeeksSynced,
    int MatchesUpserted,
    int TeamsUpserted,
    /// <summary>"L1_CALENDAR" | "L2_SCOREBOARD" | "L3_CONFIG" | "MANUAL" | "ERROR"</summary>
    string FallbackLevel,
    string? ErrorMessage
);
