using Domain.Common;
using FluentAssertions;
using Interface.UseCases.Knockout;
using Moq;
using UseCases.Liguilla.Simulation;

namespace PickSportsApi.UnitTests.UseCasesTests.Knockout;

public class FmfSimulationBuilderTests
{
    private const string QuarterFinalsRoundCode = "QF";
    private const string ValidStandingsJson = "{\"standings\":{}}";

    private readonly Mock<IEspnKnockoutParser> _parserMock = new();
    private readonly FmfSimulationBuilder _builder;

    public FmfSimulationBuilderTests()
    {
        _builder = new FmfSimulationBuilder(_parserMock.Object);
    }

    [Fact]
    public void FormatCode_IsFmfLiguilla()
    {
        // Arrange

        // Act
        var formatCode = _builder.FormatCode;

        // Assert
        formatCode.Should().Be(KnockoutFormatCodes.FmfLiguilla);
    }

    [Fact]
    public void Build_WithEightSeeds_ReturnsFourQuarterFinalTiesOneVsEightThroughFourVsFive()
    {
        // Arrange
        var seeds = new Dictionary<string, int>
        {
            ["t1"] = 1,
            ["t2"] = 2,
            ["t3"] = 3,
            ["t4"] = 4,
            ["t5"] = 5,
            ["t6"] = 6,
            ["t7"] = 7,
            ["t8"] = 8
        };
        _parserMock
            .Setup(p => p.ParseFmfPlayoffSeeds(It.IsAny<string>()))
            .Returns(seeds);

        var expected = new[]
        {
            new SimulatedTie(1, QuarterFinalsRoundCode, null, "t1", 1, "t8", 8),
            new SimulatedTie(2, QuarterFinalsRoundCode, null, "t2", 2, "t7", 7),
            new SimulatedTie(3, QuarterFinalsRoundCode, null, "t3", 3, "t6", 6),
            new SimulatedTie(4, QuarterFinalsRoundCode, null, "t4", 4, "t5", 5)
        };

        // Act
        var ties = _builder.Build(ValidStandingsJson);

        // Assert
        ties.Should().Equal(expected);
    }

    [Fact]
    public void Build_WithSeedsReturnedUnordered_StillPairsBySeedPosition()
    {
        // Arrange
        var seeds = new Dictionary<string, int>
        {
            ["t8"] = 8,
            ["t3"] = 3,
            ["t1"] = 1,
            ["t6"] = 6,
            ["t2"] = 2,
            ["t7"] = 7,
            ["t5"] = 5,
            ["t4"] = 4
        };
        _parserMock
            .Setup(p => p.ParseFmfPlayoffSeeds(It.IsAny<string>()))
            .Returns(seeds);

        var expected = new[]
        {
            new SimulatedTie(1, QuarterFinalsRoundCode, null, "t1", 1, "t8", 8),
            new SimulatedTie(2, QuarterFinalsRoundCode, null, "t2", 2, "t7", 7),
            new SimulatedTie(3, QuarterFinalsRoundCode, null, "t3", 3, "t6", 6),
            new SimulatedTie(4, QuarterFinalsRoundCode, null, "t4", 4, "t5", 5)
        };

        // Act
        var ties = _builder.Build(ValidStandingsJson);

        // Assert
        ties.Should().Equal(expected);
    }

    [Fact]
    public void Build_WithFewerThanEightSeeds_ReturnsEmpty()
    {
        // Arrange
        var seeds = new Dictionary<string, int>
        {
            ["t1"] = 1,
            ["t2"] = 2,
            ["t3"] = 3,
            ["t4"] = 4,
            ["t5"] = 5,
            ["t6"] = 6,
            ["t7"] = 7
        };
        _parserMock
            .Setup(p => p.ParseFmfPlayoffSeeds(It.IsAny<string>()))
            .Returns(seeds);

        // Act
        var ties = _builder.Build(ValidStandingsJson);

        // Assert
        ties.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Build_WithEmptyOrWhitespaceJson_ReturnsEmptyWithoutCallingParser(string json)
    {
        // Arrange

        // Act
        var ties = _builder.Build(json);

        // Assert
        ties.Should().BeEmpty();
        _parserMock.Verify(p => p.ParseFmfPlayoffSeeds(It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [MemberData(nameof(InvalidSeedMaps))]
    public void Build_WithDuplicatedOrOutOfRangeSeeds_ReturnsEmpty(IReadOnlyDictionary<string, int> seeds)
    {
        // Arrange
        _parserMock
            .Setup(p => p.ParseFmfPlayoffSeeds(It.IsAny<string>()))
            .Returns(seeds);

        // Act
        var ties = _builder.Build(ValidStandingsJson);

        // Assert
        ties.Should().BeEmpty();
    }

    public static IEnumerable<object[]> InvalidSeedMaps()
    {
        yield return new object[]
        {
            new Dictionary<string, int>
            {
                ["t1"] = 1,
                ["t2"] = 2,
                ["t3"] = 3,
                ["t4"] = 4,
                ["t5"] = 5,
                ["t6"] = 6,
                ["t7"] = 7,
                ["t9"] = 9
            }
        };

        yield return new object[]
        {
            new Dictionary<string, int>
            {
                ["t1"] = 1,
                ["t2"] = 2,
                ["t3"] = 3,
                ["t4"] = 4,
                ["t5"] = 5,
                ["t6"] = 6,
                ["t7"] = 7,
                ["t8"] = 1
            }
        };
    }

    [Fact]
    public void StandingsPath_PointsToLigaMxStandings()
    {
        // Arrange

        // Act
        var path = _builder.StandingsPath("soccer/mex.1");

        // Assert
        path.Should().Contain("soccer/mex.1/standings");
    }

    [Fact]
    public void Constructor_NullParser_Throws()
    {
        // Arrange

        // Act
        var act = () => new FmfSimulationBuilder(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("parser");
    }
}
