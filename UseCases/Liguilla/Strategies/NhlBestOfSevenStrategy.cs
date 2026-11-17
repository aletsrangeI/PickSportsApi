using Domain.Common;
using Domain.Entities;
using Interface.UseCases.Knockout;

namespace UseCases.Liguilla.Strategies;

/// <summary>
/// Playoffs de la NHL: 16 equipos, series al mejor de siete y llave FIJA sin re-siembra.
/// Los nodos 2k-1 y 2k de una ronda alimentan el nodo k de la ronda siguiente.
/// </summary>
public sealed class NhlBestOfSevenStrategy : IKnockoutFormatStrategy
{
    public const string FormatCodeValue = KnockoutFormatCodes.NhlBestOf7;

    private const string FirstRoundCode = "R1";
    private const string SecondRoundCode = "R2";
    private const string ConferenceFinalCode = "CF";
    private const string FinalCode = "FINAL";

    private const int FirstRoundPointsPerHit = 10;
    private const int SecondRoundPointsPerHit = 20;
    private const int ConferenceFinalPointsPerHit = 30;
    private const int FinalPointsPerHit = 50;
    private const int PerfectBonus = 20;

    private const int FirstRoundNodeCount = 8;
    private const int SecondRoundNodeCount = 4;
    private const int ConferenceFinalNodeCount = 2;
    private const int FinalNodeCount = 1;

    /// <summary>Victorias necesarias para ganar una serie al mejor de siete.</summary>
    private const int WinsToAdvance = 4;

    private static readonly IReadOnlyList<KnockoutRound> RoundDefinitions = new List<KnockoutRound>
    {
        new(FirstRoundCode, 1, FirstRoundNodeCount, FirstRoundPointsPerHit, FirstRoundNodeCount),
        new(SecondRoundCode, 2, SecondRoundNodeCount, SecondRoundPointsPerHit, SecondRoundNodeCount),
        new(ConferenceFinalCode, 3, ConferenceFinalNodeCount, ConferenceFinalPointsPerHit, ConferenceFinalNodeCount),
        new(FinalCode, 4, FinalNodeCount, FinalPointsPerHit, FinalNodeCount)
    };

    /// <inheritdoc />
    public string FormatCode => FormatCodeValue;

    /// <inheritdoc />
    public IReadOnlyList<KnockoutRound> Rounds => RoundDefinitions;

    /// <inheritdoc />
    public int PerfectBonusPoints => PerfectBonus;

    /// <inheritdoc />
    public string FirstPickableRoundCode => FirstRoundCode;

    /// <inheritdoc />
    public bool IsFieldReady(IReadOnlyList<LiguillaNode> nodes, bool regularSeasonFinished)
    {
        if (!regularSeasonFinished)
        {
            return false;
        }

        var firstRound = NodesOfRound(nodes, FirstRoundCode);

        return firstRound.Count == FirstRoundNodeCount
               && firstRound.All(node => node.TeamAId is not null && node.TeamBId is not null);
    }

    /// <inheritdoc />
    public IReadOnlyList<int> EligibleTeamIds(
        string roundCode,
        IReadOnlyList<LiguillaNode> nodes,
        IReadOnlyDictionary<string, IReadOnlyList<int>> previousPicks)
    {
        var roundIndex = IndexOfRound(roundCode);
        if (roundIndex < 0)
        {
            return Array.Empty<int>();
        }

        if (roundIndex == 0)
        {
            return FieldTeamIds(nodes, roundCode).ToList();
        }

        // Solo equipos que existen en la llave: un pick ajeno al cuadro no puede avanzar.
        var firstRoundSlots = FirstRoundSlotByTeam(nodes);

        return PicksOfRound(previousPicks, RoundDefinitions[roundIndex - 1].Code)
            .Where(firstRoundSlots.ContainsKey)
            .Distinct()
            .ToList();
    }

