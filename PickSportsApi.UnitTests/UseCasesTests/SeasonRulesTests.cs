using Domain.Entities;
using UseCases.Scoring;

namespace PickSportsApi.UnitTests.UseCasesTests;

/// <summary>
/// Reglas de temporada y fase (SPEC-020): Liguilla fuera de la quiniela regular y temporada en juego de una quiniela.
/// </summary>
public class SeasonRulesTests
{
    [Theory]
    [InlineData(17, 1, false)]
    [InlineData(17, 17, false)]  // Última jornada regular de Liga MX
    [InlineData(17, 18, true)]   // Primera jornada de Liguilla
    [InlineData(0, 25, false)]   // Liga sin jornadas regulares configuradas: nunca se considera Liguilla
    public void League_IsPlayoffWeek_DistingueTemporadaRegularDeLiguilla(int weeksCount, int weekNumber, bool expected)
    {
        // Arrange
        var league = new League { WeeksCount = weeksCount };

        // Act
        var isPlayoff = league.IsPlayoffWeek(weekNumber);

        // Assert
        Assert.Equal(expected, isPlayoff);
    }

    private static Pick PickOn(int seasonId, int weekNumber, DateTime dateUtc) =>
        new() { Match = new Match { DateUtc = dateUtc, Week = new Week { SeasonId = seasonId, WeekNumber = weekNumber } } };

    [Fact]
    public void QuinielaSeasonResolver_TomaLaTemporadaDelPartidoMasReciente()
    {
        // Arrange: picks del Clausura (temporada 1) y del Apertura (temporada 3)
        var picks = new List<Pick>
        {
            PickOn(3, 11, new DateTime(2026, 10, 3)),
            PickOn(1, 10, new DateTime(2026, 3, 15))
        };

        // Act
        var seasonId = QuinielaSeasonResolver.ResolveActiveSeasonId(picks);

        // Assert
        Assert.Equal(3, seasonId);
        Assert.True(QuinielaSeasonResolver.AppliesToWeek(picks, new Week { SeasonId = 3 }));
        Assert.False(QuinielaSeasonResolver.AppliesToWeek(picks, new Week { SeasonId = 1 }));
    }

    [Fact]
    public void QuinielaSeasonResolver_QuinielaSinPicks_AplicaACualquierJornada()
    {
        // Act
        var seasonId = QuinielaSeasonResolver.ResolveActiveSeasonId(new List<Pick>());

        // Assert
        Assert.Null(seasonId);
        Assert.True(QuinielaSeasonResolver.AppliesToWeek(new List<Pick>(), new Week { SeasonId = 7 }));
    }
}
