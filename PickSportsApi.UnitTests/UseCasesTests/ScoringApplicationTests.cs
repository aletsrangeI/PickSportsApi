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

        quinielaRepository.Setup(r => r.GetAsync(10)).ReturnsAsync(new Quiniela { Id = 10 });
        weekRepository.Setup(r => r.GetAsync(week.Id)).ReturnsAsync(week);
        memberRepository.Setup(r => r.GetMembersAsync(10)).ReturnsAsync(members.ToList());
        matchRepository.Setup(r => r.GetByWeekIdAsync(week.Id)).ReturnsAsync(matches.ToList());
        pickRepository.Setup(r => r.GetAllPicksForWeekAsync(10, week.Id)).ReturnsAsync(historicalPicks.Where(p => p.Match?.Week?.WeekNumber == week.WeekNumber).ToList());
        pickRepository.Setup(r => r.GetAllPicksForQuinielaAsync(10)).ReturnsAsync(historicalPicks.ToList());

        unitOfWork.SetupGet(u => u.Quinielas).Returns(quinielaRepository.Object);
        unitOfWork.SetupGet(u => u.Weeks).Returns(weekRepository.Object);
        unitOfWork.SetupGet(u => u.QuinielaMembers).Returns(memberRepository.Object);
        unitOfWork.SetupGet(u => u.Matches).Returns(matchRepository.Object);
        unitOfWork.SetupGet(u => u.Picks).Returns(pickRepository.Object);

        return unitOfWork;
    }
}
