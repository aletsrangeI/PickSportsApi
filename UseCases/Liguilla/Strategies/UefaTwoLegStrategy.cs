using Domain.Common;
using Domain.Entities;
using Interface.UseCases.Knockout;

namespace UseCases.Liguilla.Strategies;

/// <summary>
/// Formato de eliminatorias de UEFA: octavos, cuartos y semifinales a ida y vuelta, y final a
/// partido unico. La llave es fija: el nodo ganador alimenta el slot que publica el sorteo de ESPN.
/// El nodo informativo de playoffs (Round "PO") nunca se considera porque todo se filtra por codigo de ronda.
/// </summary>
public sealed class UefaTwoLegStrategy : IKnockoutFormatStrategy
{
    public const string FormatCodeValue = KnockoutFormatCodes.UefaTwoLeg;

    private const string RoundOf16Code = "R16";
    private const string QuarterFinalsCode = "QF";
    private const string SemiFinalsCode = "SF";
    private const string FinalCode = "FINAL";

    private const int RoundOf16PointsPerHit = 10;
    private const int QuarterFinalsPointsPerHit = 15;
    private const int SemiFinalsPointsPerHit = 25;
    private const int FinalPointsPerHit = 60;
    private const int PerfectBonus = 20;

    private const int RoundOf16NodeCount = 8;
    private const int QuarterFinalsNodeCount = 4;
    private const int SemiFinalsNodeCount = 2;
    private const int FinalNodeCount = 1;

    private static readonly IReadOnlyList<KnockoutRound> RoundDefinitions = new List<KnockoutRound>
    {
        new(RoundOf16Code, 1, 8, RoundOf16PointsPerHit, RoundOf16NodeCount),
        new(QuarterFinalsCode, 2, 4, QuarterFinalsPointsPerHit, QuarterFinalsNodeCount),
        new(SemiFinalsCode, 3, 2, SemiFinalsPointsPerHit, SemiFinalsNodeCount),
        new(FinalCode, 4, 1, FinalPointsPerHit, FinalNodeCount)
    };

    /// <inheritdoc />
    public string FormatCode => FormatCodeValue;

    /// <inheritdoc />
    public IReadOnlyList<KnockoutRound> Rounds => RoundDefinitions;

    /// <inheritdoc />
    public int PerfectBonusPoints => PerfectBonus;

    /// <inheritdoc />
    public string FirstPickableRoundCode => RoundOf16Code;

    /// <inheritdoc />
    public bool IsFieldReady(IReadOnlyList<LiguillaNode> nodes, bool regularSeasonFinished)
    {
        // UEFA no depende del cierre de la fase regular: depende del sorteo de octavos publicado por ESPN.
        var roundOf16 = NodesOfRound(nodes, RoundOf16Code);

        return roundOf16.Count == RoundOf16NodeCount
               && roundOf16.All(node => node.TeamAId is not null && node.TeamBId is not null);
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
            return NodesOfRound(nodes, roundCode)
                .SelectMany(node => new[] { node.TeamAId, node.TeamBId })
                .OfType<int>()
                .Distinct()
                .ToList();
        }

        var previousRoundCode = RoundDefinitions[roundIndex - 1].Code;
        var previous = PicksOfRound(previousPicks, previousRoundCode);

        return previous.Distinct().ToList();
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

            if (roundIndex == 0)
            {
                var fieldTeams = NodesOfRound(nodes, round.Code)
                    .SelectMany(node => new[] { node.TeamAId, node.TeamBId })
                    .OfType<int>()
                    .ToHashSet();

                if (picks.Any(teamId => !fieldTeams.Contains(teamId)))
                {
                    throw new ArgumentException($"Los equipos de {round.Code} deben haber sido elegidos en la ronda anterior.");
                }

                continue;
            }

            var previousRoundCode = RoundDefinitions[roundIndex - 1].Code;
            var previousPicksOfRound = PicksOfRound(picksByRound, previousRoundCode).ToHashSet();