    /// <inheritdoc />
    public void ValidatePicks(
        IReadOnlyList<LiguillaNode> nodes,
        IReadOnlyDictionary<string, IReadOnlyList<int>> picksByRound)
    {
        for (var roundIndex = 0; roundIndex < RoundDefinitions.Count; roundIndex++)
        {
            var round = RoundDefinitions[roundIndex];
            var picks = PicksOfRound(picksByRound, round.Code);

            if (picks.Count != round.WinnersToPick || picks.Distinct().Count() != picks.Count)
            {
                throw new ArgumentException($"Debes elegir {round.WinnersToPick} ganadores distintos en {round.Code}.");
            }

            var allowedTeams = roundIndex == 0
                ? FieldTeamIds(nodes, round.Code).ToHashSet()
                : PicksOfRound(picksByRound, RoundDefinitions[roundIndex - 1].Code).ToHashSet();

            if (!picks.All(allowedTeams.Contains))
            {
                var isLastRound = roundIndex == RoundDefinitions.Count - 1;
                throw isLastRound
                    ? new ArgumentException("El Campeón debe ser uno de los finalistas elegidos.")
                    : new ArgumentException($"Los equipos de {round.Code} deben haber sido elegidos en la ronda anterior.");
            }

            ValidateOnePickPerSeries(nodes, round, roundIndex, picks);
        }
    }

    /// <summary>
    /// La llave es fija: la llave de un equipo en la ronda r es su posicion en R1 desplazada r bits.
    /// Exige un ganador por llave; en CF eso equivale a un finalista del Este y uno del Oeste.
    /// </summary>
    private static void ValidateOnePickPerSeries(
        IReadOnlyList<LiguillaNode> nodes,
        KnockoutRound round,
        int roundIndex,
        IReadOnlyList<int> picks)
    {
        var firstRoundSlots = FirstRoundSlotByTeam(nodes);
        var seriesOfPicks = picks
            .Select(teamId => firstRoundSlots.TryGetValue(teamId, out var slot) ? slot >> roundIndex : -1)
            .ToList();

        var onePerSeries = seriesOfPicks.Distinct().Count() == round.NodeCount
                           && seriesOfPicks.All(series => series >= 0 && series < round.NodeCount);
        if (onePerSeries)
        {
            return;
        }

        if (round.Code == ConferenceFinalCode)
        {
            throw new ArgumentException("Los dos finalistas deben ser uno del Este y uno del Oeste.");
        }

        throw new ArgumentException($"Debes elegir un ganador por cada llave de {round.Code}.");
    }

    /// <summary>Posicion (0-7) del cruce de R1 donde juega cada equipo.</summary>
    private static Dictionary<int, int> FirstRoundSlotByTeam(IReadOnlyList<LiguillaNode> nodes)
    {
        var slots = new Dictionary<int, int>();
        var firstRound = NodesOfRound(nodes, FirstRoundCode);

        for (var position = 0; position < firstRound.Count; position++)
        {
            foreach (var teamId in new[] { firstRound[position].TeamAId, firstRound[position].TeamBId }.OfType<int>())
            {
                slots[teamId] = position;
            }
        }

        return slots;
    }

    /// <inheritdoc />
    public IReadOnlyList<LiguillaNode> AdvanceWinners(IReadOnlyList<LiguillaNode> allNodes)
    {
        var modified = new List<LiguillaNode>();

        for (var roundIndex = 0; roundIndex < RoundDefinitions.Count - 1; roundIndex++)
        {
            var sourceNodes = NodesOfRound(allNodes, RoundDefinitions[roundIndex].Code);
            var targetNodes = NodesOfRound(allNodes, RoundDefinitions[roundIndex + 1].Code);

            for (var position = 0; position < sourceNodes.Count; position++)
            {
                var source = sourceNodes[position];
                var targetIndex = position / 2;

                if (!source.IsResolved || source.WinnerTeamId is null || targetIndex >= targetNodes.Count)
                {
                    continue;
                }

                var target = targetNodes[targetIndex];
                if (target.IsResolved)
                {
                    continue;
                }

                if (PlaceWinner(source, target, isSlotA: position % 2 == 0) && !modified.Contains(target))
                {
                    modified.Add(target);
                }
            }
        }

        return modified;
    }

