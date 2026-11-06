using Domain.Entities;
using Domain.Gamification;
using FluentAssertions;
using UseCases.Scoring;

namespace PickSportsApi.UnitTests.UseCasesTests;

public class ScoringEngineEquippedTitleTests
{
    private readonly ScoringEngine _engine = new();
    private static readonly DateTime Start = new(2026, 11, 1, 18, 0, 0, DateTimeKind.Utc);

    private static List<Match> OneMatch() =>
        new() { new Match { Id = 1, StatusState = "post", WinnerAbbr = "X", DateUtc = Start } };

    private static List<Pick> OnePick() =>
        new() { new Pick { MemberId = 1, MatchId = 1, IsHit = true } };

    private static List<QuinielaMember> MemberWithTitle(string? titleCode) => new()
    {
        new QuinielaMember
        {
            Id = 1, UserId = 7, Alias = "Alex",
            User = new User { Id = 7, DisplayName = "Alejandro", EquippedTitleCode = titleCode }
        }
    };

    [Fact]
    public void EquippedTitle_ConGamificacionYTituloEquipado_MuestraElNombreDelTitulo()
    {
        // Arrange
        var members = MemberWithTitle(AchievementCatalog.SoloYoLoVi);

        // Act
        var standing = _engine.CalculateWeeklyStandings(members, OnePick(), OneMatch(), includeHeat: true).Single();

        // Assert
        standing.EquippedTitle.Should().Be("Ojo de Halcón");
    }

    [Fact]
    public void EquippedTitle_SinGamificacion_NoSeExpone()
    {
        // Arrange
        var members = MemberWithTitle(AchievementCatalog.SoloYoLoVi);

        // Act
        var standing = _engine.CalculateWeeklyStandings(members, OnePick(), OneMatch(), includeHeat: false).Single();

        // Assert
        standing.EquippedTitle.Should().BeNull();
    }

    [Fact]
    public void EquippedTitle_SinTituloEquipado_EsNulo()
    {
        // Arrange
        var members = MemberWithTitle(null);

        // Act
        var standing = _engine.CalculateWeeklyStandings(members, OnePick(), OneMatch(), includeHeat: true).Single();

        // Assert
        standing.EquippedTitle.Should().BeNull();
    }

    [Fact]
    public void EquippedTitle_CodigoDesconocido_EsNuloSinLanzar()
    {
        // Arrange
        var members = MemberWithTitle("CODIGO_QUE_NO_EXISTE");

        // Act
        var standing = _engine.CalculateWeeklyStandings(members, OnePick(), OneMatch(), includeHeat: true).Single();

        // Assert
        standing.EquippedTitle.Should().BeNull();
    }
}
