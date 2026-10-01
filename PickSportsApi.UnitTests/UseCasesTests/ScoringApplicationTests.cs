using Domain.Entities;
using Interface.Persistence;
using Moq;
using UseCases.Scoring;

namespace PickSportsApi.UnitTests.UseCasesTests;

public class ScoringApplicationTests
{
    [Fact]
    public async Task GetStandingsAsync_CalculaPosicionAnteriorYDeltaRespectoALaJornadaAnterior()
    {
        // Arrange
        var memberA = new QuinielaMember { Id = 1, UserId = 1, Alias = "Alex" };
        var memberB = new QuinielaMember { Id = 2, UserId = 2, Alias = "Brenda" };
        var week = new Week { Id = 2, WeekNumber = 2, Name = "Jornada 2", Status = "SCORED" };
        var previousMatch = new Domain.Entities.Match { Id = 1, Week = new Week { WeekNumber = 1 } };
        var currentMatch = new Domain.Entities.Match { Id = 2, Week = week, StatusState = "post", WinnerAbbr = "A" };

        var historicalPicks = new List<Pick>
        {
            new() { MemberId = 1, MatchId = 1, Match = previousMatch, IsHit = true },
            new() { MemberId = 2, MatchId = 1, Match = previousMatch, IsHit = false },
            new() { MemberId = 1, MatchId = 2, Match = currentMatch, IsHit = false, PickAbbr = "B" },
            new() { MemberId = 2, MatchId = 2, Match = currentMatch, IsHit = true, PickAbbr = "A" }
        };

        var unitOfWork = CreateUnitOfWork(
            week,
            new[] { memberA, memberB },
            new[] { currentMatch },
            historicalPicks);
        var application = new ScoringApplication(unitOfWork.Object, new ScoringEngine());

        // Act
        var result = await application.GetStandingsAsync(10, 2);

        // Assert
        var alex = result.Data!.GeneralStandings.Single(s => s.MemberId == 1);
        var brenda = result.Data.GeneralStandings.Single(s => s.MemberId == 2);
        Assert.Equal(2, alex.Rank);
        Assert.Equal(1, alex.PreviousRank);
        Assert.Equal(-1, alex.RankDelta);
        Assert.Equal(1, brenda.Rank);
        Assert.Equal(2, brenda.PreviousRank);
        Assert.Equal(1, brenda.RankDelta);
    }

    [Fact]
    public async Task GetStandingsAsync_EnLaPrimeraJornadaNoAsignaCambioDePosicion()
    {
        // Arrange
        var week = new Week { Id = 1, WeekNumber = 1, Name = "Jornada 1", Status = "SCORED" };
        var match = new Domain.Entities.Match { Id = 1, Week = week, StatusState = "post", WinnerAbbr = "A" };
        var picks = new List<Pick>
        {
            new() { MemberId = 1, MatchId = 1, Match = match, IsHit = true, PickAbbr = "A" }
        };
        var unitOfWork = CreateUnitOfWork(
            week,
            new[] { new QuinielaMember { Id = 1, UserId = 1, Alias = "Alex" } },
            new[] { match },
            picks);
        var application = new ScoringApplication(unitOfWork.Object, new ScoringEngine());

        // Act
        var result = await application.GetStandingsAsync(10, 1);

        // Assert
        var standing = Assert.Single(result.Data!.GeneralStandings);
        Assert.Null(standing.PreviousRank);
        Assert.Null(standing.RankDelta);
    }