    /// <inheritdoc />
    public NodeOutcome ResolveFromEspn(LiguillaNode node, EspnKnockoutTie tie)
    {
        // El campo "winner" de la serie llega vacio en ESPN: se decide por el equipo con 4 victorias.
        var winnerEspnId = tie.Wins
            .Where(entry => entry.Value >= WinsToAdvance)
            .Select(entry => entry.Key)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(winnerEspnId))
        {
            if (string.Equals(node.TeamA?.EspnTeamId, winnerEspnId, StringComparison.Ordinal))
            {
                return new NodeOutcome(node.NodeNumber, true, node.TeamAId);
            }

            if (string.Equals(node.TeamB?.EspnTeamId, winnerEspnId, StringComparison.Ordinal))
            {
                return new NodeOutcome(node.NodeNumber, true, node.TeamBId);
            }
        }

        return new NodeOutcome(node.NodeNumber, false, null);
    }

    /// <summary>Copia el ganador de <paramref name="source"/> (equipo y siembra) al slot indicado de <paramref name="target"/>. True si cambio algo.</summary>
    private static bool PlaceWinner(LiguillaNode source, LiguillaNode target, bool isSlotA)
    {
        var winnerTeamId = source.WinnerTeamId;
        Team? winnerTeam;
        int? winnerSeed;

        if (source.TeamAId == winnerTeamId)
        {
            winnerTeam = source.TeamA;
            winnerSeed = source.SeedA;
        }
        else if (source.TeamBId == winnerTeamId)
        {
            winnerTeam = source.TeamB;
            winnerSeed = source.SeedB;
        }
        else
        {
            return false;
        }

        // Un cruce ya publicado por ESPN con ambos equipos no se toca.
        if (target.EspnEventGroupKey is not null && target.TeamAId.HasValue && target.TeamBId.HasValue)
        {
            return false;
        }

        // Solo se rellenan slots vacios: nunca se pisa un equipo ya asignado.
        if (isSlotA)
        {
            if (target.TeamAId.HasValue)
            {
                return false;
            }

            target.TeamAId = winnerTeamId;
            target.TeamA = winnerTeam;
            target.SeedA = winnerSeed;
        }
        else
        {
            if (target.TeamBId.HasValue)
            {
                return false;
            }

            target.TeamBId = winnerTeamId;
            target.TeamB = winnerTeam;
            target.SeedB = winnerSeed;
        }

        return true;
    }

    private static IEnumerable<int> FieldTeamIds(IReadOnlyList<LiguillaNode> nodes, string roundCode)
    {
        return NodesOfRound(nodes, roundCode)
            .SelectMany(node => new[] { node.TeamAId, node.TeamBId })
            .OfType<int>()
            .Distinct();
    }

    private static List<LiguillaNode> NodesOfRound(IReadOnlyList<LiguillaNode> nodes, string roundCode)
    {
        return nodes
            .Where(node => node.Round == roundCode)
            .OrderBy(node => node.NodeNumber)
            .ToList();
    }

    private static int IndexOfRound(string roundCode)
    {
        for (var index = 0; index < RoundDefinitions.Count; index++)
        {
            if (RoundDefinitions[index].Code == roundCode)
            {
                return index;
            }
        }

        return -1;
    }

    private static IReadOnlyList<int> PicksOfRound(
        IReadOnlyDictionary<string, IReadOnlyList<int>> picksByRound,
        string roundCode)
    {
        return picksByRound.TryGetValue(roundCode, out var picks) && picks is not null
            ? picks
            : Array.Empty<int>();
    }
}
