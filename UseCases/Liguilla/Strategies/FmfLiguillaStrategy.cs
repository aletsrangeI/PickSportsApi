using Domain.Common;
using Domain.Entities;
using Interface.UseCases.Knockout;

namespace UseCases.Liguilla.Strategies;

/// <summary>Liguilla de la Liga MX (FMF): 8 clubes, Cuartos, Semifinales con re-siembra (Art. 10) y Final.</summary>
public class FmfLiguillaStrategy : IKnockoutFormatStrategy
{
    public const string Code = KnockoutFormatCodes.FmfLiguilla;
    public const string QuarterFinalRound = "QF";
    public const string SemiFinalRound = "SF";
    public const string FinalRound = "FINAL";

    private const int QuarterFinalCount = 4;
    private const int SemiFinalCount = 2;
    private const int FirstQuarterFinalNode = 1;
    private const int LastQuarterFinalNode = 4;
    private const int FirstSemiFinalNode = 5;
    private const int SecondSemiFinalNode = 6;
    private const int FinalNodeNumber = 7;
    private const int DefaultSeed = 8;

    private static readonly IReadOnlyList<KnockoutRound> RoundList = new List<KnockoutRound>
    {
        new(QuarterFinalRound, 1, QuarterFinalCount, FmfLiguillaEngine.QF_POINTS_PER_HIT, QuarterFinalCount),
        new(SemiFinalRound, 2, SemiFinalCount, FmfLiguillaEngine.SF_POINTS_PER_HIT, SemiFinalCount),
        new(FinalRound, 3, 1, FmfLiguillaEngine.CHAMPION_POINTS, 1)
    };

    public string FormatCode => Code;

    public IReadOnlyList<KnockoutRound> Rounds => RoundList;

    public int PerfectBonusPoints => FmfLiguillaEngine.PERFECT_BONUS;

    public string FirstPickableRoundCode => QuarterFinalRound;

    public bool IsFieldReady(IReadOnlyList<LiguillaNode> nodes, bool regularSeasonFinished)
    {
        if (!regularSeasonFinished)
            return false;

        var qfNodes = GetQuarterFinals(nodes);
        return qfNodes.Count == QuarterFinalCount && qfNodes.All(n => n.TeamAId.HasValue && n.TeamBId.HasValue);
    }

    public IReadOnlyList<int> EligibleTeamIds(
        string roundCode,
        IReadOnlyList<LiguillaNode> nodes,
        IReadOnlyDictionary<string, IReadOnlyList<int>> previousPicks)
    {
        if (string.Equals(roundCode, QuarterFinalRound, StringComparison.OrdinalIgnoreCase))
        {
            return GetQuarterFinals(nodes)
                .SelectMany(n => new[] { n.TeamAId, n.TeamBId })
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();
        }

        if (string.Equals(roundCode, SemiFinalRound, StringComparison.OrdinalIgnoreCase))
            return PicksOf(previousPicks, QuarterFinalRound);

        if (string.Equals(roundCode, FinalRound, StringComparison.OrdinalIgnoreCase))
            return PicksOf(previousPicks, SemiFinalRound);

        return Array.Empty<int>();
    }

    public void ValidatePicks(
        IReadOnlyList<LiguillaNode> nodes,
        IReadOnlyDictionary<string, IReadOnlyList<int>> picksByRound)
    {
        var qfWinners = PicksOf(picksByRound, QuarterFinalRound).ToHashSet();
        var sfWinners = PicksOf(picksByRound, SemiFinalRound);
        var champions = PicksOf(picksByRound, FinalRound);

        if (qfWinners.Count != QuarterFinalCount)
            throw new ArgumentException("Debes elegir 4 ganadores distintos en Cuartos de Final.");

        if (sfWinners.Count != SemiFinalCount || sfWinners.Any(id => !qfWinners.Contains(id)))
            throw new ArgumentException("Los finalistas de Semifinales deben haber sido seleccionados como ganadores en Cuartos de Final.");

        if (sfWinners[0] == sfWinners[1])
            throw new ArgumentException("Los dos finalistas deben ser equipos distintos.");

        if (champions.Count != 1 || !sfWinners.Contains(champions[0]))
            throw new ArgumentException("El Campeón debe ser uno de los dos equipos finalistas seleccionados.");
    }

