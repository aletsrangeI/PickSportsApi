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
}

/// <summary>Siembra de playoffs NFL de un equipo: conferencia ("afc"/"nfc") y seed 1..7.</summary>
public record NflPlayoffSeed(string Conference, int Seed);

