using Common;
using Domain.Entities;
using DTO.Notifications;
using DTO.Scoring;
using Interface.Persistence;
using Interface.UseCases;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using WebApi.BackgroundServices;
using Xunit;
using DbMatch = Domain.Entities.Match;

namespace PickSportsApi.UnitTests.UseCasesTests;

public class EspnLiveScoreBackgroundWorkerTests
{
    private readonly Mock<IUnitOfWork> _mockUow = new();
    private readonly Mock<IEspnSyncService> _mockEspnSync = new();
    private readonly Mock<IWebPushNotificationService> _mockWebPush = new();
    private readonly Mock<IScoringApplication> _mockScoringApp = new();
    private readonly Mock<IAppLogger<EspnLiveScoreBackgroundWorker>> _mockLogger = new();
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory = new();

    private readonly Mock<ISeasonRepository> _mockSeasons = new();
    private readonly Mock<IWeekRepository> _mockWeeks = new();
    private readonly Mock<IMatchRepository> _mockMatches = new();
    private readonly Mock<IQuinielaRepository> _mockQuinielas = new();
    private readonly Mock<IQuinielaMemberRepository> _mockMembers = new();
    private readonly Mock<IPickRepository> _mockPicks = new();
    private readonly Mock<ILeagueRepository> _mockLeagues = new();

    public EspnLiveScoreBackgroundWorkerTests()
    {
        _mockUow.Setup(u => u.Seasons).Returns(_mockSeasons.Object);
        _mockUow.Setup(u => u.Weeks).Returns(_mockWeeks.Object);
        _mockUow.Setup(u => u.Matches).Returns(_mockMatches.Object);
        _mockUow.Setup(u => u.Quinielas).Returns(_mockQuinielas.Object);
        _mockUow.Setup(u => u.QuinielaMembers).Returns(_mockMembers.Object);
        _mockUow.Setup(u => u.Picks).Returns(_mockPicks.Object);
        _mockUow.Setup(u => u.Leagues).Returns(_mockLeagues.Object);
    }

    [Fact]
    public async Task ProcessAutomationCycle_WhenFirstGameStarted_ShouldLockWeekAndAutofill()
    {
        // Arrange
        var worker = new EspnLiveScoreBackgroundWorker(_mockScopeFactory.Object, _mockLogger.Object);

        var season = new Season { Id = 1, LeagueId = 10, Active = true };
        var week = new Week
        {
            Id = 100,
            SeasonId = 1,
            WeekNumber = 5,
            Status = "PUBLISHED",
            FirstGameUtc = DateTime.UtcNow.AddMinutes(-5),
            Active = true
        };
        var quiniela = new Quiniela { Id = 50, LeagueId = 10, Active = true, Name = "Quiniela Amigos" };
        var match = new DbMatch
        {
            Id = 1001,
            WeekId = 100,
            StatusState = "in",
            HomeTeam = new Team { Abbreviation = "AME" },
            AwayTeam = new Team { Abbreviation = "CHI" },
            Active = true
        };
        var member = new QuinielaMember { Id = 200, QuinielaId = 50, UserId = 99, Active = true };

        _mockSeasons.Setup(s => s.GetAllAsync()).ReturnsAsync(new List<Season> { season });
        _mockWeeks.Setup(w => w.GetBySeasonIdAsync(1)).ReturnsAsync(new List<Week> { week });
        _mockMatches.Setup(m => m.GetByWeekIdAsync(100)).ReturnsAsync(new List<DbMatch> { match });
        _mockQuinielas.Setup(q => q.GetByLeagueIdAsync(10)).ReturnsAsync(new List<Quiniela> { quiniela });
        _mockMembers.Setup(m => m.GetMembersAsync(50)).ReturnsAsync(new List<QuinielaMember> { member });
        _mockPicks.Setup(p => p.GetAllPicksForWeekAsync(50, 100)).ReturnsAsync(new List<Pick>()); // Sin picks -> autofill

        // Act
        var hasActive = await worker.ProcessAutomationCycleAsync(
            _mockUow.Object,
            _mockEspnSync.Object,
            _mockWebPush.Object,
            _mockScoringApp.Object,
            default);

        // Assert
        Assert.Equal("LOCKED", week.Status);
        Assert.NotNull(week.LockedAt);
        _mockPicks.Verify(p => p.InsertAsync(It.Is<Pick>(pick =>
            pick.QuinielaId == 50 &&
            pick.MemberId == 200 &&
            pick.MatchId == 1001 &&
            pick.IsAutoFilled)), Times.Once);

        _mockWebPush.Verify(w => w.SendNotificationToQuinielaAsync(
            50,
            It.Is<PushNotificationPayload>(p => p.Title.Contains("bloqueada")),
            default), Times.Once);

        Assert.True(hasActive);
    }