    public IReadOnlyList<LiguillaNode> AdvanceWinners(IReadOnlyList<LiguillaNode> allNodes)
    {
        var modified = new List<LiguillaNode>();

        // Cuartos resueltos: Art. 10 de la FMF siembra Semifinales (nodos 5 y 6)
        var qfNodes = GetQuarterFinals(allNodes);
        if (qfNodes.Count == QuarterFinalCount && qfNodes.All(IsDecided))
        {
            var qfWinnerSeeds = qfNodes.Select(qf =>
            {
                int winnerId = qf.WinnerTeamId!.Value;
                int seed = (winnerId == qf.TeamAId && qf.SeedA.HasValue) ? qf.SeedA.Value
                         : (winnerId == qf.TeamBId && qf.SeedB.HasValue) ? qf.SeedB.Value
                         : DefaultSeed;
                return new TeamSeedInfo(winnerId, seed);
            }).ToList();

            var (semi1, semi2) = FmfLiguillaEngine.CalculateFmfReSeed(qfWinnerSeeds);

            SeedSemiFinal(allNodes.FirstOrDefault(n => n.NodeNumber == FirstSemiFinalNode), semi1, modified);
            SeedSemiFinal(allNodes.FirstOrDefault(n => n.NodeNumber == SecondSemiFinalNode), semi2, modified);
        }

        // Semifinales resueltas: siembra la Gran Final (nodo 7)
        var sfNodes = allNodes
            .Where(n => n.NodeNumber == FirstSemiFinalNode || n.NodeNumber == SecondSemiFinalNode)
            .OrderBy(n => n.NodeNumber)
            .ToList();
        if (sfNodes.Count == SemiFinalCount && sfNodes.All(IsDecided))
        {
            var finalNode = allNodes.FirstOrDefault(n => n.NodeNumber == FinalNodeNumber);
            if (finalNode != null)
            {
                finalNode.TeamAId = sfNodes[0].WinnerTeamId;
                finalNode.TeamBId = sfNodes[1].WinnerTeamId;
                modified.Add(finalNode);
            }
        }

        return modified;
    }

    public NodeOutcome ResolveFromEspn(LiguillaNode node, EspnKnockoutTie tie)
    {
        if (tie.SeriesCompleted && !string.IsNullOrEmpty(tie.WinnerEspnTeamId))
        {
            if (node.TeamA?.EspnTeamId == tie.WinnerEspnTeamId && node.TeamAId.HasValue)
                return new NodeOutcome(node.NodeNumber, true, node.TeamAId);

            if (node.TeamB?.EspnTeamId == tie.WinnerEspnTeamId && node.TeamBId.HasValue)
                return new NodeOutcome(node.NodeNumber, true, node.TeamBId);
        }

        return new NodeOutcome(node.NodeNumber, false, null);
    }

    private static List<LiguillaNode> GetQuarterFinals(IReadOnlyList<LiguillaNode> nodes) =>
        nodes.Where(n => n.NodeNumber >= FirstQuarterFinalNode && n.NodeNumber <= LastQuarterFinalNode).ToList();

    private static bool IsDecided(LiguillaNode node) => node.IsResolved && node.WinnerTeamId.HasValue;

    private static IReadOnlyList<int> PicksOf(IReadOnlyDictionary<string, IReadOnlyList<int>> picksByRound, string roundCode) =>
        picksByRound.TryGetValue(roundCode, out var picks) ? picks : Array.Empty<int>();

    private static void SeedSemiFinal(LiguillaNode? node, SemiPairing pairing, List<LiguillaNode> modified)
    {
        if (node == null)
            return;

        node.TeamAId = pairing.TeamA.TeamId;
        node.SeedA = pairing.TeamA.Seed;
        node.TeamBId = pairing.TeamB.TeamId;
        node.SeedB = pairing.TeamB.Seed;
        modified.Add(node);
    }
}
