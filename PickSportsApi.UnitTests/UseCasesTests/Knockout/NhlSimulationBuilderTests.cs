using Domain.Common;
using FluentAssertions;
using Interface.UseCases.Knockout;
using UseCases.Liguilla.Espn;
using UseCases.Liguilla.Simulation;

namespace PickSportsApi.UnitTests.UseCasesTests.Knockout;

public class NhlSimulationBuilderTests
{
    private const string NhlStandings2027Fixture = "nhl-standings-2027-today-level3";
    private const string ExpectedStandingsPath = "apis/v2/sports/hockey/nhl/standings?level=3";
    private const string FirstRoundCode = "R1";
    private const string EastConference = "east";
    private const string WestConference = "west";
    private const string AtlanticDivision = "atlantic";
    private const string MetropolitanDivision = "metropolitan";
    private const string CentralDivision = "central";
    private const string PacificDivision = "pacific";
    private const int ExpectedNodeCount = 8;
    private const int ExpectedDistinctTeamsCount = 16;

    private readonly EspnKnockoutParser _parser = new();
    private readonly NhlSimulationBuilder _builder;

    public NhlSimulationBuilderTests()
    {
        _builder = new NhlSimulationBuilder(_parser);
    }

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Espn", name + ".json"));

    [Fact]
    public void Properties_DefaultConfiguration_ExposeNhlFormatAndStandingsPath()
    {
        // Arrange & Act
        var formatCode = _builder.FormatCode;
        var standingsPath = _builder.StandingsPath("nhl");

        // Assert
        formatCode.Should().Be(KnockoutFormatCodes.NhlBestOf7);
        standingsPath.Should().Be(ExpectedStandingsPath);
    }

