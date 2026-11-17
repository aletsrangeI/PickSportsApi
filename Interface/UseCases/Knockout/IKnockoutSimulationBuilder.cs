namespace Interface.UseCases.Knockout;

/// <summary>
/// Cruce de primera ronda (o equipo con descanso) de una simulacion de QA calculada con la tabla de ESPN de HOY.
/// Los ids son los de ESPN (<c>Team.EspnTeamId</c>). <paramref name="EspnTeamIdB"/> nulo = equipo con descanso que entra directo
/// (NFL: el seed 1 de cada conferencia en el primer nodo de Divisional).
/// </summary>
public record SimulatedTie(
    int NodeNumber,
    string RoundCode,
    string? Conference,
    string EspnTeamIdA,
    int SeedA,
    string? EspnTeamIdB,
    int? SeedB);

/// <summary>
/// Strategy de simulacion de QA por formato: arma el campo de la primera ronda pickable a partir del JSON de standings de ESPN.
/// No tiene efectos secundarios ni acceso a base de datos: el servicio de simulacion descarga el JSON, resuelve los equipos y guarda los nodos.
/// </summary>
public interface IKnockoutSimulationBuilder
{
    /// <summary>Codigo de formato que simula (<c>KnockoutFormatCodes</c>).</summary>
    string FormatCode { get; }

    /// <summary>Ruta relativa del endpoint de standings de ESPN para el formato; sin dominio, p. ej. "apis/v2/sports/hockey/nhl/standings?level=3".</summary>
    string StandingsPath(string espnApiPath);

    /// <summary>
    /// Cruces de la primera ronda pickable con la numeracion de nodos del formato (ver 00-common.md). Devuelve lista vacia si el JSON es invalido
    /// o no alcanzan equipos. Nunca lanza por JSON inesperado.
    /// </summary>
    IReadOnlyList<SimulatedTie> Build(string standingsJson);
}

/// <summary>Simulacion de QA de un formato de eliminatoria con la tabla actual de ESPN (generaliza el boton "Sembrar Top 8" de Liga MX).</summary>
public interface IKnockoutSimulationService
{
    /// <summary>
    /// Reemplaza los nodos de la temporada de la quiniela por una primera ronda simulada con los standings de hoy y rondas siguientes vacias,
    /// con kickoffs futuros. Devuelve cuantos nodos quedaron. Lanza <see cref="InvalidOperationException"/> si la liga no tiene formato,
    /// si el formato no tiene simulacion, si no hay temporada o si ESPN no devuelve equipos suficientes.
    /// </summary>
    Task<int> SeedAsync(int quinielaId, CancellationToken ct = default);
}
