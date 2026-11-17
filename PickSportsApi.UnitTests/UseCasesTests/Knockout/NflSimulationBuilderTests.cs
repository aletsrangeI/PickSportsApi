using Domain.Common;
using FluentAssertions;
using UseCases.Liguilla.Espn;
using UseCases.Liguilla.Simulation;

namespace PickSportsApi.UnitTests.UseCasesTests.Knockout;

public class NflSimulationBuilderTests
{
    private const string WildCard = "WC";
    private const string Divisional = "DIV";
    private const string Afc = "afc";
    private const string Nfc = "nfc";

    private const int WildCardCount = 6;
    private const int ByeCount = 2;
    private const int TotalTieCount = 8;
    private const int DistinctTeamCount = WildCardCount * 2 + ByeCount;
    private const int SeedPairSum = 9;
    private const int ByeSeed = 1;
    private const int AfcDivisionalNode = 7;
    private const int NfcDivisionalNode = 9;

    private static readonly (int Node, string Conference, string TeamA, string TeamB)[] ExpectedWildCards =
    {
        (1, Afc, "12", "5"),
        (2, Afc, "30", "23"),
        (3, Afc, "4", "7"),
        (4, Nfc, "8", "9"),
        (5, Nfc, "19", "3"),
        (6, Nfc, "29", "16")
    };

    private readonly NflSimulationBuilder _builder = new(new EspnKnockoutParser());

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Espn", name + ".json"));

    [Fact]
    public void FormatCode_IsNflReseededSingleGame()
    {
        // Arrange

        // Act
        var formatCode = _builder.FormatCode;

        // Assert
        formatCode.Should().Be(KnockoutFormatCodes.NflReseededSingleGame);
    }

    [Fact]
    public void StandingsPath_Always_ReturnsNflStandingsPath()
    {
        // Arrange
        const string expected = "apis/v2/sports/football/nfl/standings";

        // Act
        var path = _builder.StandingsPath("football/nfl");

        // Assert
        path.Should().Be(expected);
    }

    [Fact]
    public void Build_NflStandingsToday_ReturnsSixWildCardTiesWithExpectedNodesAndConferences()
    {
        // Arrange
        var json = Fixture("nfl-standings-2026-today");

        // Act
        var ties = _builder.Build(json);

        // Assert
        var wildCards = ties.Where(t => t.RoundCode == WildCard).ToList();
        wildCards.Should().HaveCount(WildCardCount);
        wildCards.Select(t => t.NodeNumber).Should().BeEquivalentTo(new[] { 1, 2, 3, 4, 5, 6 });

        foreach (var expected in ExpectedWildCards)
        {
            var tie = wildCards.Single(t => t.NodeNumber == expected.Node);
            tie.Conference.Should().Be(expected.Conference);
            tie.EspnTeamIdA.Should().Be(expected.TeamA);
            tie.EspnTeamIdB.Should().Be(expected.TeamB);
        }
    }

    [Fact]
    public void Build_NflStandingsToday_ReturnsTwoByeTiesWithNullTeamB()
    {
        // Arrange
        var json = Fixture("nfl-standings-2026-today");

        // Act
        var ties = _builder.Build(json);

        // Assert
        var byes = ties.Where(t => t.RoundCode == Divisional).ToList();
        byes.Should().HaveCount(ByeCount);
        byes.Should().OnlyContain(t => t.EspnTeamIdB == null && t.SeedB == null && t.SeedA == ByeSeed);

        var afcBye = byes.Single(t => t.NodeNumber == AfcDivisionalNode);
        afcBye.Conference.Should().Be(Afc);
        afcBye.EspnTeamIdA.Should().Be("2");

        var nfcBye = byes.Single(t => t.NodeNumber == NfcDivisionalNode);
        nfcBye.Conference.Should().Be(Nfc);
        nfcBye.EspnTeamIdA.Should().Be("25");
    }

    [Fact]
    public void Build_NflStandingsToday_EveryWildCardPitsBetterSeedAndSeedsSumNine()
    {
        // Arrange
        var json = Fixture("nfl-standings-2026-today");

        // Act
        var wildCards = _builder.Build(json).Where(t => t.RoundCode == WildCard).ToList();

        // Assert
        wildCards.Should().HaveCount(WildCardCount);
        wildCards.Should().OnlyContain(t =>
            t.SeedB.HasValue && t.SeedA < t.SeedB.Value && t.SeedA + t.SeedB.Value == SeedPairSum);
    }

    [Fact]
    public void Build_NflStandingsToday_HasNoRepeatedTeamsAcrossWildCardsAndByes()
    {
        // Arrange
        var json = Fixture("nfl-standings-2026-today");

        // Act
        var ties = _builder.Build(json);

        // Assert
        ties.Should().HaveCount(TotalTieCount);
        var allIds = ties
            .SelectMany(t => new string?[] { t.EspnTeamIdA, t.EspnTeamIdB })
            .Where(id => id != null)
            .ToList();
        allIds.Should().HaveCount(DistinctTeamCount);
        allIds.Should().OnlyHaveUniqueItems();

        var wildCardIds = ties
            .Where(t => t.RoundCode == WildCard)
            .SelectMany(t => new[] { t.EspnTeamIdA, t.EspnTeamIdB ?? string.Empty })
            .ToList();
        var byeIds = ties.Where(t => t.RoundCode == Divisional).Select(t => t.EspnTeamIdA).ToList();
        wildCardIds.Should().NotIntersectWith(byeIds);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{no")]
    [InlineData("{}")]
    public void Build_InvalidOrEmptyJson_ReturnsEmpty(string json)
    {
        // Arrange

        // Act
        var ties = _builder.Build(json);

        // Assert
        ties.Should().BeEmpty();
    }
}
