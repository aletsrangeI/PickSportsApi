using Domain.Common;
using FluentAssertions;
using UseCases.Liguilla.Simulation;

namespace PickSportsApi.UnitTests.UseCasesTests.Knockout;

public class UefaSimulationBuilderTests
{
    private const string RoundOfSixteen = "R16";
    private const int TieCount = 8;
    private const int TeamCount = 16;
    private const int RankPairSum = 17;

    private static readonly string[] ExpectedTeamA =
    {
        "160", "86", "359", "2572", "83", "362", "382", "364"
    };

    private static readonly string[] ExpectedTeamB =
    {
        "4411", "110", "134", "132", "124", "244", "2250", "570"
    };

    private readonly UefaSimulationBuilder _builder = new();

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Espn", name + ".json"));

    private static string StandingsJson(params string[] entries) =>
        $"{{\"children\":[{{\"standings\":{{\"entries\":[{string.Join(",", entries)}]}}}}]}}";

    private static string StringEntry(string id, int rank) =>
        $"{{\"team\":{{\"id\":\"{id}\"}},\"stats\":[{{\"name\":\"rank\",\"value\":{rank}}}]}}";

    private static string NumericEntry(int id, int rank) =>
        $"{{\"team\":{{\"id\":{id}}},\"stats\":[{{\"name\":\"rank\",\"value\":{rank}}}]}}";

    [Fact]
    public void FormatCode_IsUefaTwoLeg()
    {
        // Arrange

        // Act
        var formatCode = _builder.FormatCode;

        // Assert
        formatCode.Should().Be(KnockoutFormatCodes.UefaTwoLeg);
    }

    [Fact]
    public void StandingsPath_Always_ReturnsChampionsLeagueStandingsPath()
    {
        // Arrange
        const string expected = "apis/v2/sports/soccer/uefa.champions/standings";

        // Act
        var path = _builder.StandingsPath("soccer/uefa.champions");

        // Assert
        path.Should().Be(expected);
    }

    [Fact]
    public void Build_UclStandingsToday_ReturnsEightSeededRoundOfSixteenTies()
    {
        // Arrange
        var json = Fixture("ucl-standings-today");

        // Act
        var ties = _builder.Build(json);

        // Assert
        ties.Should().HaveCount(TieCount);
        for (var index = 0; index < TieCount; index++)
        {
            var node = index + 1;
            var tie = ties.Single(t => t.NodeNumber == node);
            tie.RoundCode.Should().Be(RoundOfSixteen);
            tie.Conference.Should().BeNull();
            tie.SeedA.Should().Be(node);
            tie.SeedB.Should().Be(RankPairSum - node);
            tie.EspnTeamIdA.Should().Be(ExpectedTeamA[index]);
            tie.EspnTeamIdB.Should().Be(ExpectedTeamB[index]);
        }
    }

    [Fact]
    public void Build_UclStandingsToday_UsesSixteenDistinctTeamsAndNodesOneToEight()
    {
        // Arrange
        var json = Fixture("ucl-standings-today");

        // Act
        var ties = _builder.Build(json);

        // Assert
        ties.Select(t => t.NodeNumber).Should().BeEquivalentTo(Enumerable.Range(1, TieCount));
        ties.Should().OnlyContain(t => t.SeedA + t.SeedB == RankPairSum);
        var ids = ties
            .SelectMany(t => new string?[] { t.EspnTeamIdA, t.EspnTeamIdB })
            .Where(id => id != null)
            .ToList();
        ids.Should().HaveCount(TeamCount);
        ids.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Build_NumericTeamIdsInReverseRankOrder_StillSeedsByRank()
    {
        // Arrange
        var entries = Enumerable.Range(1, TeamCount)
            .Reverse()
            .Select(rank => NumericEntry(100 + rank, rank))
            .ToArray();
        var json = StandingsJson(entries);

        // Act
        var ties = _builder.Build(json);

        // Assert
        ties.Should().HaveCount(TieCount);
        var first = ties.Single(t => t.NodeNumber == 1);
        first.EspnTeamIdA.Should().Be("101");
        first.EspnTeamIdB.Should().Be("116");

        var last = ties.Single(t => t.NodeNumber == TieCount);
        last.EspnTeamIdA.Should().Be("108");
        last.EspnTeamIdB.Should().Be("109");
    }

    [Fact]
    public void Build_JsonWithFewerThanSixteenRankedTeams_ReturnsEmpty()
    {
        // Arrange
        var entries = Enumerable.Range(1, TeamCount - 1)
            .Select(rank => StringEntry((100 + rank).ToString(), rank))
            .ToArray();
        var json = StandingsJson(entries);

        // Act
        var ties = _builder.Build(json);

        // Assert
        ties.Should().BeEmpty();
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
