namespace Interface.UseCases.Knockout;

public interface IEspnKnockoutSyncService
{
    /// <summary>
    /// Para la temporada vigente de la liga: siembra los nodos desde ESPN, resuelve los cruces terminados con la estrategia
    /// y avanza ganadores. Devuelve cuantos nodos cambiaron. No hace nada si la liga no tiene estrategia. No lanza por fallos de ESPN.
    /// </summary>
    Task<int> SyncLeagueAsync(int leagueId, CancellationToken ct = default);
}

/// <summary>Resultado de puntuar un bracket con el motor generico.</summary>
public record KnockoutScore(
    int TotalPoints,
    int BonusPoints,
    IReadOnlyDictionary<string, int> PointsByRound,
    IReadOnlyList<int> ShatteredTeamIds);
