using Domain.Entities;
using FluentAssertions;
using Interface.UseCases.Knockout;
using UseCases.Liguilla;
using UseCases.Liguilla.Strategies;
using Xunit;

namespace PickSportsApi.UnitTests.UseCasesTests.Knockout;

public class KnockoutScoringEngineTests
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<int>> FmfPicks = new Dictionary<string, IReadOnlyList<int>>
    {
        ["QF"] = new List<int> { 101, 102, 103, 104 },
        ["SF"] = new List<int> { 101, 102 },
        ["FINAL"] = new List<int> { 101 }
    };

    private static LiguillaNode Node(string round, int number, int? a, int? b, int? winner) => new()
    {
        Round = round, NodeNumber = number, TeamAId = a, TeamBId = b, WinnerTeamId = winner, IsResolved = winner.HasValue
    };

    private static List<LiguillaNode> FmfMixedNodes() => new()
    {
        Node("QF", 1, 101, 108, 101),
        Node("QF", 2, 102, 107, 107),
        Node("QF", 3, 103, 106, 103),
        Node("QF", 4, 104, 105, 104),
        Node("SF", 5, 101, 107, 101),
        Node("SF", 6, 103, 104, 103),
        Node("FINAL", 7, 101, 103, 101)
    };

    [Fact]
    public void Evaluate_FmfMixedBracket_MatchesLegacyEvaluateBracket()
    {
        // Arrange
        var strategy = new FmfLiguillaStrategy();
        var nodes = FmfMixedNodes();
        var bracket = new LiguillaBracket
        {
            QfWinner1TeamId = 101, QfWinner2TeamId = 102, QfWinner3TeamId = 103, QfWinner4TeamId = 104,
            SfWinner1TeamId = 101, SfWinner2TeamId = 102, ChampionTeamId = 101
        };

        // Act
        var score = KnockoutScoringEngine.Evaluate(strategy, FmfPicks, nodes);
        var legacy = FmfLiguillaEngine.EvaluateBracket(bracket, nodes);

        // Assert
        score.PointsByRound["QF"].Should().Be(legacy.qfPts).And.Be(30);
        score.PointsByRound["SF"].Should().Be(legacy.sfPts).And.Be(25);
        score.PointsByRound["FINAL"].Should().Be(legacy.champPts).And.Be(50);
        score.BonusPoints.Should().Be(legacy.bonusPts).And.Be(0);
        score.TotalPoints.Should().Be(legacy.totalPts).And.Be(105);
        score.ShatteredTeamIds.Should().BeEquivalentTo(legacy.shatteredTeamIds).And.Contain(102);
    }

    [Fact]
    public void Evaluate_FmfPerfectBracket_AwardsBonus()
    {
        // Arrange
        var strategy = new FmfLiguillaStrategy();
        var nodes = new List<LiguillaNode>
        {
            Node("QF", 1, null, null, 101), Node("QF", 2, null, null, 102),
            Node("QF", 3, null, null, 103), Node("QF", 4, null, null, 104),
            Node("SF", 5, null, null, 101), Node("SF", 6, null, null, 102),
            Node("FINAL", 7, null, null, 101)
        };

        // Act
        var score = KnockoutScoringEngine.Evaluate(strategy, FmfPicks, nodes);

        // Assert
        score.BonusPoints.Should().Be(20);
        score.TotalPoints.Should().Be(160);
    }

    [Fact]
    public void Evaluate_AllPicksHitButNotAllNodesResolved_NoBonus()
    {
        // Arrange: la final aún no se resuelve
        var strategy = new FmfLiguillaStrategy();
        var nodes = new List<LiguillaNode>
        {
            Node("QF", 1, null, null, 101), Node("QF", 2, null, null, 102),
            Node("QF", 3, null, null, 103), Node("QF", 4, null, null, 104),
            Node("SF", 5, null, null, 101), Node("SF", 6, null, null, 102),
            Node("FINAL", 7, 101, 102, null)
        };

        // Act
        var score = KnockoutScoringEngine.Evaluate(strategy, FmfPicks, nodes);

        // Assert
        score.BonusPoints.Should().Be(0);
        score.TotalPoints.Should().Be(90);
    }

    [Fact]
    public void Evaluate_FakeTwoRoundFormat_UsesStrategyPointsAndBonus()
    {
        // Arrange: formato inventado de 2 rondas con puntos y bono propios
        var strategy = new FakeStrategy(
            new KnockoutRound("A", 1, 2, 7, 2),
            new KnockoutRound("B", 2, 1, 40, 1));
        var picks = new Dictionary<string, IReadOnlyList<int>>
        {
            ["A"] = new List<int> { 1, 3 },
            ["B"] = new List<int> { 1 }
        };
        var nodes = new List<LiguillaNode>
        {
            Node("A", 1, 1, 2, 1),
            Node("A", 2, 3, 4, 3),
            Node("B", 3, 1, 3, 1),
            Node("PO", 0, 9, 10, 9) // informativo: no puntúa ni cuenta para el bono
        };

        // Act
        var score = KnockoutScoringEngine.Evaluate(strategy, picks, nodes);

        // Assert
        score.PointsByRound["A"].Should().Be(14);
        score.PointsByRound["B"].Should().Be(40);
        score.BonusPoints.Should().Be(FakeStrategy.Bonus);
        score.TotalPoints.Should().Be(14 + 40 + FakeStrategy.Bonus);
        score.ShatteredTeamIds.Should().Equal(3); // perdió la final del formato fake
    }

    [Fact]
    public void Evaluate_WithoutPicksForRound_ScoresZeroForThatRound()
    {
        // Arrange
        var strategy = new FmfLiguillaStrategy();
        var picks = new Dictionary<string, IReadOnlyList<int>>();

        // Act
        var score = KnockoutScoringEngine.Evaluate(strategy, picks, FmfMixedNodes());

        // Assert
        score.TotalPoints.Should().Be(0);
        score.ShatteredTeamIds.Should().BeEmpty();
    }

    private sealed class FakeStrategy : IKnockoutFormatStrategy
    {
        public const int Bonus = 99;

        public FakeStrategy(params KnockoutRound[] rounds) => Rounds = rounds;

        public string FormatCode => "FAKE";
        public IReadOnlyList<KnockoutRound> Rounds { get; }
        public int PerfectBonusPoints => Bonus;
        public string FirstPickableRoundCode => Rounds[0].Code;
        public bool IsFieldReady(IReadOnlyList<LiguillaNode> nodes, bool regularSeasonFinished) => true;
        public IReadOnlyList<int> EligibleTeamIds(string roundCode, IReadOnlyList<LiguillaNode> nodes, IReadOnlyDictionary<string, IReadOnlyList<int>> previousPicks) => Array.Empty<int>();
        public void ValidatePicks(IReadOnlyList<LiguillaNode> nodes, IReadOnlyDictionary<string, IReadOnlyList<int>> picksByRound) { }
        public IReadOnlyList<LiguillaNode> AdvanceWinners(IReadOnlyList<LiguillaNode> allNodes) => Array.Empty<LiguillaNode>();
        public NodeOutcome ResolveFromEspn(LiguillaNode node, EspnKnockoutTie tie) => new(node.NodeNumber, false, null);
    }
}
