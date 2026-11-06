using Domain.Entities;
using FluentAssertions;
using Interface.Persistence;
using Interface.UseCases;
using Moq;
using UseCases.Gamification;

namespace PickSportsApi.UnitTests.UseCasesTests;

public class GamificationApplicationTests
{
    private const int QuinielaId = 10, WeekId = 3, LeagueId = 5;

    private static (GamificationApplication Sut, Mock<IAchievementApplication> Achievements) Build(
        Quiniela? quiniela, Week? week, int weeksCount = 17)
    {
        var unitOfWork = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
        unitOfWork.Setup(u => u.Quinielas.GetAsync(QuinielaId)).ReturnsAsync(quiniela!);
        unitOfWork.Setup(u => u.Weeks.GetAsync(WeekId)).ReturnsAsync(week!);
        unitOfWork.Setup(u => u.Leagues.GetAsync(LeagueId)).ReturnsAsync(new League { Id = LeagueId, WeeksCount = weeksCount });
        var achievements = new Mock<IAchievementApplication>();
        return (new GamificationApplication(unitOfWork.Object, achievements.Object), achievements);
    }

    [Fact]
    public async Task ResolveWeek_ConGamificacionActivaYJornadaRegular_EvaluaTrofeos()
    {
        var (sut, achievements) = Build(
            new Quiniela { Id = QuinielaId, LeagueId = LeagueId, GamificationEnabled = true },
            new Week { Id = WeekId, WeekNumber = 17 });

        await sut.ResolveWeekAsync(QuinielaId, WeekId);

        achievements.Verify(a => a.EvaluateWeekAsync(QuinielaId, WeekId), Times.Once);
    }

    [Theory]
    [InlineData(2, 1, false)]   // jornada de una temporada anterior: no otorga
    [InlineData(2, 2, true)]    // temporada vigente
    [InlineData(null, 1, true)] // quiniela sin temporada: comportamiento previo
    public async Task ResolveWeek_SoloEvaluaJornadasDeLaTemporadaDeLaQuiniela(int? quinielaSeason, int weekSeason, bool expected)
    {
        var (sut, achievements) = Build(
            new Quiniela { Id = QuinielaId, LeagueId = LeagueId, GamificationEnabled = true, SeasonId = quinielaSeason },
            new Week { Id = WeekId, WeekNumber = 5, SeasonId = weekSeason });

        await sut.ResolveWeekAsync(QuinielaId, WeekId);

        achievements.Verify(a => a.EvaluateWeekAsync(QuinielaId, WeekId), expected ? Times.Once() : Times.Never());
    }

    [Fact]
    public async Task ResolveWeek_SiLaEvaluacionLanzaExcepcion_NoPropagaYRegistraElError()
    {
        var unitOfWork = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
        unitOfWork.Setup(u => u.Quinielas.GetAsync(QuinielaId))
            .ReturnsAsync(new Quiniela { Id = QuinielaId, LeagueId = LeagueId, GamificationEnabled = true });
        unitOfWork.Setup(u => u.Weeks.GetAsync(WeekId)).ReturnsAsync(new Week { Id = WeekId, WeekNumber = 5 });
        unitOfWork.Setup(u => u.Leagues.GetAsync(LeagueId)).ReturnsAsync(new League { Id = LeagueId, WeeksCount = 17 });
        var achievements = new Mock<IAchievementApplication>();
        achievements.Setup(a => a.EvaluateWeekAsync(QuinielaId, WeekId)).ThrowsAsync(new InvalidOperationException("boom"));
        var logger = new Mock<Common.IAppLogger<GamificationApplication>>();
        var sut = new GamificationApplication(unitOfWork.Object, achievements.Object, logger.Object);

        var act = () => sut.ResolveWeekAsync(QuinielaId, WeekId);

        await act.Should().NotThrowAsync();
        logger.Verify(l => l.LogError(It.IsAny<string>(), It.IsAny<object[]>()), Times.Once);
    }

    [Fact]
    public async Task ResolveWeek_SiLaEvaluacionLanzaExcepcionYNoHayLogger_TampocoPropaga()
    {
        var unitOfWork = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
        unitOfWork.Setup(u => u.Quinielas.GetAsync(QuinielaId))
            .ReturnsAsync(new Quiniela { Id = QuinielaId, LeagueId = LeagueId, GamificationEnabled = true });
        unitOfWork.Setup(u => u.Weeks.GetAsync(WeekId)).ReturnsAsync(new Week { Id = WeekId, WeekNumber = 5 });
        unitOfWork.Setup(u => u.Leagues.GetAsync(LeagueId)).ReturnsAsync(new League { Id = LeagueId, WeeksCount = 17 });
        var achievements = new Mock<IAchievementApplication>();
        achievements.Setup(a => a.EvaluateWeekAsync(QuinielaId, WeekId)).ThrowsAsync(new InvalidOperationException("boom"));
        var sut = new GamificationApplication(unitOfWork.Object, achievements.Object);

        var act = () => sut.ResolveWeekAsync(QuinielaId, WeekId);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ResolveWeek_GamificacionApagada_NoHaceNada()
    {
        var (sut, achievements) = Build(
            new Quiniela { Id = QuinielaId, LeagueId = LeagueId, GamificationEnabled = false },
            new Week { Id = WeekId, WeekNumber = 5 });

        await sut.ResolveWeekAsync(QuinielaId, WeekId);

        achievements.Verify(a => a.EvaluateWeekAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task ResolveWeek_JornadaDeLiguilla_NoHaceNada()
    {
        var (sut, achievements) = Build(
            new Quiniela { Id = QuinielaId, LeagueId = LeagueId, GamificationEnabled = true },
            new Week { Id = WeekId, WeekNumber = 18 });

        await sut.ResolveWeekAsync(QuinielaId, WeekId);

        achievements.Verify(a => a.EvaluateWeekAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task ResolveWeek_QuinielaInexistente_NoLanzaNiHaceNada()
    {
        var (sut, achievements) = Build(null, new Week { Id = WeekId, WeekNumber = 5 });

        var act = () => sut.ResolveWeekAsync(QuinielaId, WeekId);

        await act();
        achievements.Verify(a => a.EvaluateWeekAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task ResolveWeek_JornadaInexistente_NoHaceNada()
    {
        var (sut, achievements) = Build(
            new Quiniela { Id = QuinielaId, LeagueId = LeagueId, GamificationEnabled = true }, null);

        await sut.ResolveWeekAsync(QuinielaId, WeekId);

        achievements.Verify(a => a.EvaluateWeekAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }
}
