using Domain.Entities;
using FluentAssertions;
using Interface.UseCases.Knockout;
using UseCases.Liguilla.Strategies;
using Xunit;

namespace PickSportsApi.UnitTests.UseCasesTests.Knockout;

public class UefaTwoLegStrategyTests
{
    private const string RoundOf16 = "R16";
    private const string QuarterFinals = "QF";
    private const string SemiFinals = "SF";
    private const string Final = "FINAL";

    [Fact]
    public void Rounds_ExposeUefaCodesPointsAndNodeCounts()
    {
        // Arrange
        var strategy = new UefaTwoLegStrategy();

        // Act
        var rounds = strategy.Rounds;

        // Assert
        strategy.FormatCode.Should().Be("UEFA_TWO_LEG");
        strategy.FirstPickableRoundCode.Should().Be(RoundOf16);
        strategy.PerfectBonusPoints.Should().Be(20);

        rounds.Should().HaveCount(4);
        rounds.Select(round => round.Code).Should().Equal(RoundOf16, QuarterFinals, SemiFinals, Final);
        rounds.Select(round => round.Order).Should().Equal(1, 2, 3, 4);
        rounds.Select(round => round.WinnersToPick).Should().Equal(8, 4, 2, 1);
        rounds.Select(round => round.PointsPerHit).Should().Equal(10, 15, 25, 60);
        rounds.Select(round => round.NodeCount).Should().Equal(8, 4, 2, 1);
    }

    [Fact]
    public void IsFieldReady_WhenEightRoundOf16TiesHaveBothTeams_IsTrueEvenIfSeasonIsOpen()
    {
        // Arrange
        var strategy = new UefaTwoLegStrategy();
        var nodes = BuildEmptyBracket();
        FillRoundOf16(nodes);

        // Act
        var isReady = strategy.IsFieldReady(nodes, regularSeasonFinished: false);

        // Assert
        isReady.Should().BeTrue();
    }

    [Fact]
    public void IsFieldReady_WhenOneRoundOf16SlotIsEmpty_IsFalse()
    {
        // Arrange
        var strategy = new UefaTwoLegStrategy();
        var nodes = BuildEmptyBracket();
        FillRoundOf16(nodes);
        nodes.Single(node => node.Round == RoundOf16 && node.NodeNumber == 8).TeamBId = null;

        // Act
        var isReady = strategy.IsFieldReady(nodes, regularSeasonFinished: true);

        // Assert
        isReady.Should().BeFalse();
    }

    [Fact]
    public void IsFieldReady_WhenOnlySevenRoundOf16TiesExist_IsFalse()
    {
        // Arrange
        var strategy = new UefaTwoLegStrategy();
        var nodes = new List<LiguillaNode>
        {
            BuildNode(1, RoundOf16, new TestTeam(1, "101"), new TestTeam(2, "102")),
            BuildNode(2, RoundOf16, new TestTeam(3, "103"), new TestTeam(4, "104")),
            BuildNode(3, RoundOf16, new TestTeam(5, "105"), new TestTeam(6, "106")),
            BuildNode(4, RoundOf16, new TestTeam(7, "107"), new TestTeam(8, "108")),
            BuildNode(5, RoundOf16, new TestTeam(9, "109"), new TestTeam(10, "110")),
            BuildNode(6, RoundOf16, new TestTeam(11, "111"), new TestTeam(12, "112")),
            BuildNode(7, RoundOf16, new TestTeam(13, "113"), new TestTeam(14, "114"))
        };

        // Act
        var isReady = strategy.IsFieldReady(nodes, regularSeasonFinished: true);

        // Assert
        isReady.Should().BeFalse();
    }

