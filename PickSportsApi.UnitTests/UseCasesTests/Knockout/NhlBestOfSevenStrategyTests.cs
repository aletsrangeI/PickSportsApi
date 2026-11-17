using Domain.Entities;
using FluentAssertions;
using Interface.UseCases.Knockout;
using UseCases.Liguilla.Strategies;
using Xunit;

namespace PickSportsApi.UnitTests.UseCasesTests.Knockout;

public class NhlBestOfSevenStrategyTests
{
    private const string FirstRound = "R1";
    private const string SecondRound = "R2";
    private const string ConferenceFinal = "CF";
    private const string Final = "FINAL";

    [Fact]
    public void Rounds_ExposeNhlCodesPointsAndNodeCounts()
    {
        // Arrange
        var strategy = new NhlBestOfSevenStrategy();

        // Act
        var rounds = strategy.Rounds;

        // Assert
        strategy.FormatCode.Should().Be("NHL_BEST_OF_7");
        strategy.FirstPickableRoundCode.Should().Be(FirstRound);
        strategy.PerfectBonusPoints.Should().Be(20);
        rounds.Select(round => round.Code).Should().Equal(FirstRound, SecondRound, ConferenceFinal, Final);
        rounds.Select(round => round.WinnersToPick).Should().Equal(8, 4, 2, 1);
        rounds.Select(round => round.PointsPerHit).Should().Equal(10, 20, 30, 50);
        rounds.Select(round => round.NodeCount).Should().Equal(8, 4, 2, 1);
    }

    [Fact]
    public void IsFieldReady_WithEightCompleteFirstRoundNodesAndRegularSeasonDone_IsTrue()
    {
        // Arrange
        var strategy = new NhlBestOfSevenStrategy();
        var nodes = BuildBracket(fillFirstRound: true);

        // Act
        var ready = strategy.IsFieldReady(nodes, regularSeasonFinished: true);

        // Assert
        ready.Should().BeTrue();
    }

    [Fact]
    public void IsFieldReady_WhenRegularSeasonIsOpen_IsFalse()
    {
        // Arrange
        var strategy = new NhlBestOfSevenStrategy();
        var nodes = BuildBracket(fillFirstRound: true);

        // Act
        var ready = strategy.IsFieldReady(nodes, regularSeasonFinished: false);

        // Assert
        ready.Should().BeFalse();
    }

    [Fact]
    public void IsFieldReady_WhenOneFirstRoundSlotIsEmpty_IsFalse()
    {
        // Arrange
        var strategy = new NhlBestOfSevenStrategy();
        var nodes = BuildBracket(fillFirstRound: true);
        nodes.Single(node => node.NodeNumber == 8).TeamBId = null;

        // Act
        var ready = strategy.IsFieldReady(nodes, regularSeasonFinished: true);

        // Assert
        ready.Should().BeFalse();
    }

    [Fact]
    public void ResolveFromEspn_WhenCarolinaHasFourWins_ResolvesCarolinaEvenWithoutWinnerField()
    {
        // Arrange (fixture nhl-series-completed-20260425: "CAR wins series 4-0")
        var strategy = new NhlBestOfSevenStrategy();
        var node = BuildNode(1, FirstRound, MakeTeam(14, "14"), MakeTeam(7, "7"));
        var tie = new EspnKnockoutTie
        {
            TotalLegs = 7,
            SeriesCompleted = true,
            WinnerEspnTeamId = null,
            Wins = new Dictionary<string, int> { ["14"] = 0, ["7"] = 4 }
        };

        // Act
        var outcome = strategy.ResolveFromEspn(node, tie);

        // Assert
        outcome.IsResolved.Should().BeTrue();
        outcome.NodeNumber.Should().Be(1);
        outcome.WinnerTeamId.Should().Be(7);
    }

