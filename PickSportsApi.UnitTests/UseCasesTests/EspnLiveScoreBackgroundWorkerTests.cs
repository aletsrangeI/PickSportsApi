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
    private readonly Mock<IPushNotificationLogRepository> _mockPushNotificationLogs = new();

    public EspnLiveScoreBackgroundWorkerTests()
    {
        _mockUow.Setup(u => u.Seasons).Returns(_mockSeasons.Object);
        _mockUow.Setup(u => u.Weeks).Returns(_mockWeeks.Object);
        _mockUow.Setup(u => u.Matches).Returns(_mockMatches.Object);
        _mockUow.Setup(u => u.Quinielas).Returns(_mockQuinielas.Object);
        _mockUow.Setup(u => u.QuinielaMembers).Returns(_mockMembers.Object);
        _mockUow.Setup(u => u.Picks).Returns(_mockPicks.Object);
        _mockUow.Setup(u => u.Leagues).Returns(_mockLeagues.Object);
        _mockUow.Setup(u => u.PushNotificationLogs).Returns(_mockPushNotificationLogs.Object);
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
            DateUtc = DateTime.UtcNow,
            Active = true
        };

        var ongoingMatch = new DbMatch
        {
            Id = 1002,
            WeekId = 100,
            StatusState = "in",
            HomeTeam = new Team { Abbreviation = "CRU" },
            AwayTeam = new Team { Abbreviation = "PUM" },
            DateUtc = DateTime.UtcNow,
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
        _mockPushNotificationLogs.Setup(l => l.HasMatchFinishedBeenSentAsync(1001, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

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

        // Assert - Registro persistente en PushNotificationLogs
        _mockPushNotificationLogs.Verify(l => l.InsertAsync(It.Is<PushNotificationLog>(log =>
            log.NotificationType == "MATCH_FINISHED_1001" &&
            log.QuinielaId == 50 &&
            log.WeekId == 100)), Times.Once);
    }

    [Fact]
    public async Task ProcessAutomationCycle_WhenMatchAlreadyNotified_ShouldNotSendAgain()
    {
        // Arrange
        var worker = new EspnLiveScoreBackgroundWorker(_mockScopeFactory.Object, _mockLogger.Object);

        var season = new Season { Id = 1, LeagueId = 10, Active = true };
        var week = new Week { Id = 100, SeasonId = 1, WeekNumber = 5, Status = "LOCKED", Active = true };
        var quiniela = new Quiniela { Id = 50, LeagueId = 10, Active = true };

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
            DateUtc = DateTime.UtcNow,
            Active = true
        };

        _mockSeasons.Setup(s => s.GetAllAsync()).ReturnsAsync(new List<Season> { season });
        _mockWeeks.Setup(w => w.GetBySeasonIdAsync(1)).ReturnsAsync(new List<Week> { week });
        _mockMatches.Setup(m => m.GetByWeekIdAsync(100)).ReturnsAsync(new List<DbMatch> { postMatch });
        _mockQuinielas.Setup(q => q.GetByLeagueIdAsync(10)).ReturnsAsync(new List<Quiniela> { quiniela });

        // Simular que el log ya existe (idempotencia)
        _mockPushNotificationLogs.Setup(l => l.HasMatchFinishedBeenSentAsync(1001, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await worker.ProcessAutomationCycleAsync(
            _mockUow.Object,
            _mockEspnSync.Object,
            _mockWebPush.Object,
            _mockScoringApp.Object,
            default);

        // Assert - Ninguna notificación enviada
        _mockWebPush.Verify(w => w.SendNotificationToUserAsync(
            It.IsAny<int>(),
            It.IsAny<PushNotificationPayload>(),
            default), Times.Never);
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

    [Fact]
    public async Task ProcessAutomationCycle_WhenAllMatchesFinished_ShouldPublishNextWeekIfDraft()
    {
        // Arrange
        var worker = new EspnLiveScoreBackgroundWorker(_mockScopeFactory.Object, _mockLogger.Object);

        var season = new Season { Id = 1, LeagueId = 10, Active = true };
        var currentWeek = new Week { Id = 100, SeasonId = 1, WeekNumber = 5, Status = "LOCKED", Active = true };
        var nextWeek = new Week { Id = 101, SeasonId = 1, WeekNumber = 6, Status = "DRAFT", Active = true };
        var quiniela = new Quiniela { Id = 50, LeagueId = 10, OwnerId = 999, Active = true, Name = "Liga Master" };

        var match1 = new DbMatch { Id = 1, WeekId = 100, StatusState = "post", Active = true };

        _mockSeasons.Setup(s => s.GetAllAsync()).ReturnsAsync(new List<Season> { season });
        _mockWeeks.Setup(w => w.GetBySeasonIdAsync(1)).ReturnsAsync(new List<Week> { currentWeek, nextWeek });
        _mockMatches.Setup(m => m.GetByWeekIdAsync(100)).ReturnsAsync(new List<DbMatch> { match1 });
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
        Assert.Equal("SCORED", currentWeek.Status);
        Assert.Equal("PUBLISHED", nextWeek.Status);
        Assert.NotNull(nextWeek.PublishedAt);
        _mockWeeks.Verify(w => w.Update(nextWeek), Times.Once);
        _mockUow.Verify(u => u.Save(It.IsAny<CancellationToken>()), Times.AtLeast(2));
        _mockEspnSync.Verify(e => e.SyncWeekAsync(101, It.IsAny<CancellationToken>()), Times.Once);
        _mockWebPush.Verify(w => w.SendNotificationToQuinielaAsync(
            50,
            It.Is<PushNotificationPayload>(p => p.Title.Contains("disponible")),
            default), Times.Once);
    }

    [Fact]
    public async Task ProcessAutomationCycle_AlCalificarLaUltimaJornadaRegular_NoPublicaLaJornadaDeLiguilla()
    {
        // Arrange: Liga MX con 17 jornadas regulares; la J18 (Liguilla) llegó del sync en DRAFT
        var worker = new EspnLiveScoreBackgroundWorker(_mockScopeFactory.Object, _mockLogger.Object);

        var season = new Season { Id = 1, LeagueId = 10, Active = true };
        var lastRegularWeek = new Week { Id = 117, SeasonId = 1, WeekNumber = 17, Status = "LOCKED", Active = true };
        var liguillaWeek = new Week { Id = 118, SeasonId = 1, WeekNumber = 18, Status = "DRAFT", Active = true };
        var quiniela = new Quiniela { Id = 50, LeagueId = 10, OwnerId = 999, Active = true, Name = "Liga Master" };

        _mockSeasons.Setup(s => s.GetAllAsync()).ReturnsAsync(new List<Season> { season });
        _mockLeagues.Setup(l => l.GetAsync(10)).ReturnsAsync(new League { Id = 10, WeeksCount = 17 });
        _mockWeeks.Setup(w => w.GetBySeasonIdAsync(1)).ReturnsAsync(new List<Week> { lastRegularWeek, liguillaWeek });
        _mockMatches.Setup(m => m.GetByWeekIdAsync(117)).ReturnsAsync(new List<DbMatch> { new() { Id = 1, WeekId = 117, StatusState = "post", Active = true } });
        _mockQuinielas.Setup(q => q.GetByLeagueIdAsync(10)).ReturnsAsync(new List<Quiniela> { quiniela });
        _mockScoringApp.Setup(s => s.ScoreWeekAsync(50, 117, 999))
            .ReturnsAsync(new Response<ScoreWeekResultDto> { isSuccess = true });

        // Act
        await worker.ProcessAutomationCycleAsync(_mockUow.Object, _mockEspnSync.Object, _mockWebPush.Object, _mockScoringApp.Object, default);

        // Assert: la J17 se califica, la J18 queda en DRAFT sin sync ni push de "disponible"
        Assert.Equal("SCORED", lastRegularWeek.Status);
        _mockScoringApp.Verify(s => s.ScoreWeekAsync(50, 117, 999), Times.Once);
        Assert.Equal("DRAFT", liguillaWeek.Status);
        Assert.Null(liguillaWeek.PublishedAt);
        _mockEspnSync.Verify(e => e.SyncWeekAsync(118, It.IsAny<CancellationToken>()), Times.Never);
        _mockWebPush.Verify(w => w.SendNotificationToQuinielaAsync(
            It.IsAny<int>(),
            It.Is<PushNotificationPayload>(p => p.Title.Contains("disponible")),
            default), Times.Never);
    }

    [Fact]
    public async Task ProcessAutomationCycle_JornadaDeLiguillaPublicada_NoSeBloqueaNiAutollena()
    {
        // Arrange: una jornada de Liguilla quedó PUBLISHED (p. ej. por un cambio manual) y su primer partido ya arrancó
        var worker = new EspnLiveScoreBackgroundWorker(_mockScopeFactory.Object, _mockLogger.Object);

        var season = new Season { Id = 1, LeagueId = 10, Active = true };
        var liguillaWeek = new Week { Id = 118, SeasonId = 1, WeekNumber = 18, Status = "PUBLISHED", FirstGameUtc = DateTime.UtcNow.AddMinutes(-5), Active = true };
        var quiniela = new Quiniela { Id = 50, LeagueId = 10, Active = true, Name = "Liga Master" };

        _mockSeasons.Setup(s => s.GetAllAsync()).ReturnsAsync(new List<Season> { season });
        _mockLeagues.Setup(l => l.GetAsync(10)).ReturnsAsync(new League { Id = 10, WeeksCount = 17 });
        _mockWeeks.Setup(w => w.GetBySeasonIdAsync(1)).ReturnsAsync(new List<Week> { liguillaWeek });
        _mockMatches.Setup(m => m.GetByWeekIdAsync(118)).ReturnsAsync(new List<DbMatch> { new() { Id = 1, WeekId = 118, StatusState = "in", Active = true } });
        _mockQuinielas.Setup(q => q.GetByLeagueIdAsync(10)).ReturnsAsync(new List<Quiniela> { quiniela });

        // Act
        await worker.ProcessAutomationCycleAsync(_mockUow.Object, _mockEspnSync.Object, _mockWebPush.Object, _mockScoringApp.Object, default);

        // Assert
        Assert.Equal("PUBLISHED", liguillaWeek.Status);
        _mockPicks.Verify(p => p.InsertAsync(It.IsAny<Pick>()), Times.Never);
        _mockWebPush.Verify(w => w.SendNotificationToQuinielaAsync(It.IsAny<int>(), It.IsAny<PushNotificationPayload>(), default), Times.Never);
    }

    [Fact]
    public async Task ProcessAutomationCycle_JornadaDeOtraTemporada_SoloAutollenaQuinielasDeEsaTemporada()
    {
        // Arrange: arranca una jornada de la temporada 1. La quiniela 50 juega la temporada 3 (H-010);
        // la quiniela 60 aún no tiene picks.
        var worker = new EspnLiveScoreBackgroundWorker(_mockScopeFactory.Object, _mockLogger.Object);

        var season = new Season { Id = 1, LeagueId = 10, Active = true };
        var week = new Week { Id = 110, SeasonId = 1, WeekNumber = 10, Status = "PUBLISHED", FirstGameUtc = DateTime.UtcNow.AddMinutes(-5), Active = true };
        var quinielaOtraTemporada = new Quiniela { Id = 50, LeagueId = 10, Active = true, Name = "Liga MX Apertura" };
        var quinielaNueva = new Quiniela { Id = 60, LeagueId = 10, Active = true, Name = "Quiniela Nueva" };
        var match = new DbMatch { Id = 1001, WeekId = 110, StatusState = "in", HomeTeam = new Team { Abbreviation = "AME" }, AwayTeam = new Team { Abbreviation = "CHI" }, Active = true };

        var aperturaWeek = new Week { Id = 311, SeasonId = 3, WeekNumber = 11 };
        var aperturaPick = new Pick { QuinielaId = 50, MemberId = 200, MatchId = 3001, Match = new DbMatch { Id = 3001, Week = aperturaWeek, DateUtc = DateTime.UtcNow.AddDays(1) } };

        _mockSeasons.Setup(s => s.GetAllAsync()).ReturnsAsync(new List<Season> { season });
        _mockLeagues.Setup(l => l.GetAsync(10)).ReturnsAsync(new League { Id = 10, WeeksCount = 17 });
        _mockWeeks.Setup(w => w.GetBySeasonIdAsync(1)).ReturnsAsync(new List<Week> { week });
        _mockMatches.Setup(m => m.GetByWeekIdAsync(110)).ReturnsAsync(new List<DbMatch> { match });
        _mockQuinielas.Setup(q => q.GetByLeagueIdAsync(10)).ReturnsAsync(new List<Quiniela> { quinielaOtraTemporada, quinielaNueva });
        _mockPicks.Setup(p => p.GetAllPicksForQuinielaAsync(50)).ReturnsAsync(new List<Pick> { aperturaPick });
        _mockPicks.Setup(p => p.GetAllPicksForQuinielaAsync(60)).ReturnsAsync(new List<Pick>());
        _mockMembers.Setup(m => m.GetMembersAsync(50)).ReturnsAsync(new List<QuinielaMember> { new() { Id = 200, QuinielaId = 50, Active = true } });
        _mockMembers.Setup(m => m.GetMembersAsync(60)).ReturnsAsync(new List<QuinielaMember> { new() { Id = 300, QuinielaId = 60, Active = true } });
        _mockPicks.Setup(p => p.GetAllPicksForWeekAsync(It.IsAny<int>(), 110)).ReturnsAsync(new List<Pick>());
        _mockUow.Setup(u => u.Sports.GetAsync(It.IsAny<int>())).ReturnsAsync(new Sport { HasDraw = true });

        // Act
        await worker.ProcessAutomationCycleAsync(_mockUow.Object, _mockEspnSync.Object, _mockWebPush.Object, _mockScoringApp.Object, default);

        // Assert: la quiniela 50 no recibe picks ni push; la 60 sí
        _mockPicks.Verify(p => p.InsertAsync(It.Is<Pick>(pick => pick.QuinielaId == 50)), Times.Never);
        _mockWebPush.Verify(w => w.SendNotificationToQuinielaAsync(50, It.IsAny<PushNotificationPayload>(), default), Times.Never);
        _mockPicks.Verify(p => p.InsertAsync(It.Is<Pick>(pick => pick.QuinielaId == 60 && pick.IsAutoFilled)), Times.Once);
        _mockWebPush.Verify(w => w.SendNotificationToQuinielaAsync(60, It.Is<PushNotificationPayload>(p => p.Title.Contains("bloqueada")), default), Times.Once);
    }
}
