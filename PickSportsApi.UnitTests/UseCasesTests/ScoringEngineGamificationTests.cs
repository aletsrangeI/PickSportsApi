using Domain.Entities;
using FluentAssertions;
using UseCases.Scoring;

namespace PickSportsApi.UnitTests.UseCasesTests;

public class ScoringEngineGamificationTests
{
    private readonly ScoringEngine _engine = new();
    private static readonly DateTime Start = new(2026, 11, 1, 18, 0, 0, DateTimeKind.Utc);

    private static List<Match> Matches(int count) =>
        Enumerable.Range(1, count)
            .Select(i => new Match { Id = i, StatusState = "post", WinnerAbbr = "X", DateUtc = Start.AddHours(i) })
            .ToList();

    private static List<Pick> PicksFor(int memberId, params bool[] results) =>
        results.Select((hit, i) => new Pick { MemberId = memberId, MatchId = i + 1, IsHit = hit }).ToList();

    private string? HeatOf(List<Pick> picks, bool includeHeat, int matchCount)
    {
        var members = new List<QuinielaMember> { new() { Id = 1, Alias = "Alex" } };
        var result = _engine.CalculateWeeklyStandings(members, picks, Matches(matchCount), includeHeat);
        return result.Single().Heat;
    }

    [Fact]
    public void Heat_FourFinalHits_ReturnsFire()
    {
        // Arrange
        var picks = PicksFor(1, false, true, true, true, true);

        // Act
        var heat = HeatOf(picks, true, 5);

        // Assert
        heat.Should().Be("FIRE");
    }

    [Fact]
    public void Heat_FourFinalMisses_ReturnsIce()
    {
        // Arrange
        var picks = PicksFor(1, true, false, false, false, false);

        // Act
        var heat = HeatOf(picks, true, 5);

        // Assert
        heat.Should().Be("ICE");
    }

    [Fact]
    public void Heat_OnlyThreeConsecutiveHits_ReturnsNull()
    {
        // Arrange
        var picks = PicksFor(1, false, true, true, true);

        // Act
        var heat = HeatOf(picks, true, 4);

        // Assert
        heat.Should().BeNull();
    }

    [Fact]
    public void Heat_StreakOfFourBrokenByFinalMiss_ReturnsNull()
    {
        // Arrange
        var picks = PicksFor(1, true, true, true, true, false);

        // Act
        var heat = HeatOf(picks, true, 5);

        // Assert
        heat.Should().BeNull();
    }

    [Fact]
    public void Heat_IncludeHeatFalse_ReturnsNullEvenWithStreak()
    {
        // Arrange
        var picks = PicksFor(1, true, true, true, true, true);

        // Act
        var heat = HeatOf(picks, false, 5);

        // Assert
        heat.Should().BeNull();
    }

    [Fact]
    public void Heat_OrdersByMatchDateNotByMatchId()
    {
        // Arrange: por fecha, el partido 1 es el ultimo (fallo) => la racha de aciertos se rompe
        var matches = Matches(5);
        matches[0].DateUtc = Start.AddDays(1);
        var picks = PicksFor(1, false, true, true, true, true);
        var members = new List<QuinielaMember> { new() { Id = 1, Alias = "Alex" } };

        // Act
        var result = _engine.CalculateWeeklyStandings(members, picks, matches, true);

        // Assert
        result.Single().Heat.Should().BeNull();
    }

    [Fact]
    public void Heat_IncludeHeatDoesNotAlterRankingNorHits()
    {
        // Arrange
        var members = new List<QuinielaMember>
        {
            new() { Id = 1, Alias = "Alex" },
            new() { Id = 2, Alias = "Fer" }
        };
        var picks = PicksFor(1, true, true, true, true, true).Concat(PicksFor(2, true, false, false, false, false)).ToList();

        // Act
        var without = _engine.CalculateWeeklyStandings(members, picks, Matches(5), false);
        var with = _engine.CalculateWeeklyStandings(members, picks, Matches(5), true);

        // Assert
        with.Select(s => (s.MemberId, s.Rank, s.Hits)).Should().Equal(without.Select(s => (s.MemberId, s.Rank, s.Hits)));
        with.First(s => s.MemberId == 1).Heat.Should().Be("FIRE");
        with.First(s => s.MemberId == 2).Heat.Should().Be("ICE");
    }
}