    [Theory]
    [InlineData(2, true)]   // Jornada de la temporada en juego (picks más recientes): actualiza acumulados
    [InlineData(1, false)]  // Jornada histórica (otra temporada): no toca acumulados (H-009)
    public async Task ScoreWeekAsync_SoloActualizaAcumuladosConJornadasDeLaTemporadaEnJuego(int weekSeasonId, bool shouldUpdateMembers)
    {
        // Arrange: la quiniela ya tiene picks de la Jornada 11 de la temporada 2 (la más reciente)
        const int quinielaId = 10, leagueId = 5, adminUserId = 99;
        var member = new QuinielaMember { Id = 1, UserId = 1, Alias = "Alex", TotalHits = 7 };
        var week = new Week { Id = 3, SeasonId = weekSeasonId, WeekNumber = 10, Name = "Jornada 10", Status = "LOCKED" };
        var match = new Domain.Entities.Match { Id = 1, WeekId = week.Id, Week = week, StatusState = "post", WinnerAbbr = "A", HomeScore = 1, AwayScore = 0, DateUtc = new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc) };
        var pick = new Pick { MemberId = 1, MatchId = 1, Match = match, PickAbbr = "A" };
        var week11 = new Week { Id = 4, SeasonId = 2, WeekNumber = 11 };
        var latestPick = new Pick
        {
            MemberId = 1, MatchId = 2, PickAbbr = "B",
            Match = new Domain.Entities.Match { Id = 2, WeekId = week11.Id, Week = week11, StatusState = "pre", DateUtc = new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc) }
        };

        var unitOfWork = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
        unitOfWork.Setup(u => u.Quinielas.GetAsync(quinielaId)).ReturnsAsync(new Quiniela { Id = quinielaId, LeagueId = leagueId });
        unitOfWork.Setup(u => u.QuinielaMembers.GetMembershipAsync(quinielaId, adminUserId)).ReturnsAsync(new QuinielaMember { Id = 50, Role = "ADMIN" });
        unitOfWork.Setup(u => u.QuinielaMembers.GetMembersAsync(quinielaId)).ReturnsAsync(new List<QuinielaMember> { member });
        unitOfWork.Setup(u => u.Weeks.GetAsync(week.Id)).ReturnsAsync(week);
        unitOfWork.Setup(u => u.Matches.GetByWeekIdAsync(week.Id)).ReturnsAsync(new List<Domain.Entities.Match> { match });
        unitOfWork.Setup(u => u.Picks.GetAllPicksForWeekAsync(quinielaId, week.Id)).ReturnsAsync(new List<Pick> { pick });
        unitOfWork.Setup(u => u.Picks.GetAllPicksForQuinielaAsync(quinielaId)).ReturnsAsync(new List<Pick> { pick, latestPick });
        var application = new ScoringApplication(unitOfWork.Object, new ScoringEngine());

        // Act
        var result = await application.ScoreWeekAsync(quinielaId, week.Id, adminUserId);

        // Assert
        Assert.True(result.isSuccess);
        unitOfWork.Verify(u => u.QuinielaMembers.UpdateAsync(It.IsAny<QuinielaMember>()), shouldUpdateMembers ? Times.Once() : Times.Never());
        Assert.Equal(shouldUpdateMembers ? 1 : 7, member.TotalHits);
    }

    private static Mock<IUnitOfWork> CreateUnitOfWork(
        Week week,
        IEnumerable<QuinielaMember> members,
        IEnumerable<Domain.Entities.Match> matches,
        IEnumerable<Pick> historicalPicks)
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        var quinielaRepository = new Mock<IQuinielaRepository>();
        var weekRepository = new Mock<IWeekRepository>();
        var memberRepository = new Mock<IQuinielaMemberRepository>();
        var matchRepository = new Mock<IMatchRepository>();
        var pickRepository = new Mock<IPickRepository>();
        var awardRepository = new Mock<IWeeklyAwardRepository>();

        quinielaRepository.Setup(r => r.GetAsync(10)).ReturnsAsync(new Quiniela { Id = 10 });
        weekRepository.Setup(r => r.GetAsync(week.Id)).ReturnsAsync(week);
        memberRepository.Setup(r => r.GetMembersAsync(10)).ReturnsAsync(members.ToList());
        matchRepository.Setup(r => r.GetByWeekIdAsync(week.Id)).ReturnsAsync(matches.ToList());
        pickRepository.Setup(r => r.GetAllPicksForWeekAsync(10, week.Id)).ReturnsAsync(historicalPicks.Where(p => p.Match?.Week?.WeekNumber == week.WeekNumber).ToList());
        pickRepository.Setup(r => r.GetAllPicksForQuinielaAsync(10)).ReturnsAsync(historicalPicks.ToList());
        awardRepository.Setup(r => r.GetAllAwardsForQuinielaAsync(10)).ReturnsAsync(new List<WeeklyAward>());

        unitOfWork.SetupGet(u => u.Quinielas).Returns(quinielaRepository.Object);
        unitOfWork.SetupGet(u => u.Weeks).Returns(weekRepository.Object);
        unitOfWork.SetupGet(u => u.QuinielaMembers).Returns(memberRepository.Object);
        unitOfWork.SetupGet(u => u.Matches).Returns(matchRepository.Object);
        unitOfWork.SetupGet(u => u.Picks).Returns(pickRepository.Object);
        unitOfWork.SetupGet(u => u.WeeklyAwards).Returns(awardRepository.Object);

        return unitOfWork;
    }
}