    [Fact]
    public async Task ProcessAutomationCycle_WhenMatchFinished_ShouldSendHitAndMissNotifications()
    {
        // Arrange
        var worker = new EspnLiveScoreBackgroundWorker(_mockScopeFactory.Object, _mockLogger.Object);

        var season = new Season { Id = 1, LeagueId = 10, Active = true };
        var week = new Week { Id = 100, SeasonId = 1, WeekNumber = 5, Status = "LOCKED", Active = true };
        var quiniela = new Quiniela { Id = 50, LeagueId = 10, Active = true };

        var preMatch = new DbMatch
        {
            Id = 1001,
            WeekId = 100,
            StatusState = "in",
            HomeScore = 1,
            AwayScore = 1,
            HomeTeam = new Team { Abbreviation = "AME" },
            AwayTeam = new Team { Abbreviation = "CHI" },
            Active = true
        };

        var postMatch = new DbMatch
        {
            Id = 1001,
            WeekId = 100,
            StatusState = "post",
            HomeScore = 2,
            AwayScore = 1,
            WinnerAbbr = "AME",
            HomeTeam = new Team { Abbreviation = "AME" },
            AwayTeam = new Team { Abbreviation = "CHI" },
            Active = true
        };

        var ongoingMatch = new DbMatch
        {
            Id = 1002,
            WeekId = 100,
            StatusState = "in",
            HomeTeam = new Team { Abbreviation = "CRU" },
            AwayTeam = new Team { Abbreviation = "PUM" },
            Active = true
        };

        var member1 = new QuinielaMember { Id = 201, QuinielaId = 50, UserId = 11, Active = true };
        var member2 = new QuinielaMember { Id = 202, QuinielaId = 50, UserId = 22, Active = true };

        var hitPick = new Pick { Id = 1, MemberId = 201, MatchId = 1001, PickAbbr = "AME" };
        var missPick = new Pick { Id = 2, MemberId = 202, MatchId = 1001, PickAbbr = "CHI" };

        _mockSeasons.Setup(s => s.GetAllAsync()).ReturnsAsync(new List<Season> { season });
        _mockWeeks.Setup(w => w.GetBySeasonIdAsync(1)).ReturnsAsync(new List<Week> { week });

        // Antes del sync: "in"
        // Después del sync: "post" (y match2 sigue en "in")
        var matchCallCount = 0;
        _mockMatches.Setup(m => m.GetByWeekIdAsync(100))
            .ReturnsAsync(() =>
            {
                matchCallCount++;
                return matchCallCount == 1 
                    ? new List<DbMatch> { preMatch, ongoingMatch } 
                    : new List<DbMatch> { postMatch, ongoingMatch };
            });

        _mockQuinielas.Setup(q => q.GetByLeagueIdAsync(10)).ReturnsAsync(new List<Quiniela> { quiniela });
        _mockMembers.Setup(m => m.GetMembersAsync(50)).ReturnsAsync(new List<QuinielaMember> { member1, member2 });
        _mockPicks.Setup(p => p.GetAllPicksForWeekAsync(50, 100)).ReturnsAsync(new List<Pick> { hitPick, missPick });

        // Act
        await worker.ProcessAutomationCycleAsync(
            _mockUow.Object,
            _mockEspnSync.Object,
            _mockWebPush.Object,
            _mockScoringApp.Object,
            default);

        // Assert - Member1 acertó -> Notificación positiva
        _mockWebPush.Verify(w => w.SendNotificationToUserAsync(
            11,
            It.Is<PushNotificationPayload>(p => p.Title.Contains("Acertaste") && p.Message.Contains("AME 2 - 1 CHI")),
            default), Times.Once);

        // Assert - Member2 falló -> Notificación negativa
        _mockWebPush.Verify(w => w.SendNotificationToUserAsync(
            22,
            It.Is<PushNotificationPayload>(p => p.Title.Contains("Fallaste") && p.Message.Contains("AME 2 - 1 CHI")),
            default), Times.Once);
    }

    [Fact]
    public async Task ProcessAutomationCycle_WhenAllMatchesFinished_ShouldScoreWeek()
    {
        // Arrange
        var worker = new EspnLiveScoreBackgroundWorker(_mockScopeFactory.Object, _mockLogger.Object);

        var season = new Season { Id = 1, LeagueId = 10, Active = true };
        var week = new Week { Id = 100, SeasonId = 1, WeekNumber = 5, Status = "LOCKED", Active = true };
        var quiniela = new Quiniela { Id = 50, LeagueId = 10, OwnerId = 999, Active = true, Name = "Liga Master" };

        var match1 = new DbMatch { Id = 1, WeekId = 100, StatusState = "post", Active = true };
        var match2 = new DbMatch { Id = 2, WeekId = 100, StatusState = "post", Active = true };

        _mockSeasons.Setup(s => s.GetAllAsync()).ReturnsAsync(new List<Season> { season });
        _mockWeeks.Setup(w => w.GetBySeasonIdAsync(1)).ReturnsAsync(new List<Week> { week });
        _mockMatches.Setup(m => m.GetByWeekIdAsync(100)).ReturnsAsync(new List<DbMatch> { match1, match2 });
        _mockQuinielas.Setup(q => q.GetByLeagueIdAsync(10)).ReturnsAsync(new List<Quiniela> { quiniela });

        _mockScoringApp.Setup(s => s.ScoreWeekAsync(50, 100, 999))
            .ReturnsAsync(new Response<ScoreWeekResultDto> { isSuccess = true });

        // Act
        await worker.ProcessAutomationCycleAsync(
            _mockUow.Object,
            _mockEspnSync.Object,
            _mockWebPush.Object,
            _mockScoringApp.Object,
            default);

        // Assert
        Assert.Equal("SCORED", week.Status);
        Assert.NotNull(week.ScoredAt);
        _mockScoringApp.Verify(s => s.ScoreWeekAsync(50, 100, 999), Times.Once);
        _mockWebPush.Verify(w => w.SendNotificationToQuinielaAsync(
            50,
            It.Is<PushNotificationPayload>(p => p.Title.Contains("finalizada")),
            default), Times.Once);
    }
}
