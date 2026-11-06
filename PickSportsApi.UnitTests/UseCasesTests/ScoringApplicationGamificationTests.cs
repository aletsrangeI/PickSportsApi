using Domain.Entities;
using FluentAssertions;
using Interface.Persistence;
using Interface.UseCases;
using Moq;
using UseCases.Scoring;
using M = Domain.Entities.Match;

namespace PickSportsApi.UnitTests.UseCasesTests;

public class ScoringApplicationGamificationTests
{
    private const int QuinielaId = 10, LeagueId = 5, AdminUserId = 99;

    private static Mock<IUnitOfWork> BuildUnitOfWork(bool gamificationEnabled, int matchCount = 1, int weeksCount = 17)
    {
        var member = new QuinielaMember { Id = 1, UserId = 1, Alias = "Alex" };
        var week = new Week { Id = 3, SeasonId = 2, WeekNumber = 10, Name = "Jornada 10", Status = "LOCKED" };
        var matches = Enumerable.Range(1, matchCount).Select(i => new M
        {
            Id = i, WeekId = week.Id, Week = week, StatusState = "post", WinnerAbbr = "A",
            HomeScore = 1, AwayScore = 0, DateUtc = new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc).AddHours(i)
        }).ToList();
        var picks = matches.Select(m => new Pick { MemberId = 1, MatchId = m.Id, Match = m, PickAbbr = "A" }).ToList();

        var unitOfWork = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
        unitOfWork.Setup(u => u.Quinielas.GetAsync(QuinielaId)).ReturnsAsync(new Quiniela { Id = QuinielaId, LeagueId = LeagueId, GamificationEnabled = gamificationEnabled });
        unitOfWork.Setup(u => u.QuinielaMembers.GetMembershipAsync(QuinielaId, AdminUserId)).ReturnsAsync(new QuinielaMember { Id = 50, Role = "ADMIN" });
        unitOfWork.Setup(u => u.QuinielaMembers.GetMembersAsync(QuinielaId)).ReturnsAsync(new List<QuinielaMember> { member });
        unitOfWork.Setup(u => u.Weeks.GetAsync(week.Id)).ReturnsAsync(week);
        unitOfWork.Setup(u => u.Leagues.GetAsync(LeagueId)).ReturnsAsync(new League { Id = LeagueId, WeeksCount = weeksCount });
        unitOfWork.Setup(u => u.Matches.GetByWeekIdAsync(week.Id)).ReturnsAsync(matches);
        unitOfWork.Setup(u => u.Picks.GetAllPicksForWeekAsync(QuinielaId, week.Id)).ReturnsAsync(picks);
        unitOfWork.Setup(u => u.Picks.GetAllPicksForQuinielaAsync(QuinielaId)).ReturnsAsync(picks);
        return unitOfWork;
    }

    [Fact]
    public async Task ScoreWeekAsync_ConGamificacionInyectada_LaLlamaComoPaso9AntesDelSave()
    {
        // Arrange
        var unitOfWork = BuildUnitOfWork(gamificationEnabled: true);
        var calls = new List<string>();
        var gamification = new Mock<IGamificationApplication>();
        gamification.Setup(g => g.ResolveWeekAsync(QuinielaId, 3)).Callback(() => calls.Add("gamification")).Returns(Task.CompletedTask);
        unitOfWork.Setup(u => u.Save(It.IsAny<CancellationToken>())).Callback(() => calls.Add("save")).ReturnsAsync(1);
        var sut = new ScoringApplication(unitOfWork.Object, new ScoringEngine(), null, gamification.Object);

        // Act
        var result = await sut.ScoreWeekAsync(QuinielaId, 3, AdminUserId);

        // Assert
        result.isSuccess.Should().BeTrue();
        gamification.Verify(g => g.ResolveWeekAsync(QuinielaId, 3), Times.Once);
        calls.Should().Equal("gamification", "save");
    }

    [Fact]
    public async Task ScoreWeekAsync_SinGamificacionInyectada_CalificaYGuardaSinErrores()
    {
        // Arrange: la dependencia es opcional (null); el paso 9 no debe lanzar NullReferenceException
        var unitOfWork = BuildUnitOfWork(gamificationEnabled: true);
        var sut = new ScoringApplication(unitOfWork.Object, new ScoringEngine());

        // Act
        var result = await sut.ScoreWeekAsync(QuinielaId, 3, AdminUserId);

        // Assert
        result.isSuccess.Should().BeTrue();
        unitOfWork.Verify(u => u.Save(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ScoreWeekAsync_JornadaDeLiguilla_NoLlegaAlPasoDeGamificacion()
    {
        // Arrange: la jornada 10 es de Liguilla (la liga tiene 9 regulares): se rechaza antes de calificar
        var unitOfWork = BuildUnitOfWork(gamificationEnabled: true, weeksCount: 9);
        var gamification = new Mock<IGamificationApplication>();
        var sut = new ScoringApplication(unitOfWork.Object, new ScoringEngine(), null, gamification.Object);

        // Act
        var result = await sut.ScoreWeekAsync(QuinielaId, 3, AdminUserId);

        // Assert
        result.isSuccess.Should().BeFalse();
        gamification.Verify(g => g.ResolveWeekAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task ScoreWeekAsync_ScoringNoOcultaLasExcepcionesDeLaGamificacion_YNoGuarda()
    {
        // Arrange: ScoringApplication NO aisla fallos de su colaborador; ese aislamiento vive en GamificationApplication
        // (ver GamificationApplicationTests). Aqui solo se documenta que, si el colaborador lanzara, no se guarda a medias.
        var unitOfWork = BuildUnitOfWork(gamificationEnabled: true);
        var gamification = new Mock<IGamificationApplication>();
        gamification.Setup(g => g.ResolveWeekAsync(It.IsAny<int>(), It.IsAny<int>())).ThrowsAsync(new InvalidOperationException("boom"));
        var sut = new ScoringApplication(unitOfWork.Object, new ScoringEngine(), null, gamification.Object);

        // Act
        var act = () => sut.ScoreWeekAsync(QuinielaId, 3, AdminUserId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        unitOfWork.Verify(u => u.Save(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetStandingsAsync_EnJornadaDeLiguilla_NoCalculaLaRacha()
    {
        // Arrange: gamificacion activa y 4 aciertos seguidos, pero la jornada 10 es de Liguilla (9 regulares)
        var unitOfWork = BuildUnitOfWork(true, matchCount: 4, weeksCount: 9);
        var sut = new ScoringApplication(unitOfWork.Object, new ScoringEngine());

        // Act
        var result = await sut.GetStandingsAsync(QuinielaId, 3);

        // Assert
        result.Data!.WeeklyStandings.Single().Heat.Should().BeNull();
    }

    [Theory]
    [InlineData(true, "FIRE")]
    [InlineData(false, null)]
    public async Task GetStandingsAsync_LaRachaVisualSoloSeCalculaConGamificacionActiva(bool enabled, string? expectedHeat)
    {
        // Arrange: 4 aciertos seguidos al final de la jornada
        var unitOfWork = BuildUnitOfWork(enabled, matchCount: 4);
        var sut = new ScoringApplication(unitOfWork.Object, new ScoringEngine());

        // Act
        var result = await sut.GetStandingsAsync(QuinielaId, 3);

        // Assert
        result.isSuccess.Should().BeTrue();
        result.Data!.WeeklyStandings.Single().Heat.Should().Be(expectedHeat);
    }
}
