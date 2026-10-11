using Domain.Entities;
using FluentAssertions;
using UseCases.Bulletin;
using Xunit;

namespace PickSportsApi.UnitTests.UseCasesTests;

public class StreakCalculatorServiceTests
{
    private readonly StreakCalculatorService _sut = new();

    [Fact]
    public void CalculateNotableStreaks_WhenTeamWith5WinsLoses_ShouldDetectBrokenStreak()
    {
        // Arrange
        var teamCruzAzul = new Team { Id = 10, Name = "Cruz Azul", Abbreviation = "CAZ" };
        var teamMazatlan = new Team { Id = 20, Name = "Mazatlán", Abbreviation = "MAZ" };

        var seasonMatches = new List<Match>
        {
            // 5 victorias previas de Cruz Azul
            new() { Id = 1, HomeTeamId = 10, AwayTeamId = 99, HomeScore = 2, AwayScore = 0, StatusState = "post", DateUtc = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc), WinnerAbbr = "CAZ" },
            new() { Id = 2, HomeTeamId = 98, AwayTeamId = 10, HomeScore = 1, AwayScore = 3, StatusState = "post", DateUtc = new DateTime(2026, 8, 8, 0, 0, 0, DateTimeKind.Utc), WinnerAbbr = "CAZ" },
            new() { Id = 3, HomeTeamId = 10, AwayTeamId = 97, HomeScore = 1, AwayScore = 0, StatusState = "post", DateUtc = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc), WinnerAbbr = "CAZ" },
            new() { Id = 4, HomeTeamId = 96, AwayTeamId = 10, HomeScore = 0, AwayScore = 2, StatusState = "post", DateUtc = new DateTime(2026, 8, 22, 0, 0, 0, DateTimeKind.Utc), WinnerAbbr = "CAZ" },
            new() { Id = 5, HomeTeamId = 10, AwayTeamId = 95, HomeScore = 4, AwayScore = 1, StatusState = "post", DateUtc = new DateTime(2026, 8, 29, 0, 0, 0, DateTimeKind.Utc), WinnerAbbr = "CAZ" },
            // Partido actual: Cruz Azul pierde contra Mazatlán
            new() { Id = 6, HomeTeamId = 10, HomeTeam = teamCruzAzul, AwayTeamId = 20, AwayTeam = teamMazatlan, HomeScore = 0, AwayScore = 1, StatusState = "post", DateUtc = new DateTime(2026, 9, 5, 0, 0, 0, DateTimeKind.Utc), WinnerAbbr = "MAZ" }
        };

        var currentWeekMatches = new List<Match> { seasonMatches[5] };

        // Act
        var streaks = _sut.CalculateNotableStreaks(seasonMatches, currentWeekMatches);

        // Assert
        streaks.Should().ContainSingle(s => s.TeamAbbr == "CAZ" && s.StreakType == "WIN_STREAK");
        var streak = streaks.First(s => s.TeamAbbr == "CAZ");
        streak.StreakCount.Should().Be(5);
        streak.IsBroken.Should().BeTrue();
        streak.Description.Should().Contain("ROTA");
    }

    [Fact]
    public void CalculateNotableStreaks_WhenTeamWith3WinsWinsAgain_ShouldDetectExtendedStreak()
    {
        // Arrange
        var teamToluca = new Team { Id = 30, Name = "Toluca", Abbreviation = "TOL" };
        var teamAtlas = new Team { Id = 40, Name = "Atlas", Abbreviation = "ATL" };

        var seasonMatches = new List<Match>
        {
            new() { Id = 1, HomeTeamId = 30, AwayTeamId = 99, HomeScore = 2, AwayScore = 1, StatusState = "post", DateUtc = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc), WinnerAbbr = "TOL" },
            new() { Id = 2, HomeTeamId = 98, AwayTeamId = 30, HomeScore = 0, AwayScore = 1, StatusState = "post", DateUtc = new DateTime(2026, 8, 8, 0, 0, 0, DateTimeKind.Utc), WinnerAbbr = "TOL" },
            new() { Id = 3, HomeTeamId = 30, AwayTeamId = 97, HomeScore = 3, AwayScore = 0, StatusState = "post", DateUtc = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc), WinnerAbbr = "TOL" },
            // Partido actual: Toluca vuelve a ganar
            new() { Id = 4, HomeTeamId = 30, HomeTeam = teamToluca, AwayTeamId = 40, AwayTeam = teamAtlas, HomeScore = 2, AwayScore = 0, StatusState = "post", DateUtc = new DateTime(2026, 8, 22, 0, 0, 0, DateTimeKind.Utc), WinnerAbbr = "TOL" }
        };

        var currentWeekMatches = new List<Match> { seasonMatches[3] };

        // Act
        var streaks = _sut.CalculateNotableStreaks(seasonMatches, currentWeekMatches);

        // Assert
        streaks.Should().ContainSingle(s => s.TeamAbbr == "TOL" && s.StreakType == "WIN_STREAK");
        var streak = streaks.First(s => s.TeamAbbr == "TOL");
        streak.StreakCount.Should().Be(4);
        streak.IsBroken.Should().BeFalse();
        streak.Description.Should().Contain("extendió su racha");
    }
}
