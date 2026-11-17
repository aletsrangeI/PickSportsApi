using Domain.Entities;

namespace Interface.UseCases.Knockout;

/// <summary>Ronda de una fase final. <c>NodeCount</c> = cruces de la ronda; <c>WinnersToPick</c> = equipos que elige el usuario.</summary>
public record KnockoutRound(string Code, int Order, int WinnersToPick, int PointsPerHit, int NodeCount);

/// <summary>Resultado de un cruce interpretado a partir de ESPN o del admin.</summary>
public record NodeOutcome(int NodeNumber, bool IsResolved, int? WinnerTeamId);

/// <summary>
/// Estrategia de formato de eliminatoria de una liga (patron Strategy). Encapsula la forma del arbol,
/// la siembra, la fuente del ganador y el momento de apertura. El contexto (LiguillaApplication) no conoce reglamentos.
/// </summary>
public interface IKnockoutFormatStrategy
{
    /// <summary>Codigo persistido en <c>League.KnockoutFormatCode</c>.</summary>
    string FormatCode { get; }

    /// <summary>Rondas pickables en orden; la ultima es la final (su unico ganador es el campeon).</summary>
    IReadOnlyList<KnockoutRound> Rounds { get; }

    /// <summary>Bono si se aciertan todos los nodos y todos estan resueltos.</summary>
    int PerfectBonusPoints { get; }

    /// <summary>Primera ronda en la que el usuario elige (en Champions es "R16").</summary>
    string FirstPickableRoundCode { get; }

    /// <summary>True cuando la fase regular termino y el campo de la primera ronda pickable esta completo.</summary>
    bool IsFieldReady(IReadOnlyList<LiguillaNode> nodes, bool regularSeasonFinished);

    /// <summary>Equipos entre los que se puede elegir en <paramref name="roundCode"/>, dados los picks de la ronda anterior.</summary>
    IReadOnlyList<int> EligibleTeamIds(
        string roundCode,
        IReadOnlyList<LiguillaNode> nodes,
        IReadOnlyDictionary<string, IReadOnlyList<int>> previousPicks);

    /// <summary>Lanza <see cref="ArgumentException"/> con el mensaje de coherencia si los picks no son validos.</summary>
    void ValidatePicks(
        IReadOnlyList<LiguillaNode> nodes,
        IReadOnlyDictionary<string, IReadOnlyList<int>> picksByRound);

    /// <summary>Siembra la ronda siguiente con los nodos ya resueltos. Devuelve solo los nodos que modifico.</summary>
    IReadOnlyList<LiguillaNode> AdvanceWinners(IReadOnlyList<LiguillaNode> allNodes);

    /// <summary>Interpreta un cruce de ESPN. Nunca lanza: si no hay ganador firme devuelve IsResolved=false.</summary>
    NodeOutcome ResolveFromEspn(LiguillaNode node, EspnKnockoutTie tie);
}