    [Theory]
    [InlineData("15", 3, "16", 1)]
    [InlineData("15", 2, "16", 2)]
    public void ResolveFromEspn_WhenNoTeamReachedFourWins_IsNotResolved(string teamA, int winsA, string teamB, int winsB)
    {
        // Arrange (fixtures: "PHI leads series 3-1" y "Series tied 2-2")
        var strategy = new NhlBestOfSevenStrategy();
        var node = BuildNode(2, FirstRound, MakeTeam(1, teamA), MakeTeam(2, teamB));
        var tie = new EspnKnockoutTie
        {
            TotalLegs = 7,
            Wins = new Dictionary<string, int> { [teamA] = winsA, [teamB] = winsB }
        };

        // Act
        var outcome = strategy.ResolveFromEspn(node, tie);

        // Assert
        outcome.IsResolved.Should().BeFalse();
        outcome.WinnerTeamId.Should().BeNull();
    }

    [Fact]
    public void ResolveFromEspn_WhenWinnerIsNotInTheNode_IsNotResolved()
    {
        // Arrange
        var strategy = new NhlBestOfSevenStrategy();
        var node = BuildNode(1, FirstRound, MakeTeam(1, "100"), MakeTeam(2, "200"));
        var tie = new EspnKnockoutTie { Wins = new Dictionary<string, int> { ["999"] = 4 } };

        // Act
        var outcome = strategy.ResolveFromEspn(node, tie);

        // Assert
        outcome.IsResolved.Should().BeFalse();
    }