    [Fact]
    public void Build_RealStandings2027_ReturnsEightNodesWithCorrectConferencesAndDistinctTeams()
    {
        // Arrange
        var json = Fixture(NhlStandings2027Fixture);

        // Act
        var ties = _builder.Build(json);

        // Assert
        ties.Should().HaveCount(ExpectedNodeCount);
        ties.Select(t => t.NodeNumber).Should().Equal(1, 2, 3, 4, 5, 6, 7, 8);
        ties.Should().OnlyContain(t => t.RoundCode == FirstRoundCode);
        ties.Where(t => t.NodeNumber <= 4).Should().OnlyContain(t => t.Conference == EastConference);
        ties.Where(t => t.NodeNumber >= 5).Should().OnlyContain(t => t.Conference == WestConference);

        var allTeamIds = new List<string>(ExpectedDistinctTeamsCount);
        foreach (var tie in ties)
        {
            Assert.NotNull(tie.EspnTeamIdB);
            allTeamIds.Add(tie.EspnTeamIdA);
            allTeamIds.Add(tie.EspnTeamIdB);
        }

        allTeamIds.Should().HaveCount(ExpectedDistinctTeamsCount);
        allTeamIds.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Build_RealStandings2027_PairsBestDivisionWinnerWithWorstWildCardAndPlacesDivisionChampionsInA()
    {
        // Arrange
        var json = Fixture(NhlStandings2027Fixture);
        var seeds = _parser.ParseNhlPlayoffSeeds(json);

        // Act
        var ties = _builder.Build(json);
        var byNode = ties.ToDictionary(t => t.NodeNumber);

        // Assert
        var expectedChampDivisions = new Dictionary<int, string>
        {
            [1] = AtlanticDivision,
            [3] = MetropolitanDivision,
            [5] = CentralDivision,
            [7] = PacificDivision
        };

        foreach (var (nodeNumber, expectedDivision) in expectedChampDivisions)
        {
            var tie = byNode[nodeNumber];
            Assert.NotNull(tie.EspnTeamIdB);
            Assert.NotNull(tie.SeedB);

            var teamA = seeds[tie.EspnTeamIdA];
            var teamB = seeds[tie.EspnTeamIdB];

            teamA.IsDivisionWinner.Should().BeTrue();
            teamA.Division.Should().Be(expectedDivision);
            teamB.IsDivisionWinner.Should().BeFalse();
            tie.SeedA.Should().Be(teamA.Seed);
            tie.SeedB.Should().Be(teamB.Seed);
        }

        // En 2027, el mejor campeon del Este (Metro #1) enfrenta al comodin de peor seed (Atlantic #8) y la otra division queda cruzada.
        var eastBestWinnerTie = byNode[3];
        var eastOtherWinnerTie = byNode[1];
        Assert.NotNull(eastBestWinnerTie.EspnTeamIdB);
        Assert.NotNull(eastOtherWinnerTie.EspnTeamIdB);
        Assert.NotNull(eastBestWinnerTie.SeedB);
        Assert.NotNull(eastOtherWinnerTie.SeedB);
        eastBestWinnerTie.SeedA.Should().BeLessThan(eastOtherWinnerTie.SeedA);
        eastBestWinnerTie.SeedB.Value.Should().BeGreaterThan(eastOtherWinnerTie.SeedB.Value);
        seeds[eastBestWinnerTie.EspnTeamIdB].Division.Should().NotBe(seeds[eastBestWinnerTie.EspnTeamIdA].Division);

        // En 2027, el mejor campeon del Oeste (Pacific #1) enfrenta al comodin de peor seed (Central #8).
        var westBestWinnerTie = byNode[7];
        var westOtherWinnerTie = byNode[5];
        Assert.NotNull(westBestWinnerTie.EspnTeamIdB);
        Assert.NotNull(westOtherWinnerTie.EspnTeamIdB);
        Assert.NotNull(westBestWinnerTie.SeedB);
        Assert.NotNull(westOtherWinnerTie.SeedB);
        westBestWinnerTie.SeedA.Should().BeLessThan(westOtherWinnerTie.SeedA);
        westBestWinnerTie.SeedB.Value.Should().BeGreaterThan(westOtherWinnerTie.SeedB.Value);
        seeds[westBestWinnerTie.EspnTeamIdB].Division.Should().NotBe(seeds[westBestWinnerTie.EspnTeamIdA].Division);
    }

    [Fact]
    public void Build_RealStandings2027_PairsSecondVsThirdWithinEachDivisionWithLowerSeedInA()
    {
        // Arrange
        var json = Fixture(NhlStandings2027Fixture);
        var seeds = _parser.ParseNhlPlayoffSeeds(json);

        // Act
        var ties = _builder.Build(json);
        var byNode = ties.ToDictionary(t => t.NodeNumber);

        // Assert
        var expectedDivisions = new Dictionary<int, string>
        {
            [2] = AtlanticDivision,
            [4] = MetropolitanDivision,
            [6] = CentralDivision,
            [8] = PacificDivision
        };

        foreach (var (nodeNumber, expectedDivision) in expectedDivisions)
        {
            var tie = byNode[nodeNumber];
            Assert.NotNull(tie.EspnTeamIdB);
            Assert.NotNull(tie.SeedB);

            var teamA = seeds[tie.EspnTeamIdA];
            var teamB = seeds[tie.EspnTeamIdB];

            teamA.Division.Should().Be(expectedDivision);
            teamB.Division.Should().Be(expectedDivision);
            teamA.IsDivisionWinner.Should().BeFalse();
            teamB.IsDivisionWinner.Should().BeFalse();
            teamA.Seed.Should().BeLessThan(teamB.Seed);
            tie.SeedA.Should().Be(teamA.Seed);
            tie.SeedB.Should().Be(teamB.Seed);
        }
    }

    [Fact]
    public void Build_FourQualifiersPerDivision_NeverMatchesChampionWithSameDivisionWildCard()
    {
        // Arrange
        const string json = """
            {
              "children": [
                {
                  "name": "Eastern Conference",
                  "children": [
                    {
                      "name": "Atlantic Division",
                      "standings": {
                        "entries": [
                          { "team": { "id": "A1" }, "stats": [ { "name": "playoffSeed", "value": 1 }, { "name": "points", "value": 110 } ] },
                          { "team": { "id": "A2" }, "stats": [ { "name": "playoffSeed", "value": 3 }, { "name": "points", "value": 100 } ] },
                          { "team": { "id": "A3" }, "stats": [ { "name": "playoffSeed", "value": 5 }, { "name": "points", "value": 95 } ] },
                          { "team": { "id": "A4" }, "stats": [ { "name": "playoffSeed", "value": 7 }, { "name": "points", "value": 90 } ] }
                        ]
                      }
                    },
                    {
                      "name": "Metropolitan Division",
                      "standings": {
                        "entries": [
                          { "team": { "id": "M1" }, "stats": [ { "name": "playoffSeed", "value": 2 }, { "name": "points", "value": 108 } ] },
                          { "team": { "id": "M2" }, "stats": [ { "name": "playoffSeed", "value": 4 }, { "name": "points", "value": 98 } ] },
                          { "team": { "id": "M3" }, "stats": [ { "name": "playoffSeed", "value": 6 }, { "name": "points", "value": 93 } ] },
                          { "team": { "id": "M4" }, "stats": [ { "name": "playoffSeed", "value": 8 }, { "name": "points", "value": 88 } ] }
                        ]
                      }
                    }
                  ]
                },
                {
                  "name": "Western Conference",
                  "children": [
                    {
                      "name": "Central Division",
                      "standings": {
                        "entries": [
                          { "team": { "id": "C1" }, "stats": [ { "name": "playoffSeed", "value": 1 }, { "name": "points", "value": 112 } ] },
                          { "team": { "id": "C2" }, "stats": [ { "name": "playoffSeed", "value": 3 }, { "name": "points", "value": 102 } ] },
                          { "team": { "id": "C3" }, "stats": [ { "name": "playoffSeed", "value": 5 }, { "name": "points", "value": 96 } ] },
                          { "team": { "id": "C4" }, "stats": [ { "name": "playoffSeed", "value": 7 }, { "name": "points", "value": 91 } ] }
                        ]
                      }
                    },
                    {
                      "name": "Pacific Division",
                      "standings": {
                        "entries": [
                          { "team": { "id": "P1" }, "stats": [ { "name": "playoffSeed", "value": 2 }, { "name": "points", "value": 107 } ] },
                          { "team": { "id": "P2" }, "stats": [ { "name": "playoffSeed", "value": 4 }, { "name": "points", "value": 99 } ] },
                          { "team": { "id": "P3" }, "stats": [ { "name": "playoffSeed", "value": 6 }, { "name": "points", "value": 94 } ] },
                          { "team": { "id": "P4" }, "stats": [ { "name": "playoffSeed", "value": 8 }, { "name": "points", "value": 89 } ] }
                        ]
                      }
                    }
                  ]
                }
              ]
            }
            """;
        var seeds = _parser.ParseNhlPlayoffSeeds(json);

        // Act
        var ties = _builder.Build(json);
        var byNode = ties.ToDictionary(t => t.NodeNumber);

        // Assert
        foreach (var nodeNumber in new[] { 1, 3, 5, 7 })
        {
            var tie = byNode[nodeNumber];
            Assert.NotNull(tie.EspnTeamIdB);
            seeds[tie.EspnTeamIdB].Division.Should().NotBe(seeds[tie.EspnTeamIdA].Division);
        }

        byNode[1].EspnTeamIdA.Should().Be("A1");
        byNode[1].EspnTeamIdB.Should().Be("M4");
        byNode[3].EspnTeamIdA.Should().Be("M1");
        byNode[3].EspnTeamIdB.Should().Be("A4");
        byNode[5].EspnTeamIdA.Should().Be("C1");
        byNode[5].EspnTeamIdB.Should().Be("P4");
        byNode[7].EspnTeamIdA.Should().Be("P1");
        byNode[7].EspnTeamIdB.Should().Be("C4");
    }

    [Fact]
    public void Build_IncompleteConference_ReturnsEmptyList()
    {
        // Arrange (solo Este completo, falta Oeste)
        const string json = """
            {
              "children": [
                {
                  "name": "Eastern Conference",
                  "children": [
                    {
                      "name": "Atlantic Division",
                      "standings": {
                        "entries": [
                          { "team": { "id": "A1" }, "stats": [ { "name": "playoffSeed", "value": 1 }, { "name": "points", "value": 110 } ] },
                          { "team": { "id": "A2" }, "stats": [ { "name": "playoffSeed", "value": 3 }, { "name": "points", "value": 100 } ] },
                          { "team": { "id": "A3" }, "stats": [ { "name": "playoffSeed", "value": 5 }, { "name": "points", "value": 95 } ] },
                          { "team": { "id": "A4" }, "stats": [ { "name": "playoffSeed", "value": 7 }, { "name": "points", "value": 90 } ] }
                        ]
                      }
                    },
                    {
                      "name": "Metropolitan Division",
                      "standings": {
                        "entries": [
                          { "team": { "id": "M1" }, "stats": [ { "name": "playoffSeed", "value": 2 }, { "name": "points", "value": 108 } ] },
                          { "team": { "id": "M2" }, "stats": [ { "name": "playoffSeed", "value": 4 }, { "name": "points", "value": 98 } ] },
                          { "team": { "id": "M3" }, "stats": [ { "name": "playoffSeed", "value": 6 }, { "name": "points", "value": 93 } ] },
                          { "team": { "id": "M4" }, "stats": [ { "name": "playoffSeed", "value": 8 }, { "name": "points", "value": 88 } ] }
                        ]
                      }
                    }
                  ]
                }
              ]
            }
            """;

        // Act
        var ties = _builder.Build(json);

        // Assert
        ties.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{}")]
    [InlineData("{no")]
    public void Build_EmptyOrInvalidJson_ReturnsEmptyWithoutThrowing(string json)
    {
        // Arrange & Act
        var act = () => _builder.Build(json);

        // Assert
        act.Should().NotThrow();
        act().Should().BeEmpty();
    }
}