    [Fact]
    public void ResolveFromEspn_CompletedSecondLegWithWinnerId_ResolvesEspnWinner()
    {
        // Arrange: Barcelona "83" vs Newcastle "361" (ucl-r16-leg2-20260318).
        var strategy = new UefaTwoLegStrategy();
        var node = BuildNode(1, RoundOf16, new TestTeam(501, "83"), new TestTeam(502, "361"));
        var tie = new EspnKnockoutTie
        {
            GroupKey = "ucl-r16-leg2-20260318",
            RoundSlug = "round-of-16",
            Headline = "2nd Leg - Barcelona advance 8-3 on aggregate",
            LegNumber = 2,
            TotalLegs = 2,
            SeriesCompleted = true,
            WinnerEspnTeamId = "83",
            AggregateScore = new Dictionary<string, double> { ["83"] = 8, ["361"] = 3 }
        };

        // Act
        var outcome = strategy.ResolveFromEspn(node, tie);

        // Assert
        outcome.IsResolved.Should().BeTrue();
        outcome.NodeNumber.Should().Be(1);
        outcome.WinnerTeamId.Should().Be(501);
    }

    [Fact]
    public void ResolveFromEspn_CompletedSecondLegWithoutWinnerId_ResolvesHigherAggregateTeam()
    {
        // Arrange: Atalanta "105" vs Bayern "132", 2-10 global, ESPN sin WinnerEspnTeamId.
        var strategy = new UefaTwoLegStrategy();
        var node = BuildNode(2, RoundOf16, new TestTeam(503, "105"), new TestTeam(504, "132"));
        var tie = new EspnKnockoutTie
        {
            GroupKey = "ucl-r16-leg2-20260318",
            RoundSlug = "round-of-16",
            LegNumber = 2,
            TotalLegs = 2,
            SeriesCompleted = true,
            WinnerEspnTeamId = null,
            AggregateScore = new Dictionary<string, double> { ["105"] = 2, ["132"] = 10 }
        };

        // Act
        var outcome = strategy.ResolveFromEspn(node, tie);

        // Assert
        outcome.IsResolved.Should().BeTrue();
        outcome.WinnerTeamId.Should().Be(504);
    }

    [Fact]
    public void ResolveFromEspn_FirstLegNotCompleted_DoesNotResolve()
    {
        // Arrange: ucl-r16-leg1-20260311, ida en curso con ventaja parcial.
        var strategy = new UefaTwoLegStrategy();
        var node = BuildNode(1, RoundOf16, new TestTeam(501, "83"), new TestTeam(502, "361"));
        var tie = new EspnKnockoutTie
        {
            GroupKey = "ucl-r16-leg1-20260311",
            RoundSlug = "round-of-16",
            LegNumber = 1,
            TotalLegs = 2,
            SeriesCompleted = false,
            WinnerEspnTeamId = null,
            AggregateScore = new Dictionary<string, double> { ["83"] = 3, ["361"] = 1 }
        };

        // Act
        var outcome = strategy.ResolveFromEspn(node, tie);

        // Assert
        outcome.IsResolved.Should().BeFalse();
        outcome.WinnerTeamId.Should().BeNull();
    }

    [Fact]
    public void ResolveFromEspn_TiedAggregateAndNoWinnerId_DoesNotResolve()
    {
        // Arrange
        var strategy = new UefaTwoLegStrategy();
        var node = BuildNode(1, RoundOf16, new TestTeam(501, "83"), new TestTeam(502, "361"));
        var tie = new EspnKnockoutTie
        {
            LegNumber = 2,
            TotalLegs = 2,
            SeriesCompleted = true,
            WinnerEspnTeamId = null,
            AggregateScore = new Dictionary<string, double> { ["83"] = 4, ["361"] = 4 }
        };

        // Act
        var outcome = strategy.ResolveFromEspn(node, tie);

        // Assert
        outcome.IsResolved.Should().BeFalse();
    }

    [Fact]
    public void ResolveFromEspn_CompletedSeriesWithoutWinnerNorAggregate_DoesNotResolve()
    {
        // Arrange
        var strategy = new UefaTwoLegStrategy();
        var node = BuildNode(3, RoundOf16, new TestTeam(505, "83"), new TestTeam(506, "361"));
        var tie = new EspnKnockoutTie
        {
            LegNumber = 2,
            TotalLegs = 2,
            SeriesCompleted = true,
            WinnerEspnTeamId = null
        };

        // Act
        var outcome = strategy.ResolveFromEspn(node, tie);

        // Assert
        outcome.IsResolved.Should().BeFalse();
    }