    [Fact]
    public void ValidatePicks_WithCoherentBracket_DoesNotThrow()
    {
        // Arrange
        var strategy = new NhlBestOfSevenStrategy();
        var nodes = BuildBracket(fillFirstRound: true);

        // Act
        var act = () => strategy.ValidatePicks(nodes, CoherentPicks());

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidatePicks_WhenBothFinalistsAreFromTheEast_ThrowsFinalistsMessage()
    {
        // Arrange: CF 1 y 5 salen del cuadro Este (R1 nodos 1-4); el campeon es uno de ellos.
        var strategy = new NhlBestOfSevenStrategy();
        var nodes = BuildBracket(fillFirstRound: true);
        var picks = CoherentPicks();
        picks[ConferenceFinal] = new List<int> { 1, 5 };
        picks[Final] = new List<int> { 1 };

        // Act
        var act = () => strategy.ValidatePicks(nodes, picks);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Los dos finalistas deben ser uno del Este y uno del Oeste.");
    }

    [Fact]
    public void ValidatePicks_WhenTwoSecondRoundWinnersShareTheSameSeries_ThrowsPerSeriesMessage()
    {
        // Arrange: 1 y 3 salen de los nodos R1 1 y 2 (misma llave R2 nodo 9); falta un ganador en el nodo 10.
        var strategy = new NhlBestOfSevenStrategy();
        var nodes = BuildBracket(fillFirstRound: true);
        var picks = CoherentPicks();
        picks[SecondRound] = new List<int> { 1, 3, 9, 13 };

        // Act
        var act = () => strategy.ValidatePicks(nodes, picks);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Debes elegir un ganador por cada llave de R2.");
    }

    [Fact]
    public void ValidatePicks_WhenTwoFirstRoundWinnersShareTheSameSeries_ThrowsPerSeriesMessage()
    {
        // Arrange: 1 y 2 juegan entre si en el nodo 1; el nodo 2 (3 vs 4) queda sin ganador.
        var strategy = new NhlBestOfSevenStrategy();
        var nodes = BuildBracket(fillFirstRound: true);
        var picks = CoherentPicks();
        picks[FirstRound] = new List<int> { 1, 2, 5, 7, 9, 11, 13, 15 };

        // Act
        var act = () => strategy.ValidatePicks(nodes, picks);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Debes elegir un ganador por cada llave de R1.");
    }

    [Fact]
    public void ValidatePicks_WithOneWinnerPerSeriesAndEastVersusWestFinal_DoesNotThrow()
    {
        // Arrange: cuadro Este = R1 nodos 1-4 (equipos 1-8), Oeste = nodos 5-8 (equipos 9-16).
        var strategy = new NhlBestOfSevenStrategy();
        var nodes = BuildBracket(fillFirstRound: true);
        var picks = new Dictionary<string, IReadOnlyList<int>>
        {
            [FirstRound] = new List<int> { 2, 3, 6, 7, 10, 11, 14, 15 },
            [SecondRound] = new List<int> { 2, 7, 10, 15 },
            [ConferenceFinal] = new List<int> { 7, 10 },
            [Final] = new List<int> { 10 }
        };

        // Act
        var act = () => strategy.ValidatePicks(nodes, picks);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void EligibleTeamIds_ForSecondRound_ExcludesTeamsOutsideTheField()
    {
        // Arrange
        var strategy = new NhlBestOfSevenStrategy();
        var nodes = BuildBracket(fillFirstRound: true);
        var previous = new Dictionary<string, IReadOnlyList<int>> { [FirstRound] = new List<int> { 1, 3, 5, 7, 9, 11, 13, 99 } };

        // Act
        var eligible = strategy.EligibleTeamIds(SecondRound, nodes, previous);

        // Assert
        eligible.Should().Equal(1, 3, 5, 7, 9, 11, 13);
    }

    [Fact]
    public void ValidatePicks_WhenSecondRoundTeamWasNotPickedInFirstRound_Throws()
    {
        // Arrange
        var strategy = new NhlBestOfSevenStrategy();
        var nodes = BuildBracket(fillFirstRound: true);
        var picks = CoherentPicks();
        picks[SecondRound] = new List<int> { 1, 3, 5, 16 }; // 16 era perdedor en R1 (no elegido)

        // Act
        var act = () => strategy.ValidatePicks(nodes, picks);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Los equipos de R2 deben haber sido elegidos en la ronda anterior.");
    }

    [Fact]
    public void ValidatePicks_WhenChampionIsNotAFinalist_ThrowsChampionMessage()
    {
        // Arrange
        var strategy = new NhlBestOfSevenStrategy();
        var nodes = BuildBracket(fillFirstRound: true);
        var picks = CoherentPicks();
        picks[Final] = new List<int> { 3 };

        // Act
        var act = () => strategy.ValidatePicks(nodes, picks);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("El Campeón debe ser uno de los finalistas elegidos.");
    }

    [Fact]
    public void ValidatePicks_WhenFirstRoundHasDuplicatedTeams_ThrowsCountMessage()
    {
        // Arrange
        var strategy = new NhlBestOfSevenStrategy();
        var nodes = BuildBracket(fillFirstRound: true);
        var picks = CoherentPicks();
        picks[FirstRound] = new List<int> { 1, 1, 5, 7, 9, 11, 13, 15 };

        // Act
        var act = () => strategy.ValidatePicks(nodes, picks);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Debes elegir 8 ganadores distintos en R1.");
    }

    [Fact]
    public void ValidatePicks_WhenFirstRoundTeamIsNotInTheField_Throws()
    {
        // Arrange
        var strategy = new NhlBestOfSevenStrategy();
        var nodes = BuildBracket(fillFirstRound: true);
        var picks = CoherentPicks();
        picks[FirstRound] = new List<int> { 1, 3, 5, 7, 9, 11, 13, 99 };

        // Act
        var act = () => strategy.ValidatePicks(nodes, picks);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Los equipos de R1 deben haber sido elegidos en la ronda anterior.");
    }

    [Fact]
    public void EligibleTeamIds_ForSecondRound_ReturnsFirstRoundPicks()
    {
        // Arrange
        var strategy = new NhlBestOfSevenStrategy();
        var nodes = BuildBracket(fillFirstRound: true);
        var previous = new Dictionary<string, IReadOnlyList<int>> { [FirstRound] = new List<int> { 1, 3, 5, 7, 9, 11, 13, 15 } };

        // Act
        var eligible = strategy.EligibleTeamIds(SecondRound, nodes, previous);

        // Assert
        eligible.Should().Equal(1, 3, 5, 7, 9, 11, 13, 15);
    }

    [Fact]
    public void EligibleTeamIds_ForFirstRound_ReturnsAllSixteenTeams()
    {
        // Arrange
        var strategy = new NhlBestOfSevenStrategy();
        var nodes = BuildBracket(fillFirstRound: true);

        // Act
        var eligible = strategy.EligibleTeamIds(FirstRound, nodes, new Dictionary<string, IReadOnlyList<int>>());

        // Assert
        eligible.Should().BeEquivalentTo(Enumerable.Range(1, 16));
    }

    [Fact]
    public void AdvanceWinners_FirstRoundNodesThreeAndFour_FeedSecondRoundNodeTen()
    {
        // Arrange
        var strategy = new NhlBestOfSevenStrategy();
        var nodes = BuildBracket(fillFirstRound: true);
        Resolve(nodes.Single(node => node.NodeNumber == 3), winnerTeamId: 5);
        Resolve(nodes.Single(node => node.NodeNumber == 4), winnerTeamId: 8);

        // Act
        var modified = strategy.AdvanceWinners(nodes);

        // Assert
        var target = nodes.Single(node => node.NodeNumber == 10);
        target.TeamAId.Should().Be(5);
        target.TeamBId.Should().Be(8);
        modified.Should().ContainSingle().Which.Should().BeSameAs(target);
    }

    [Fact]
    public void AdvanceWinners_OddNodeFillsSlotAAndEvenNodeFillsSlotB_CarryingSeed()
    {
        // Arrange
        var strategy = new NhlBestOfSevenStrategy();
        var nodes = BuildBracket(fillFirstRound: true);
        var first = nodes.Single(node => node.NodeNumber == 1);
        first.SeedB = 4;
        Resolve(first, winnerTeamId: 2);
        Resolve(nodes.Single(node => node.NodeNumber == 2), winnerTeamId: 3);

        // Act
        strategy.AdvanceWinners(nodes);

        // Assert
        var target = nodes.Single(node => node.NodeNumber == 9);
        target.TeamAId.Should().Be(2);
        target.SeedA.Should().Be(4);
        target.TeamBId.Should().Be(3);
    }

    [Fact]
    public void AdvanceWinners_ConferenceFinalWinnersFeedTheFinal()
    {
        // Arrange
        var strategy = new NhlBestOfSevenStrategy();
        var nodes = BuildBracket(fillFirstRound: false);
        var cf1 = nodes.Single(node => node.NodeNumber == 13);
        cf1.TeamAId = 1; cf1.TeamA = MakeTeam(1, "1"); cf1.TeamBId = 2; cf1.TeamB = MakeTeam(2, "2");
        var cf2 = nodes.Single(node => node.NodeNumber == 14);
        cf2.TeamAId = 3; cf2.TeamA = MakeTeam(3, "3"); cf2.TeamBId = 4; cf2.TeamB = MakeTeam(4, "4");
        Resolve(cf1, winnerTeamId: 2);
        Resolve(cf2, winnerTeamId: 3);

        // Act
        strategy.AdvanceWinners(nodes);

        // Assert
        var final = nodes.Single(node => node.NodeNumber == 15);
        final.TeamAId.Should().Be(2);
        final.TeamBId.Should().Be(3);
    }

    [Fact]
    public void AdvanceWinners_WhenTargetNodeIsAlreadyResolved_DoesNotTouchIt()
    {
        // Arrange
        var strategy = new NhlBestOfSevenStrategy();
        var nodes = BuildBracket(fillFirstRound: true);
        var target = nodes.Single(node => node.NodeNumber == 9);
        target.TeamAId = 1;
        target.TeamBId = 3;
        target.IsResolved = true;
        target.WinnerTeamId = 1;
        Resolve(nodes.Single(node => node.NodeNumber == 1), winnerTeamId: 2);

        // Act
        var modified = strategy.AdvanceWinners(nodes);

        // Assert
        modified.Should().NotContain(target);
        target.TeamAId.Should().Be(1);
        target.TeamBId.Should().Be(3);
    }

    [Fact]
    public void AdvanceWinners_WhenTargetSlotAlreadyAssigned_DoesNotOverwriteIt()
    {
        // Arrange
        const int publishedTeamId = 99;
        var strategy = new NhlBestOfSevenStrategy();
        var nodes = BuildBracket(fillFirstRound: true);
        var target = nodes.Single(node => node.NodeNumber == 9);
        target.TeamAId = publishedTeamId;
        target.TeamA = MakeTeam(publishedTeamId, "99");
        Resolve(nodes.Single(node => node.NodeNumber == 1), winnerTeamId: 2);
        Resolve(nodes.Single(node => node.NodeNumber == 2), winnerTeamId: 3);

        // Act
        var modified = strategy.AdvanceWinners(nodes);

        // Assert
        target.TeamAId.Should().Be(publishedTeamId);
        target.TeamBId.Should().Be(3);
        modified.Should().ContainSingle().Which.Should().BeSameAs(target);
    }

    [Fact]
    public void AdvanceWinners_WhenTargetPublishedByEspnWithBothTeams_DoesNotTouchIt()
    {
        // Arrange
        var strategy = new NhlBestOfSevenStrategy();
        var nodes = BuildBracket(fillFirstRound: true);
        var target = nodes.Single(node => node.NodeNumber == 9);
        target.EspnEventGroupKey = "post-season:7-14";
        target.TeamAId = 7;
        target.TeamBId = 14;
        Resolve(nodes.Single(node => node.NodeNumber == 1), winnerTeamId: 2);
        Resolve(nodes.Single(node => node.NodeNumber == 2), winnerTeamId: 3);

        // Act
        var modified = strategy.AdvanceWinners(nodes);

        // Assert
        modified.Should().BeEmpty();
        target.TeamAId.Should().Be(7);
        target.TeamBId.Should().Be(14);
    }

    [Fact]
    public void AdvanceWinners_WhenNothingIsResolved_ReturnsEmpty()
    {
        // Arrange
        var strategy = new NhlBestOfSevenStrategy();
        var nodes = BuildBracket(fillFirstRound: true);

        // Act
        var modified = strategy.AdvanceWinners(nodes);

        // Assert
        modified.Should().BeEmpty();
    }

    private static Dictionary<string, IReadOnlyList<int>> CoherentPicks()
    {
        return new Dictionary<string, IReadOnlyList<int>>
        {
            [FirstRound] = new List<int> { 1, 3, 5, 7, 9, 11, 13, 15 },
            [SecondRound] = new List<int> { 1, 5, 9, 13 },
            [ConferenceFinal] = new List<int> { 1, 9 },
            [Final] = new List<int> { 9 }
        };
    }

    private static void Resolve(LiguillaNode node, int winnerTeamId)
    {
        node.IsResolved = true;
        node.WinnerTeamId = winnerTeamId;
    }

    /// <summary>Llave de 15 nodos; la primera ronda reparte los equipos 1..16 en pares (1v2, 3v4...).</summary>
    private static List<LiguillaNode> BuildBracket(bool fillFirstRound)
    {
        var nodes = new List<LiguillaNode>();
        for (var number = 1; number <= 15; number++)
        {
            var round = number <= 8 ? FirstRound : number <= 12 ? SecondRound : number <= 14 ? ConferenceFinal : Final;
            var node = new LiguillaNode { NodeNumber = number, Round = round };
            if (fillFirstRound && number <= 8)
            {
                var teamA = (number * 2) - 1;
                node.TeamAId = teamA;
                node.TeamA = MakeTeam(teamA, teamA.ToString());
                node.TeamBId = teamA + 1;
                node.TeamB = MakeTeam(teamA + 1, (teamA + 1).ToString());
            }

            nodes.Add(node);
        }

        return nodes;
    }

    private static LiguillaNode BuildNode(int number, string round, Team teamA, Team teamB)
    {
        return new LiguillaNode
        {
            NodeNumber = number,
            Round = round,
            TeamAId = teamA.Id,
            TeamA = teamA,
            TeamBId = teamB.Id,
            TeamB = teamB
        };
    }

    private static Team MakeTeam(int id, string espnId)
    {
        return new Team { Id = id, EspnTeamId = espnId };
    }
}
