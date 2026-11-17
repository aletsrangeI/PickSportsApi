using Domain.Entities;
using FluentAssertions;
using Interface.UseCases.Knockout;
using UseCases.Liguilla.Strategies;
using Xunit;

namespace PickSportsApi.UnitTests.UseCasesTests.Knockout;

public class NflReseededStrategyTests
{
    private const string WildCard = "WC";
    private const string Divisional = "DIV";
    private const string ConferenceRound = "CONF";
    private const string SuperBowl = "SB";
    private const string Afc = "afc";
    private const string Nfc = "nfc";

    // Ids de BD: AFC 1..7 (id = seed), NFC 11..17 (id = 10 + seed).
    private const int NfcOffset = 10;

    [Fact]
    public void Rounds_ExposeNflCodesPointsAndNodeCounts()
    {
        // Arrange
        var strategy = new NflReseededStrategy();

        // Act
        var rounds = strategy.Rounds;

        // Assert
        strategy.FormatCode.Should().Be("NFL_RESEEDED_SINGLE_GAME");
        strategy.FirstPickableRoundCode.Should().Be(WildCard);
        strategy.PerfectBonusPoints.Should().Be(20);
        rounds.Select(round => round.Code).Should().Equal(WildCard, Divisional, ConferenceRound, SuperBowl);
        rounds.Select(round => round.WinnersToPick).Should().Equal(6, 4, 2, 1);
        rounds.Select(round => round.PointsPerHit).Should().Equal(5, 10, 20, 40);
        rounds.Select(round => round.NodeCount).Should().Equal(6, 4, 2, 1);
    }

    [Fact]
    public void IsFieldReady_WithSixCompleteWildCardNodes_IsTrueOnlyWhenRegularSeasonIsFinished()
    {
        // Arrange
        var strategy = new NflReseededStrategy();
        var nodes = BuildBracket();

        // Act
        var finished = strategy.IsFieldReady(nodes, regularSeasonFinished: true);
        var open = strategy.IsFieldReady(nodes, regularSeasonFinished: false);

        // Assert
        finished.Should().BeTrue();
        open.Should().BeFalse();
    }

    [Fact]
    public void IsFieldReady_WhenAWildCardSlotIsEmpty_IsFalse()
    {
        // Arrange
        var strategy = new NflReseededStrategy();
        var nodes = BuildBracket();
        nodes.Single(node => node.NodeNumber == 6).TeamBId = null;

        // Act
        var ready = strategy.IsFieldReady(nodes, regularSeasonFinished: true);

        // Assert
        ready.Should().BeFalse();
    }

    [Fact]
    public void ResolveFromEspn_WithSingleGameWinner_ResolvesTheMatchingTeam()
    {
        // Arrange (fixture nfl-postseason-20260111: partido unico con ganador)
        var strategy = new NflReseededStrategy();
        var node = BuildNode(1, WildCard, Afc, MakeTeam(2, "2"), MakeTeam(7, "7"));
        var tie = new EspnKnockoutTie { TotalLegs = 1, WinnerEspnTeamId = "7" };

        // Act
        var outcome = strategy.ResolveFromEspn(node, tie);

        // Assert
        outcome.IsResolved.Should().BeTrue();
        outcome.WinnerTeamId.Should().Be(7);
        outcome.NodeNumber.Should().Be(1);
    }

    [Fact]
    public void ResolveFromEspn_WhenGameHasNoWinnerYet_IsNotResolved()
    {
        // Arrange
        var strategy = new NflReseededStrategy();
        var node = BuildNode(1, WildCard, Afc, MakeTeam(2, "2"), MakeTeam(7, "7"));
        var tie = new EspnKnockoutTie { TotalLegs = 1, WinnerEspnTeamId = null };

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
        var strategy = new NflReseededStrategy();
        var node = BuildNode(1, WildCard, Afc, MakeTeam(2, "2"), MakeTeam(7, "7"));
        var tie = new EspnKnockoutTie { WinnerEspnTeamId = "999" };

        // Act
        var outcome = strategy.ResolveFromEspn(node, tie);

        // Assert
        outcome.IsResolved.Should().BeFalse();
    }