    [Fact]
    public void ResolveFromEspn_FinalDecidedOnPenalties_ResolvesWinnerInFinalNode()
    {
        // Arrange: ucl-final-20260530, PSG "160" 1-1 Arsenal "359" y 4-3 en penales.
        var strategy = new UefaTwoLegStrategy();
        var node = BuildNode(15, Final, new TestTeam(601, "160"), new TestTeam(602, "359"));
        var tie = new EspnKnockoutTie
        {
            GroupKey = "ucl-final-20260530",
            RoundSlug = "final",
            LegNumber = 1,
            TotalLegs = 1,
            SeriesCompleted = true,
            WinnerEspnTeamId = "160",
            AggregateScore = new Dictionary<string, double> { ["160"] = 1, ["359"] = 1 }
        };

        // Act
        var outcome = strategy.ResolveFromEspn(node, tie);

        // Assert
        outcome.NodeNumber.Should().Be(15);
        outcome.IsResolved.Should().BeTrue();
        outcome.WinnerTeamId.Should().Be(601);
    }

    [Fact]
    public void ResolveFromEspn_WinnerIdNotPresentInNode_DoesNotResolve()
    {
        // Arrange
        var strategy = new UefaTwoLegStrategy();
        var node = BuildNode(1, RoundOf16, new TestTeam(501, "83"), new TestTeam(502, "361"));
        var tie = new EspnKnockoutTie
        {
            LegNumber = 2,
            TotalLegs = 2,
            SeriesCompleted = true,
            WinnerEspnTeamId = "999",
            AggregateScore = new Dictionary<string, double> { ["83"] = 8, ["361"] = 3 }
        };

        // Act
        var outcome = strategy.ResolveFromEspn(node, tie);

        // Assert
        outcome.IsResolved.Should().BeFalse();
        outcome.WinnerTeamId.Should().BeNull();
    }

