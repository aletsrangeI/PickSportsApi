namespace Interface.UseCases.Knockout;

/// <summary>Convierte el JSON del scoreboard de ESPN en cruces normalizados. Un parser por familia de respuesta.</summary>
public interface IEspnKnockoutParser
{
    /// <summary>Codigos de formato que sabe interpretar (p. ej. "NHL_BEST_OF_7").</summary>
    IReadOnlyCollection<string> SupportedFormatCodes { get; }

    /// <summary>Un cruce por serie/partido de eliminatoria; ignora eventos de temporada regular. Nunca lanza por JSON inesperado.</summary>
    IReadOnlyList<EspnKnockoutTie> ParseScoreboard(string json, string formatCode);

    /// <summary>NFL: <c>playoffSeed</c> y conferencia por EspnTeamId desde standings (solo siembras 1..7).</summary>
    IReadOnlyDictionary<string, NflPlayoffSeed> ParseNflPlayoffSeeds(string standingsJson);

    /// <summary>
    /// NHL: conferencia, division y <c>playoffSeed</c> (1..8 por conferencia) por EspnTeamId desde standings con <c>level=3</c>
    /// (<c>children[]</c> = conferencias, <c>children[].children[]</c> = divisiones). Solo equipos con seed 1..8.
    /// </summary>
    IReadOnlyDictionary<string, NhlPlayoffSeed> ParseNhlPlayoffSeeds(string standingsJson);
}

/// <summary>
/// Siembra de playoffs NHL de un equipo: conferencia ("east"/"west"), division ("atlantic"/"metropolitan"/"central"/"pacific"),
/// seed 1..8 y si es el ganador de su division. OJO: el <c>playoffSeed</c> de ESPN es un orden por puntos de la conferencia,
/// NO identifica a los ganadores de division (p. ej. 2025-26 Oeste: Central tuvo seeds 1 y 2 y el campeon de Pacifico fue seed 4).
/// <paramref name="IsDivisionWinner"/>: entre los clasificados (seed 1..8) de la division, el de mas puntos; a igualdad, el de menor seed.
/// </summary>
public record NhlPlayoffSeed(string Conference, string Division, int Seed, bool IsDivisionWinner);

/// <summary>Siembra de playoffs NFL de un equipo: conferencia ("afc"/"nfc") y seed 1..7.</summary>
public record NflPlayoffSeed(string Conference, int Seed);

