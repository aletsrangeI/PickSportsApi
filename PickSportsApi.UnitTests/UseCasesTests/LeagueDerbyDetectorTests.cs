using Domain.Entities;
using FluentAssertions;
using UseCases.Bulletin;
using Xunit;

namespace PickSportsApi.UnitTests.UseCasesTests;

public class LeagueDerbyDetectorTests
{
    private readonly LeagueDerbyDetector _sut = new();

    [Fact]
    public void DetectDerbies_WhenClasicoNacionalPlayed_ShouldDetectCorrectly()
    {
        // Arrange (AME vs GDL)
        var matches = new List<Match>
        {
            new()
            {
                Id = 1,
                HomeTeam = new Team { Name = "América", Abbreviation = "AME" },
                AwayTeam = new Team { Name = "Guadalajara", Abbreviation = "GDL" },
                HomeScore = 2,
                AwayScore = 1,
                StatusState = "post",
                WinnerAbbr = "AME"
            }
        };

        // Act
        var derbies = _sut.DetectDerbies(matches);

        // Assert
        derbies.Should().HaveCount(1);
        var derby = derbies[0];
        derby.DerbyName.Should().Be("El Clásico Nacional");
        derby.WinnerAbbr.Should().Be("AME");
        derby.ScoreLabel.Should().Be("América 2 - 1 Guadalajara");
    }

    [Fact]
    public void DetectDerbies_WhenClasicoRegioInvertedOrder_ShouldDetectCorrectly()
    {
        // Arrange (TIG vs MTY)
        var matches = new List<Match>
        {
            new()
            {
                Id = 2,
                HomeTeam = new Team { Name = "Tigres", Abbreviation = "UANL" },
                AwayTeam = new Team { Name = "Monterrey", Abbreviation = "MTY" },
                HomeScore = 1,
                AwayScore = 1,
                StatusState = "post",
                WinnerAbbr = "EMPATE"
            }
        };

        // Act
        var derbies = _sut.DetectDerbies(matches);

        // Assert
        derbies.Should().HaveCount(1);
        derbies[0].DerbyName.Should().Be("El Clásico Regio");
        derbies[0].WinnerAbbr.Should().Be("EMPATE");
    }

    [Fact]
    public void DetectDerbies_WhenNoDerbiesInWeek_ShouldReturnEmpty()
    {
        // Arrange
        var matches = new List<Match>
        {
            new()
            {
                Id = 3,
                HomeTeam = new Team { Name = "Mazatlán", Abbreviation = "MAZ" },
                AwayTeam = new Team { Name = "Puebla", Abbreviation = "PUE" },
                HomeScore = 0,
                AwayScore = 0,
                StatusState = "post"
            }
        };

        // Act
        var derbies = _sut.DetectDerbies(matches);

        // Assert
        derbies.Should().BeEmpty();
    }
}