            if (!picks.All(previousPicksOfRound.Contains))
            {
                throw roundIndex == RoundDefinitions.Count - 1
                    ? new ArgumentException("El Campeón debe ser uno de los finalistas elegidos.")
                    : new ArgumentException($"Los equipos de {round.Code} deben haber sido elegidos en la ronda anterior.");
            }
        }
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
                if (!source.IsResolved || source.WinnerTeamId is null)
                {
                    continue;
                }

                var targetIndex = position / 2;
                if (targetIndex >= targetNodes.Count)
                {
                    continue;
                }

                // Un nodo ya resuelto jamas se reescribe.
                var target = targetNodes[targetIndex];
                if (target.IsResolved)
                {
                    continue;
                }

                var winnerTeamId = source.WinnerTeamId.Value;
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
                    continue;
                }

                // UEFA solo rellena slots vacios: el resto lo publica el sorteo de ESPN.
                if (position % 2 == 0)
                {
                    if (target.TeamAId is not null)
                    {
                        continue;
                    }

                    target.TeamAId = winnerTeamId;
                    target.TeamA = winnerTeam;
                    target.SeedA = winnerSeed;
                }
                else
                {
                    if (target.TeamBId is not null)
                    {
                        continue;
                    }

                    target.TeamBId = winnerTeamId;
                    target.TeamB = winnerTeam;
                    target.SeedB = winnerSeed;
                }

                if (!modified.Contains(target))
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
        var unresolved = new NodeOutcome(node.NodeNumber, false, null);

        var teamAEspnId = node.TeamA?.EspnTeamId;
        var teamBEspnId = node.TeamB?.EspnTeamId;
        if (string.IsNullOrEmpty(teamAEspnId) && string.IsNullOrEmpty(teamBEspnId))
        {
            return unresolved;
        }

        string? winnerEspnId;

        if (node.Round == FinalCode || tie.TotalLegs <= 1)
        {
            // Partido unico o final: el ganador del partido incluye los penales.
            winnerEspnId = tie.WinnerEspnTeamId;
        }
        else
        {
            if (!tie.SeriesCompleted)
            {
                return unresolved;
            }

            winnerEspnId = string.IsNullOrWhiteSpace(tie.WinnerEspnTeamId)
                ? AggregateWinner(tie.AggregateScore, teamAEspnId, teamBEspnId)
                : tie.WinnerEspnTeamId;
        }

        if (string.IsNullOrWhiteSpace(winnerEspnId))
        {
            return unresolved;
        }

        if (!string.IsNullOrEmpty(teamAEspnId) && string.Equals(teamAEspnId, winnerEspnId, StringComparison.Ordinal))
        {
            return new NodeOutcome(node.NodeNumber, true, node.TeamAId);
        }

        if (!string.IsNullOrEmpty(teamBEspnId) && string.Equals(teamBEspnId, winnerEspnId, StringComparison.Ordinal))
        {
            return new NodeOutcome(node.NodeNumber, true, node.TeamBId);
        }

        return unresolved;
    }

    /// <summary>Equipo con mayor marcador global; sin dos equipos o con empate no hay ganador firme.</summary>
    private static string? AggregateWinner(
        IReadOnlyDictionary<string, double> aggregateScore,
        string? teamAEspnId,
        string? teamBEspnId)
    {
        if (string.IsNullOrEmpty(teamAEspnId)
            || string.IsNullOrEmpty(teamBEspnId)
            || !aggregateScore.TryGetValue(teamAEspnId, out var teamAScore)
            || !aggregateScore.TryGetValue(teamBEspnId, out var teamBScore))
        {
            return null;
        }

        if (teamAScore > teamBScore)
        {
            return teamAEspnId;
        }

        if (teamBScore > teamAScore)
        {
            return teamBEspnId;
        }

        return null;
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
