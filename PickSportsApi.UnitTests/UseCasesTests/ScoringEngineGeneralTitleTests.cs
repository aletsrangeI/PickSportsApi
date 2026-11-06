using Domain.Entities;
using Domain.Gamification;
using FluentAssertions;
using UseCases.Scoring;

namespace PickSportsApi.UnitTests.UseCasesTests;

public class ScoringEngineGeneralTitleTests
{
    private readonly ScoringEngine _engine = new();
    private static readonly DateTime Start = new(2026, 11, 1, 18, 0, 0, DateTimeKind.Utc);

    private static List<QuinielaMember> MemberWithTitle(string? titleCode) => new()
    {
        new QuinielaMember
        {
            Id = 1, UserId = 7, Alias = "Alex",
            User = new User { Id = 7, DisplayName = "Alejandro", EquippedTitleCode = titleCode }
        }
    };

    private static List<Pick> GeneralPicks()
    {
        var week = new Week { Id = 1, SeasonId = 1, WeekNumber = 1 };
        var match = new Match { Id = 1, StatusState = "post", WinnerAbbr = "X", DateUtc = Start, Week = week };
        return new() { new Pick { MemberId = 1, MatchId = 1, IsHit = true, Match = match } };
    }

    [Fact]
    public void General_ConIncludeTitle_MuestraElNombreDelTitulo()
    {
        // Arrange
        var members = MemberWithTitle(AchievementCatalog.SoloYoLoVi);

        // Act
        var row = _engine.CalculateGeneralStandings(members, GeneralPicks(), 1, 1, includeTitle: true).Single();

        // Assert
        row.EquippedTitle.Should().Be("Ojo de Halcón");
    }

    [Fact]
    public void General_SinIncludeTitle_EsNulo()
    {
        // Arrange
        var members = MemberWithTitle(AchievementCatalog.SoloYoLoVi);

        // Act
        var row = _engine.CalculateGeneralStandings(members, GeneralPicks(), 1, 1, includeTitle: false).Single();

        // Assert
        row.EquippedTitle.Should().BeNull();
    }

    [Fact]
    public void General_CodigoDesconocido_EsNuloSinLanzar()
    {
        // Arrange
        var members = MemberWithTitle("CODIGO_QUE_NO_EXISTE");

        // Act
        var act = () => _engine.CalculateGeneralStandings(members, GeneralPicks(), 1, 1, includeTitle: true);

        // Assert
        act.Should().NotThrow();
        act().Single().EquippedTitle.Should().BeNull();
    }

    [Fact]
    public void Semanal_ConIncludeTitleSinHeat_MuestraTituloYNoHeat()
    {
        // Arrange
        var members = MemberWithTitle(AchievementCatalog.SoloYoLoVi);
        var matches = new List<Match> { new() { Id = 1, StatusState = "post", WinnerAbbr = "X", DateUtc = Start } };
        var picks = new List<Pick> { new() { MemberId = 1, MatchId = 1, IsHit = true } };

        // Act
        var row = _engine.CalculateWeeklyStandings(members, picks, matches, includeHeat: false, includeTitle: true).Single();

        // Assert
        row.EquippedTitle.Should().Be("Ojo de Halcón");
        row.Heat.Should().BeNull();
    }
}