    [Fact]
    public void ValidatePicks_WithCoherentPicksAndByeInDivisional_DoesNotThrow()
    {
        // Arrange
        var strategy = new NflReseededStrategy();
        var nodes = BuildBracket();

        // Act
        var act = () => strategy.ValidatePicks(nodes, CoherentPicks());

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidatePicks_WhenDivisionalTeamIsNeitherWildCardPickNorBye_Throws()
    {
        // Arrange
        var strategy = new NflReseededStrategy();
        var nodes = BuildBracket();
        var picks = CoherentPicks();
        picks[Divisional] = new List<int> { 1, 5, 11, 12 }; // 5 no esta en los picks de WC ni es bye

        // Act
        var act = () => strategy.ValidatePicks(nodes, picks);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Los equipos de DIV deben haber sido elegidos en la ronda anterior.");
    }

    [Fact]
    public void ValidatePicks_WhenWildCardTeamIsNotInTheField_Throws()
    {
        // Arrange
        var strategy = new NflReseededStrategy();
        var nodes = BuildBracket();
        var picks = CoherentPicks();
        picks[WildCard] = new List<int> { 2, 3, 4, 12, 13, 99 };

        // Act
        var act = () => strategy.ValidatePicks(nodes, picks);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Los equipos de WC deben haber sido elegidos en la ronda anterior.");
    }

    [Fact]
    public void ValidatePicks_WhenSuperBowlWinnerIsNotAConferenceChampionPick_ThrowsChampionMessage()
    {
        // Arrange
        var strategy = new NflReseededStrategy();
        var nodes = BuildBracket();
        var picks = CoherentPicks();
        picks[SuperBowl] = new List<int> { 2 };

        // Act
        var act = () => strategy.ValidatePicks(nodes, picks);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("El Campeón debe ser uno de los finalistas elegidos.");
    }

    [Fact]
    public void ValidatePicks_WhenWildCardPicksAreIncomplete_ThrowsCountMessage()
    {
        // Arrange
        var strategy = new NflReseededStrategy();
        var nodes = BuildBracket();
        var picks = CoherentPicks();
        picks[WildCard] = new List<int> { 2, 3, 4 };

        // Act
        var act = () => strategy.ValidatePicks(nodes, picks);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Debes elegir 6 ganadores distintos en WC.");
    }

    [Fact]
    public void EligibleTeamIds_ForDivisional_IncludesTheByeTeamsAndWildCardPicks()
    {
        // Arrange
        var strategy = new NflReseededStrategy();
        var nodes = BuildBracket();
        var previous = new Dictionary<string, IReadOnlyList<int>> { [WildCard] = new List<int> { 2, 3, 4, 12, 13, 14 } };

        // Act
        var eligible = strategy.EligibleTeamIds(Divisional, nodes, previous);

        // Assert
        eligible.Should().BeEquivalentTo(new[] { 2, 3, 4, 12, 13, 14, 1, 11 });
    }

    [Fact]
    public void EligibleTeamIds_ForWildCard_DoesNotIncludeTheByeTeams()
    {
        // Arrange
        var strategy = new NflReseededStrategy();
        var nodes = BuildBracket();

        // Act
        var eligible = strategy.EligibleTeamIds(WildCard, nodes, new Dictionary<string, IReadOnlyList<int>>());

        // Assert
        eligible.Should().BeEquivalentTo(new[] { 2, 3, 4, 5, 6, 7, 12, 13, 14, 15, 16, 17 });
    }

    [Fact]
    public void AdvanceWinners_AfterWildCard_ByeFacesWorstSurvivorAndBestFacesMiddle()
    {
        // Arrange: gana AFC 2v7 -> 7 (sorpresa), 3v6 -> 3, 4v5 -> 5. Sobrevivientes ordenados: 3, 5, 7.
        var strategy = new NflReseededStrategy();
        var nodes = BuildBracket();
        Resolve(nodes.Single(node => node.NodeNumber == 1), winnerTeamId: 7);
        Resolve(nodes.Single(node => node.NodeNumber == 2), winnerTeamId: 3);
        Resolve(nodes.Single(node => node.NodeNumber == 3), winnerTeamId: 5);

        // Act
        var modified = strategy.AdvanceWinners(nodes);

        // Assert
        var first = nodes.Single(node => node.NodeNumber == 7);
        first.TeamAId.Should().Be(1);
        first.SeedA.Should().Be(1);
        first.TeamBId.Should().Be(7);
        first.SeedB.Should().Be(7);
        var second = nodes.Single(node => node.NodeNumber == 8);
        second.TeamAId.Should().Be(3);
        second.TeamBId.Should().Be(5);
        modified.Should().BeEquivalentTo(new[] { first, second });
    }

    [Fact]
    public void AdvanceWinners_WhenWildCardIsIncomplete_DoesNotReseedThatConference()
    {
        // Arrange
        var strategy = new NflReseededStrategy();
        var nodes = BuildBracket();
        Resolve(nodes.Single(node => node.NodeNumber == 1), winnerTeamId: 2);
        Resolve(nodes.Single(node => node.NodeNumber == 2), winnerTeamId: 3);

        // Act
        var modified = strategy.AdvanceWinners(nodes);

        // Assert
        modified.Should().BeEmpty();
        nodes.Single(node => node.NodeNumber == 8).TeamAId.Should().BeNull();
    }

    [Fact]
    public void AdvanceWinners_AfterDivisional_SeedsConferenceFinalOfTheSameConferenceOnly()
    {
        // Arrange
        var strategy = new NflReseededStrategy();
        var nodes = BuildBracket();
        SeedDivisional(nodes, 7, 1, 7, Afc);
        SeedDivisional(nodes, 8, 3, 5, Afc);
        Resolve(nodes.Single(node => node.NodeNumber == 7), winnerTeamId: 1);
        Resolve(nodes.Single(node => node.NodeNumber == 8), winnerTeamId: 5);

        // Act
        var modified = strategy.AdvanceWinners(nodes);

        // Assert
        var afcFinal = nodes.Single(node => node.NodeNumber == 11);
        afcFinal.TeamAId.Should().Be(1);
        afcFinal.TeamBId.Should().Be(5);
        nodes.Single(node => node.NodeNumber == 12).TeamAId.Should().BeNull();
        modified.Should().ContainSingle().Which.Should().BeSameAs(afcFinal);
    }

    [Fact]
    public void AdvanceWinners_AfterConferenceFinals_SeedsSuperBowlWithAfcAsTeamA()
    {
        // Arrange
        var strategy = new NflReseededStrategy();
        var nodes = BuildBracket();
        SeedDivisional(nodes, 11, 1, 5, Afc);
        SeedDivisional(nodes, 12, 11, 12, Nfc);
        Resolve(nodes.Single(node => node.NodeNumber == 11), winnerTeamId: 5);
        Resolve(nodes.Single(node => node.NodeNumber == 12), winnerTeamId: 12);

        // Act
        strategy.AdvanceWinners(nodes);

        // Assert
        var superBowl = nodes.Single(node => node.NodeNumber == 13);
        superBowl.TeamAId.Should().Be(5);
        superBowl.TeamBId.Should().Be(12);
    }

    [Fact]
    public void AdvanceWinners_WhenTargetIsAlreadyResolved_DoesNotTouchIt()
    {
        // Arrange
        var strategy = new NflReseededStrategy();
        var nodes = BuildBracket();
        SeedDivisional(nodes, 11, 1, 5, Afc);
        SeedDivisional(nodes, 12, 11, 12, Nfc);
        Resolve(nodes.Single(node => node.NodeNumber == 11), winnerTeamId: 5);
        Resolve(nodes.Single(node => node.NodeNumber == 12), winnerTeamId: 12);
        var superBowl = nodes.Single(node => node.NodeNumber == 13);
        superBowl.TeamAId = 1;
        superBowl.TeamBId = 11;
        Resolve(superBowl, winnerTeamId: 1);

        // Act
        var modified = strategy.AdvanceWinners(nodes);

        // Assert
        modified.Should().BeEmpty();
        superBowl.TeamAId.Should().Be(1);
    }

    /// <summary>Picks completos y coherentes: AFC 2,3,4 y NFC 12,13,14 ganan WC; DIV incluye byes 1 y 11.</summary>
    private static Dictionary<string, IReadOnlyList<int>> CoherentPicks()
    {
        return new Dictionary<string, IReadOnlyList<int>>
        {
            [WildCard] = new List<int> { 2, 3, 4, 12, 13, 14 },
            [Divisional] = new List<int> { 1, 2, 11, 12 },
            [ConferenceRound] = new List<int> { 1, 11 },
            [SuperBowl] = new List<int> { 1 }
        };
    }

    private static void SeedDivisional(List<LiguillaNode> nodes, int number, int teamAId, int teamBId, string conference)
    {
        var node = nodes.Single(item => item.NodeNumber == number);
        node.Conference = conference;
        node.TeamAId = teamAId;
        node.TeamA = MakeTeam(teamAId, teamAId.ToString());
        node.TeamBId = teamBId;
        node.TeamB = MakeTeam(teamBId, teamBId.ToString());
    }

    private static void Resolve(LiguillaNode node, int winnerTeamId)
    {
        node.IsResolved = true;
        node.WinnerTeamId = winnerTeamId;
    }

    /// <summary>
    /// Llave NFL: WC 1-3 AFC (2v7, 3v6, 4v5) y 4-6 NFC; DIV 7-8 AFC y 9-10 NFC con el seed 1 pre-sembrado en 7 y 9;
    /// CONF 11 AFC y 12 NFC; SB 13. Los seeds coinciden con el numero de equipo (NFC con offset 10).
    /// </summary>
    private static List<LiguillaNode> BuildBracket()
    {
        var nodes = new List<LiguillaNode>();
        var pairings = new[] { (2, 7), (3, 6), (4, 5) };

        for (var index = 0; index < pairings.Length; index++)
        {
            nodes.Add(WildCardNode(1 + index, Afc, pairings[index].Item1, pairings[index].Item2, 0));
            nodes.Add(WildCardNode(4 + index, Nfc, pairings[index].Item1, pairings[index].Item2, NfcOffset));
        }

        nodes.Add(NewNode(7, Divisional, Afc));
        nodes.Add(NewNode(8, Divisional, Afc));
        nodes.Add(NewNode(9, Divisional, Nfc));
        nodes.Add(NewNode(10, Divisional, Nfc));
        nodes.Add(NewNode(11, ConferenceRound, Afc));
        nodes.Add(NewNode(12, ConferenceRound, Nfc));
        nodes.Add(NewNode(13, SuperBowl, null));

        PlaceBye(nodes.Single(node => node.NodeNumber == 7), 1);
        PlaceBye(nodes.Single(node => node.NodeNumber == 9), 1 + NfcOffset);

        return nodes;
    }

    private static void PlaceBye(LiguillaNode node, int teamId)
    {
        node.TeamAId = teamId;
        node.TeamA = MakeTeam(teamId, teamId.ToString());
        node.SeedA = 1;
    }

    private static LiguillaNode WildCardNode(int number, string conference, int seedA, int seedB, int offset)
    {
        var node = BuildNode(number, WildCard, conference, MakeTeam(seedA + offset, (seedA + offset).ToString()), MakeTeam(seedB + offset, (seedB + offset).ToString()));
        node.SeedA = seedA;
        node.SeedB = seedB;

        return node;
    }

    private static LiguillaNode NewNode(int number, string round, string? conference)
    {
        return new LiguillaNode { NodeNumber = number, Round = round, Conference = conference };
    }

    private static LiguillaNode BuildNode(int number, string round, string conference, Team teamA, Team teamB)
    {
        return new LiguillaNode
        {
            NodeNumber = number,
            Round = round,
            Conference = conference,
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