    [Fact]
    public void ValidatePicks_CoherentBracket_DoesNotThrow()
    {
        // Arrange
        var strategy = new UefaTwoLegStrategy();
        var nodes = BuildEmptyBracket();
        FillRoundOf16(nodes);
        var picks = new Dictionary<string, IReadOnlyList<int>>
        {
            [RoundOf16] = new List<int> { 1, 3, 5, 7, 9, 11, 13, 15 },
            [QuarterFinals] = new List<int> { 1, 5, 9, 13 },
            [SemiFinals] = new List<int> { 1, 9 },
            [Final] = new List<int> { 1 }
        };

        // Act
        var act = () => strategy.ValidatePicks(nodes, picks);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidatePicks_QuarterFinalPickNotChosenInRoundOf16_ThrowsExactMessage()
    {
        // Arrange
        var strategy = new UefaTwoLegStrategy();
        var nodes = BuildEmptyBracket();
        FillRoundOf16(nodes);
        var picks = new Dictionary<string, IReadOnlyList<int>>
        {
            [RoundOf16] = new List<int> { 1, 3, 5, 7, 9, 11, 13, 15 },
            [QuarterFinals] = new List<int> { 1, 5, 9, 16 },
            [SemiFinals] = new List<int> { 1 },
            [Final] = new List<int> { 1 }
        };

        // Act
        var act = () => strategy.ValidatePicks(nodes, picks);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Los equipos de QF deben haber sido elegidos en la ronda anterior.");
    }

    [Fact]
    public void ValidatePicks_ChampionNotAmongSemifinalPicks_ThrowsExactMessage()
    {
        // Arrange
        var strategy = new UefaTwoLegStrategy();
        var nodes = BuildEmptyBracket();
        FillRoundOf16(nodes);
        var picks = new Dictionary<string, IReadOnlyList<int>>
        {
            [RoundOf16] = new List<int> { 1, 3, 5, 7, 9, 11, 13, 15 },
            [QuarterFinals] = new List<int> { 1, 5, 9, 13 },
            [SemiFinals] = new List<int> { 1, 9 },
            [Final] = new List<int> { 5 }
        };

        // Act
        var act = () => strategy.ValidatePicks(nodes, picks);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("El Campeón debe ser uno de los finalistas elegidos.");
    }

    [Fact]
    public void ValidatePicks_WhenRoundOf16HasSevenWinners_ThrowsExactMessage()
    {
        // Arrange
        var strategy = new UefaTwoLegStrategy();
        var nodes = BuildEmptyBracket();
        FillRoundOf16(nodes);
        var picks = new Dictionary<string, IReadOnlyList<int>>
        {
            [RoundOf16] = new List<int> { 1, 3, 5, 7, 9, 11, 13 }
        };

        // Act
        var act = () => strategy.ValidatePicks(nodes, picks);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Debes elegir 8 ganadores distintos en R16.");
    }

    [Fact]
    public void ValidatePicks_WithDuplicatedWinners_ThrowsExactMessage()
    {
        // Arrange
        var strategy = new UefaTwoLegStrategy();
        var nodes = BuildEmptyBracket();
        FillRoundOf16(nodes);
        var picks = new Dictionary<string, IReadOnlyList<int>>
        {
            [RoundOf16] = new List<int> { 1, 1, 5, 7, 9, 11, 13, 15 }
        };

        // Act
        var act = () => strategy.ValidatePicks(nodes, picks);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Debes elegir 8 ganadores distintos en R16.");
    }

    [Fact]
    public void ValidatePicks_WhenRoundOf16TeamIsNotInTheField_ThrowsExactMessage()
    {
        // Arrange
        var strategy = new UefaTwoLegStrategy();
        var nodes = BuildEmptyBracket();
        FillRoundOf16(nodes);
        var picks = new Dictionary<string, IReadOnlyList<int>>
        {
            [RoundOf16] = new List<int> { 1, 3, 5, 7, 9, 11, 13, 99 }
        };

        // Act
        var act = () => strategy.ValidatePicks(nodes, picks);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Los equipos de R16 deben haber sido elegidos en la ronda anterior.");
    }

    [Fact]
    public void AdvanceWinners_ResolvedRoundOf16_SeedsQuarterFinalSlotsAndCopiesSeeds()
    {
        // Arrange: nodo 1 lo gana el TeamB (seed 2), nodo 2 lo gana el TeamA (seed 1).
        var strategy = new UefaTwoLegStrategy();
        var nodes = BuildEmptyBracket();
        FillRoundOf16(nodes);

        var nodeOne = nodes.Single(node => node.Round == RoundOf16 && node.NodeNumber == 1);
        nodeOne.IsResolved = true;
        nodeOne.WinnerTeamId = nodeOne.TeamBId;
        nodeOne.SeedA = 1;
        nodeOne.SeedB = 2;

        var nodeTwo = nodes.Single(node => node.Round == RoundOf16 && node.NodeNumber == 2);
        nodeTwo.IsResolved = true;
        nodeTwo.WinnerTeamId = nodeTwo.TeamAId;
        nodeTwo.SeedA = 1;
        nodeTwo.SeedB = 2;

        // Act
        var modified = strategy.AdvanceWinners(nodes);

        // Assert
        modified.Should().HaveCount(1);

        var quarterFinal = nodes.Single(node => node.Round == QuarterFinals && node.NodeNumber == 9);
        quarterFinal.TeamAId.Should().Be(2);
        quarterFinal.TeamA.Should().NotBeNull();
        quarterFinal.TeamA?.EspnTeamId.Should().Be("102");
        quarterFinal.SeedA.Should().Be(2);
        quarterFinal.TeamBId.Should().Be(3);
        quarterFinal.TeamB.Should().NotBeNull();
        quarterFinal.TeamB?.EspnTeamId.Should().Be("103");
        quarterFinal.SeedB.Should().Be(1);

        var untouchedQuarterFinal = nodes.Single(node => node.Round == QuarterFinals && node.NodeNumber == 10);
        untouchedQuarterFinal.TeamAId.Should().BeNull();
        untouchedQuarterFinal.TeamBId.Should().BeNull();
    }

    [Fact]
    public void AdvanceWinners_DoesNotOverwriteDrawnSlotsNorResolvedTargets()
    {
        // Arrange: el sorteo ya publico el TeamA del QF 9 y el QF 10 ya esta resuelto.
        var strategy = new UefaTwoLegStrategy();
        var nodes = BuildEmptyBracket();
        FillRoundOf16(nodes);

        var quarterFinal = nodes.Single(node => node.Round == QuarterFinals && node.NodeNumber == 9);
        quarterFinal.TeamAId = 701;
        quarterFinal.TeamA = BuildTeam(new TestTeam(701, "701"));

        var resolvedQuarterFinal = nodes.Single(node => node.Round == QuarterFinals && node.NodeNumber == 10);
        resolvedQuarterFinal.IsResolved = true;
        resolvedQuarterFinal.WinnerTeamId = 1;

        var nodeOne = nodes.Single(node => node.Round == RoundOf16 && node.NodeNumber == 1);
        nodeOne.IsResolved = true;
        nodeOne.WinnerTeamId = nodeOne.TeamAId;

        var nodeTwo = nodes.Single(node => node.Round == RoundOf16 && node.NodeNumber == 2);
        nodeTwo.IsResolved = true;
        nodeTwo.WinnerTeamId = nodeTwo.TeamAId;

        var nodeThree = nodes.Single(node => node.Round == RoundOf16 && node.NodeNumber == 3);
        nodeThree.IsResolved = true;
        nodeThree.WinnerTeamId = nodeThree.TeamAId;

        // Act
        var modified = strategy.AdvanceWinners(nodes);

        // Assert
        modified.Should().HaveCount(1);
        modified.Single().Should().BeSameAs(quarterFinal);

        quarterFinal.TeamAId.Should().Be(701);
        quarterFinal.TeamBId.Should().Be(3);

        resolvedQuarterFinal.TeamAId.Should().BeNull();
        resolvedQuarterFinal.TeamBId.Should().BeNull();
    }

    [Fact]
    public void AdvanceWinners_AfterSemifinalsResolve_SeedsTheFinalSlots()
    {
        // Arrange
        var strategy = new UefaTwoLegStrategy();
        var nodes = BuildEmptyBracket();
        FillRoundOf16(nodes);

        var firstSemiFinal = nodes.Single(node => node.Round == SemiFinals && node.NodeNumber == 13);
        firstSemiFinal.TeamAId = 21;
        firstSemiFinal.TeamA = BuildTeam(new TestTeam(21, "121"));
        firstSemiFinal.TeamBId = 22;
        firstSemiFinal.TeamB = BuildTeam(new TestTeam(22, "122"));
        firstSemiFinal.SeedA = 1;
        firstSemiFinal.SeedB = 2;
        firstSemiFinal.IsResolved = true;
        firstSemiFinal.WinnerTeamId = firstSemiFinal.TeamAId;

        var secondSemiFinal = nodes.Single(node => node.Round == SemiFinals && node.NodeNumber == 14);
        secondSemiFinal.TeamAId = 23;
        secondSemiFinal.TeamA = BuildTeam(new TestTeam(23, "123"));
        secondSemiFinal.TeamBId = 24;
        secondSemiFinal.TeamB = BuildTeam(new TestTeam(24, "124"));
        secondSemiFinal.SeedA = 3;
        secondSemiFinal.SeedB = 4;
        secondSemiFinal.IsResolved = true;
        secondSemiFinal.WinnerTeamId = secondSemiFinal.TeamBId;

        // Act
        var modified = strategy.AdvanceWinners(nodes);

        // Assert
        modified.Should().HaveCount(1);

        var finalNode = nodes.Single(node => node.Round == Final);
        finalNode.TeamAId.Should().Be(firstSemiFinal.TeamAId);
        finalNode.TeamBId.Should().Be(secondSemiFinal.TeamBId);
    }

    [Fact]
    public void AdvanceWinners_WhenNoSourceIsResolved_ReturnsEmpty()
    {
        // Arrange
        var strategy = new UefaTwoLegStrategy();
        var nodes = BuildEmptyBracket();
        FillRoundOf16(nodes);

        // Act
        var modified = strategy.AdvanceWinners(nodes);

        // Assert
        modified.Should().BeEmpty();
    }

    [Fact]
    public void EligibleTeamIds_UnknownRound_ReturnsEmpty()
    {
        // Arrange
        var strategy = new UefaTwoLegStrategy();
        var nodes = BuildEmptyBracket();
        FillRoundOf16(nodes);

        // Act
        var eligible = strategy.EligibleTeamIds("PO", nodes, new Dictionary<string, IReadOnlyList<int>>());

        // Assert
        eligible.Should().BeEmpty();
    }

    [Fact]
    public void EligibleTeamIds_RoundOf16_ReturnsAllDistinctTeamsInTheField()
    {
        // Arrange
        var strategy = new UefaTwoLegStrategy();
        var nodes = BuildEmptyBracket();
        FillRoundOf16(nodes);

        // Act
        var eligible = strategy.EligibleTeamIds(RoundOf16, nodes, new Dictionary<string, IReadOnlyList<int>>());

        // Assert
        eligible.Should().HaveCount(16);
        eligible.Should().BeEquivalentTo(Enumerable.Range(1, 16));
    }

    [Fact]
    public void EligibleTeamIds_LaterRound_ReturnsPreviousPicksDistinctAndInOrder()
    {
        // Arrange
        var strategy = new UefaTwoLegStrategy();
        var nodes = BuildEmptyBracket();
        FillRoundOf16(nodes);
        var previousPicks = new Dictionary<string, IReadOnlyList<int>>
        {
            [RoundOf16] = new List<int> { 5, 3, 5, 9, 11, 13, 15, 17, 19 }
        };

        // Act
        var eligible = strategy.EligibleTeamIds(QuarterFinals, nodes, previousPicks);

        // Assert
        eligible.Should().Equal(5, 3, 9, 11, 13, 15, 17, 19);
    }

    [Fact]
    public void EligibleTeamIds_LaterRoundWithoutPreviousPicks_ReturnsEmpty()
    {
        // Arrange
        var strategy = new UefaTwoLegStrategy();
        var nodes = BuildEmptyBracket();
        FillRoundOf16(nodes);

        // Act
        var eligible = strategy.EligibleTeamIds(Final, nodes, new Dictionary<string, IReadOnlyList<int>>());

        // Assert
        eligible.Should().BeEmpty();
    }

    private static List<LiguillaNode> BuildEmptyBracket()
    {
        var nodes = new List<LiguillaNode>();

        for (var nodeNumber = 1; nodeNumber <= 8; nodeNumber++)
        {
            nodes.Add(BuildNode(nodeNumber, RoundOf16));
        }

        for (var nodeNumber = 9; nodeNumber <= 12; nodeNumber++)
        {
            nodes.Add(BuildNode(nodeNumber, QuarterFinals));
        }

        nodes.Add(BuildNode(13, SemiFinals));
        nodes.Add(BuildNode(14, SemiFinals));
        nodes.Add(BuildNode(15, Final));
        nodes.Add(BuildNode(0, "PO"));

        return nodes;
    }

    private static void FillRoundOf16(List<LiguillaNode> nodes)
    {
        foreach (var node in nodes.Where(node => node.Round == RoundOf16))
        {
            var teamAId = node.NodeNumber * 2 - 1;
            var teamBId = node.NodeNumber * 2;

            node.TeamAId = teamAId;
            node.TeamA = BuildTeam(new TestTeam(teamAId, (100 + teamAId).ToString()));
            node.TeamBId = teamBId;
            node.TeamB = BuildTeam(new TestTeam(teamBId, (100 + teamBId).ToString()));
        }
    }

    private static LiguillaNode BuildNode(
        int nodeNumber,
        string round,
        TestTeam? teamA = null,
        TestTeam? teamB = null)
    {
        var node = new LiguillaNode
        {
            Round = round,
            NodeNumber = nodeNumber
        };

        if (teamA is not null)
        {
            node.TeamAId = teamA.Id;
            node.TeamA = BuildTeam(teamA);
        }

        if (teamB is not null)
        {
            node.TeamBId = teamB.Id;
            node.TeamB = BuildTeam(teamB);
        }

        return node;
    }

    private static Team BuildTeam(TestTeam team)
    {
        return new Team
        {
            Id = team.Id,
            EspnTeamId = team.EspnTeamId,
            Name = $"Team {team.Id}",
            Abbreviation = $"T{team.Id}",
            DisplayName = $"Team {team.Id}"
        };
    }

    private sealed record TestTeam(int Id, string EspnTeamId);
}
