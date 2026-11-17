using Common;
using Domain.Entities;
using DTO.Notifications;
using FluentAssertions;
using Interface.Persistence;
using Interface.UseCases;
using Moq;
using UseCases.Notifications;
using Xunit;
using DbMatch = Domain.Entities.Match;

namespace PickSportsApi.UnitTests.UseCasesTests;

public class QuinielaReminderApplicationTests
{
    private const int LeagueId = 10;
    private const int FmfLiguillaWeeksCount = 17;
    private const string FmfLiguillaFormatCode = "FMF_LIGUILLA";

    private readonly Mock<IUnitOfWork> _mockUow = new();
    private readonly Mock<IWebPushNotificationService> _mockWebPush = new();
    private readonly Mock<IAppLogger<QuinielaReminderApplication>> _mockLogger = new();

    private readonly Mock<IQuinielaRepository> _mockQuinielas = new();
    private readonly Mock<ILeagueRepository> _mockLeagues = new();
    private readonly Mock<ISeasonRepository> _mockSeasons = new();
    private readonly Mock<IWeekRepository> _mockWeeks = new();
    private readonly Mock<IMatchRepository> _mockMatches = new();
    private readonly Mock<IQuinielaMemberRepository> _mockMembers = new();
    private readonly Mock<IPickRepository> _mockPicks = new();
    private readonly Mock<IPushNotificationLogRepository> _mockLogs = new();

    public QuinielaReminderApplicationTests()
    {
        _mockUow.Setup(u => u.Quinielas).Returns(_mockQuinielas.Object);
        _mockUow.Setup(u => u.Leagues).Returns(_mockLeagues.Object);
        _mockUow.Setup(u => u.Seasons).Returns(_mockSeasons.Object);
        _mockUow.Setup(u => u.Weeks).Returns(_mockWeeks.Object);
        _mockUow.Setup(u => u.Matches).Returns(_mockMatches.Object);
        _mockUow.Setup(u => u.QuinielaMembers).Returns(_mockMembers.Object);
        _mockUow.Setup(u => u.Picks).Returns(_mockPicks.Object);
        _mockUow.Setup(u => u.PushNotificationLogs).Returns(_mockLogs.Object);

        // SPEC-027: fixture por defecto de la liga de la quiniela (Liga MX, Liguilla, 17 jornadas regulares)
        _mockLeagues.Setup(l => l.GetAsync(LeagueId))
            .ReturnsAsync(new League { Id = LeagueId, KnockoutFormatCode = FmfLiguillaFormatCode, WeeksCount = FmfLiguillaWeeksCount });
    }

    private void ArrangeFinishedRegularSeasonForLeague(string? knockoutFormatCode, int weeksCount = FmfLiguillaWeeksCount)
    {
        var quiniela = new Quiniela { Id = 1, LeagueId = LeagueId, Name = "Quiniela Amigos", Active = true };
        var season = new Season { Id = 5, LeagueId = LeagueId, Active = true };
        var weeks = Enumerable.Range(1, weeksCount).Select(i => new Week
        {
            Id = 100 + i,
            SeasonId = 5,
            WeekNumber = i,
            Status = "SCORED",
            Active = true
        }).ToList();

        _mockQuinielas.Setup(q => q.GetAllAsync()).ReturnsAsync(new List<Quiniela> { quiniela });
        _mockSeasons.Setup(s => s.GetAllAsync()).ReturnsAsync(new List<Season> { season });
        _mockWeeks.Setup(w => w.GetBySeasonIdAsync(5)).ReturnsAsync(weeks);
        _mockLeagues.Setup(l => l.GetAsync(LeagueId)).ReturnsAsync(new League
        {
            Id = LeagueId,
            KnockoutFormatCode = knockoutFormatCode,
            WeeksCount = weeksCount
        });
    }

    [Fact]
    public async Task ProcessDailyReminders_OnMondayAfterNoon_ShouldSendWeekOpenedNotification()
    {
        // ARRANGE: Lunes a las 13:00 hora de México (CST = UTC-6 -> 19:00 UTC)
        // 2026-09-28 es Lunes.
        var mondayUtc = new DateTime(2026, 9, 28, 19, 0, 0, DateTimeKind.Utc);

        var quiniela = new Quiniela { Id = 1, LeagueId = 10, Name = "Quiniela Amigos", Active = true };
        var season = new Season { Id = 5, LeagueId = 10, Active = true };
        var week = new Week { Id = 100, SeasonId = 5, WeekNumber = 8, Status = "PUBLISHED", Active = true };

        _mockQuinielas.Setup(q => q.GetAllAsync()).ReturnsAsync(new List<Quiniela> { quiniela });
        _mockSeasons.Setup(s => s.GetAllAsync()).ReturnsAsync(new List<Season> { season });
        _mockWeeks.Setup(w => w.GetBySeasonIdAsync(5)).ReturnsAsync(new List<Week> { week });

        _mockLogs.Setup(l => l.HasWeekOpenedBeenSentAsync(100, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockWebPush.Setup(w => w.SendNotificationToQuinielaAsync(1, It.IsAny<PushNotificationPayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var service = new QuinielaReminderApplication(_mockUow.Object, _mockWebPush.Object, _mockLogger.Object);

        // ACT
        var count = await service.ProcessDailyRemindersAsync(mondayUtc);

        // ASSERT
        Assert.True(count > 0);
        _mockWebPush.Verify(w => w.SendNotificationToQuinielaAsync(
            1,
            It.Is<PushNotificationPayload>(p => p.Title.Contains("disponible") && p.Message.Contains("Jornada 8")),
            It.IsAny<CancellationToken>()),
            Times.Once);

        _mockLogs.Verify(l => l.InsertAsync(It.Is<PushNotificationLog>(log =>
            log.NotificationType == "WEEK_OPENED" &&
            log.WeekId == 100 &&
            log.QuinielaId == 1)),
            Times.Once);
    }

    [Fact]
    public async Task ProcessDailyReminders_QuinielaConSeasonId_SoloNotificaJornadasDeSuTemporada()
    {
        // ARRANGE (SPEC-020): Lunes 13:00 CST. La liga tiene dos temporadas activas, la quiniela juega la 5.
        var mondayUtc = new DateTime(2026, 9, 28, 19, 0, 0, DateTimeKind.Utc);

        var quiniela = new Quiniela { Id = 1, LeagueId = 10, Name = "Quiniela Apertura", SeasonId = 5 };
        var seasonOwn = new Season { Id = 5, LeagueId = 10, Active = true };
        var seasonOther = new Season { Id = 6, LeagueId = 10, Active = true };
        var weekOwn = new Week { Id = 100, SeasonId = 5, WeekNumber = 8, Status = "PUBLISHED", Active = true };
        var weekOther = new Week { Id = 200, SeasonId = 6, WeekNumber = 3, Status = "PUBLISHED", Active = true };

        _mockQuinielas.Setup(q => q.GetAllAsync()).ReturnsAsync(new List<Quiniela> { quiniela });
        _mockSeasons.Setup(s => s.GetAllAsync()).ReturnsAsync(new List<Season> { seasonOwn, seasonOther });
        _mockWeeks.Setup(w => w.GetBySeasonIdAsync(5)).ReturnsAsync(new List<Week> { weekOwn });
        _mockWeeks.Setup(w => w.GetBySeasonIdAsync(6)).ReturnsAsync(new List<Week> { weekOther });
        _mockWebPush.Setup(w => w.SendNotificationToQuinielaAsync(1, It.IsAny<PushNotificationPayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var service = new QuinielaReminderApplication(_mockUow.Object, _mockWebPush.Object, _mockLogger.Object);

        // ACT
        await service.ProcessDailyRemindersAsync(mondayUtc);

        // ASSERT: avisa la Jornada 8 de su temporada y nunca la Jornada 3 de la otra
        _mockLogs.Verify(l => l.InsertAsync(It.Is<PushNotificationLog>(log =>
            log.NotificationType == "WEEK_OPENED" && log.WeekId == 100 && log.QuinielaId == 1)), Times.Once);
        _mockLogs.Verify(l => l.InsertAsync(It.Is<PushNotificationLog>(log => log.WeekId == 200)), Times.Never);
        _mockWeeks.Verify(w => w.GetBySeasonIdAsync(6), Times.Never);
        _mockWebPush.Verify(w => w.SendNotificationToQuinielaAsync(
            1, It.Is<PushNotificationPayload>(p => p.Message.Contains("Jornada 3")), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessDailyReminders_QuinielaSinSeasonId_ConservaTodasLasTemporadasDeLaLiga()
    {
        // ARRANGE: SeasonId null = comportamiento anterior (todas las temporadas activas de la liga)
        var mondayUtc = new DateTime(2026, 9, 28, 19, 0, 0, DateTimeKind.Utc);

        var quiniela = new Quiniela { Id = 1, LeagueId = 10, Name = "Quiniela Legada" };
        var seasonA = new Season { Id = 5, LeagueId = 10, Active = true };
        var seasonB = new Season { Id = 6, LeagueId = 10, Active = true };

        _mockQuinielas.Setup(q => q.GetAllAsync()).ReturnsAsync(new List<Quiniela> { quiniela });
        _mockSeasons.Setup(s => s.GetAllAsync()).ReturnsAsync(new List<Season> { seasonA, seasonB });
        _mockWeeks.Setup(w => w.GetBySeasonIdAsync(5)).ReturnsAsync(new List<Week> { new() { Id = 100, SeasonId = 5, WeekNumber = 8, Status = "PUBLISHED", Active = true } });
        _mockWeeks.Setup(w => w.GetBySeasonIdAsync(6)).ReturnsAsync(new List<Week> { new() { Id = 200, SeasonId = 6, WeekNumber = 3, Status = "PUBLISHED", Active = true } });

        var service = new QuinielaReminderApplication(_mockUow.Object, _mockWebPush.Object, _mockLogger.Object);

        // ACT
        await service.ProcessDailyRemindersAsync(mondayUtc);

        // ASSERT
        _mockWeeks.Verify(w => w.GetBySeasonIdAsync(5), Times.AtLeastOnce);
        _mockWeeks.Verify(w => w.GetBySeasonIdAsync(6), Times.AtLeastOnce);
        _mockLogs.Verify(l => l.InsertAsync(It.Is<PushNotificationLog>(log => log.WeekId == 100)), Times.Once);
        _mockLogs.Verify(l => l.InsertAsync(It.Is<PushNotificationLog>(log => log.WeekId == 200)), Times.Once);
    }

    [Fact]
    public async Task ProcessDailyReminders_QuinielaArchivada_NoEnviaRecordatorios()
    {
        // ARRANGE: quiniela archivada (SPEC-020) aunque siga marcada IsActive y tenga jornada publicada
        var mondayUtc = new DateTime(2026, 9, 28, 19, 0, 0, DateTimeKind.Utc);

        var archived = new Quiniela { Id = 1, LeagueId = 10, Name = "Apertura archivada", SeasonId = 5, ArchivedAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc) };
        var season = new Season { Id = 5, LeagueId = 10, Active = true };
        var week = new Week { Id = 100, SeasonId = 5, WeekNumber = 8, Status = "PUBLISHED", Active = true };

        _mockQuinielas.Setup(q => q.GetAllAsync()).ReturnsAsync(new List<Quiniela> { archived });
        _mockSeasons.Setup(s => s.GetAllAsync()).ReturnsAsync(new List<Season> { season });
        _mockWeeks.Setup(w => w.GetBySeasonIdAsync(5)).ReturnsAsync(new List<Week> { week });

        var service = new QuinielaReminderApplication(_mockUow.Object, _mockWebPush.Object, _mockLogger.Object);

        // ACT
        var count = await service.ProcessDailyRemindersAsync(mondayUtc);

        // ASSERT
        Assert.Equal(0, count);
        _mockWebPush.Verify(w => w.SendNotificationToQuinielaAsync(It.IsAny<int>(), It.IsAny<PushNotificationPayload>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockLogs.Verify(l => l.InsertAsync(It.IsAny<PushNotificationLog>()), Times.Never);
    }

    [Fact]
    public async Task ProcessDailyReminders_OnTuesdayAfter11AM_ShouldSendReminderOnlyToMembersWithIncompletePicks()
    {
        // ARRANGE: Martes a las 11:30 hora de México (CST = UTC-6 -> 17:30 UTC)
        // 2026-09-29 es Martes.
        var tuesdayUtc = new DateTime(2026, 9, 29, 17, 30, 0, DateTimeKind.Utc);

        var quiniela = new Quiniela { Id = 1, LeagueId = 10, Name = "Quiniela Amigos", Active = true };
        var season = new Season { Id = 5, LeagueId = 10, Active = true };
        var week = new Week
        {
            Id = 100,
            SeasonId = 5,
            WeekNumber = 8,
            Status = "PUBLISHED",
            FirstGameUtc = tuesdayUtc.AddDays(3), // Arranca el viernes
            Active = true
        };

        var match1 = new DbMatch { Id = 10, WeekId = 100, DateUtc = tuesdayUtc.AddDays(3) };
        var match2 = new DbMatch { Id = 11, WeekId = 100, DateUtc = tuesdayUtc.AddDays(3) };

        var memberCompleted = new QuinielaMember { Id = 101, UserId = 201, QuinielaId = 1, Active = true };
        var memberIncomplete = new QuinielaMember { Id = 102, UserId = 202, QuinielaId = 1, Active = true };

        // Miembro 101 tiene ambos picks
        var pick1 = new Pick { MemberId = 101, MatchId = 10, QuinielaId = 1 };
        var pick2 = new Pick { MemberId = 101, MatchId = 11, QuinielaId = 1 };
        // Miembro 102 solo tiene 1 pick de 2
        var pick3 = new Pick { MemberId = 102, MatchId = 10, QuinielaId = 1 };

        _mockQuinielas.Setup(q => q.GetAllAsync()).ReturnsAsync(new List<Quiniela> { quiniela });
        _mockSeasons.Setup(s => s.GetAllAsync()).ReturnsAsync(new List<Season> { season });
        _mockWeeks.Setup(w => w.GetBySeasonIdAsync(5)).ReturnsAsync(new List<Week> { week });
        _mockMatches.Setup(m => m.GetByWeekIdAsync(100)).ReturnsAsync(new List<DbMatch> { match1, match2 });
        _mockMembers.Setup(m => m.GetMembersAsync(1)).ReturnsAsync(new List<QuinielaMember> { memberCompleted, memberIncomplete });
        _mockPicks.Setup(p => p.GetAllPicksForWeekAsync(1, 100)).ReturnsAsync(new List<Pick> { pick1, pick2, pick3 });

        _mockLogs.Setup(l => l.HasNotificationBeenSentTodayAsync("INCOMPLETE_PICKS_REMINDER", 100, 1, 202, "2026-09-29", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockWebPush.Setup(w => w.SendNotificationToUserAsync(202, It.IsAny<PushNotificationPayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var service = new QuinielaReminderApplication(_mockUow.Object, _mockWebPush.Object, _mockLogger.Object);

        // ACT
        var count = await service.ProcessDailyRemindersAsync(tuesdayUtc);

        // ASSERT: Solo se le debió enviar al miembro con picks incompletos (User 202), NUNCA al 201
        Assert.Equal(1, count);
        _mockWebPush.Verify(w => w.SendNotificationToUserAsync(
            202,
            It.Is<PushNotificationPayload>(p => p.Title.Contains("Completa tu quiniela") && p.Message.Contains("1 pronóstico(s) pendientes")),
            It.IsAny<CancellationToken>()),
            Times.Once);

        _mockWebPush.Verify(w => w.SendNotificationToUserAsync(
            201,
            It.IsAny<PushNotificationPayload>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessDailyReminders_WhenIncompleteReminderAlreadySentToday_ShouldNotSendDuplicate()
    {
        // ARRANGE: Martes 11:30
        var tuesdayUtc = new DateTime(2026, 9, 29, 17, 30, 0, DateTimeKind.Utc);

        var quiniela = new Quiniela { Id = 1, LeagueId = 10, Name = "Quiniela Amigos", Active = true };
        var season = new Season { Id = 5, LeagueId = 10, Active = true };
        var week = new Week { Id = 100, SeasonId = 5, WeekNumber = 8, Status = "PUBLISHED", Active = true };
        var match = new DbMatch { Id = 10, WeekId = 100, DateUtc = tuesdayUtc.AddDays(2) };
        var member = new QuinielaMember { Id = 102, UserId = 202, QuinielaId = 1, Active = true };

        _mockQuinielas.Setup(q => q.GetAllAsync()).ReturnsAsync(new List<Quiniela> { quiniela });
        _mockSeasons.Setup(s => s.GetAllAsync()).ReturnsAsync(new List<Season> { season });
        _mockWeeks.Setup(w => w.GetBySeasonIdAsync(5)).ReturnsAsync(new List<Week> { week });
        _mockMatches.Setup(m => m.GetByWeekIdAsync(100)).ReturnsAsync(new List<DbMatch> { match });
        _mockMembers.Setup(m => m.GetMembersAsync(1)).ReturnsAsync(new List<QuinielaMember> { member });
        _mockPicks.Setup(p => p.GetAllPicksForWeekAsync(1, 100)).ReturnsAsync(new List<Pick>()); // 0 picks

        // Ya fue enviado hoy
        _mockLogs.Setup(l => l.HasNotificationBeenSentTodayAsync("INCOMPLETE_PICKS_REMINDER", 100, 1, 202, "2026-09-29", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new QuinielaReminderApplication(_mockUow.Object, _mockWebPush.Object, _mockLogger.Object);

        // ACT
        var count = await service.ProcessDailyRemindersAsync(tuesdayUtc);

        // ASSERT: 0 notificaciones enviadas
        Assert.Equal(0, count);
        _mockWebPush.Verify(w => w.SendNotificationToUserAsync(It.IsAny<int>(), It.IsAny<PushNotificationPayload>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessDailyReminders_OnMatchdayAfter1030AM_ShouldSendMorningRoundup()
    {
        // ARRANGE: Sábado a las 10:45 AM en hora México (16:45 UTC)
        // 2026-10-03 es Sábado.
        var saturdayUtc = new DateTime(2026, 10, 3, 16, 45, 0, DateTimeKind.Utc);

        var quiniela = new Quiniela { Id = 1, LeagueId = 10, Name = "Quiniela Amigos", Active = true };
        var season = new Season { Id = 5, LeagueId = 10, Active = true };
        var week = new Week { Id = 100, SeasonId = 5, WeekNumber = 8, Status = "LOCKED", Active = true };

        // Partido hoy sábado a las 19:00 hora de México (01:00 UTC del domingo)
        // Nota: en UTC es 2026-10-04 01:00, pero en CST es 2026-10-03 19:00 (hoy!)
        var matchSaturday = new DbMatch
        {
            Id = 50,
            WeekId = 100,
            DateUtc = new DateTime(2026, 10, 4, 1, 0, 0, DateTimeKind.Utc),
            HomeTeam = new Team { Abbreviation = "AME" },
            AwayTeam = new Team { Abbreviation = "CHI" }
        };

        _mockQuinielas.Setup(q => q.GetAllAsync()).ReturnsAsync(new List<Quiniela> { quiniela });
        _mockSeasons.Setup(s => s.GetAllAsync()).ReturnsAsync(new List<Season> { season });
        _mockWeeks.Setup(w => w.GetBySeasonIdAsync(5)).ReturnsAsync(new List<Week> { week });
        _mockMatches.Setup(m => m.GetByWeekIdAsync(100)).ReturnsAsync(new List<DbMatch> { matchSaturday });

        _mockLogs.Setup(l => l.HasNotificationBeenSentTodayAsync("MATCHDAY_MORNING_ROUNDUP", 100, 1, null, "2026-10-03", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockWebPush.Setup(w => w.SendNotificationToQuinielaAsync(1, It.IsAny<PushNotificationPayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        var service = new QuinielaReminderApplication(_mockUow.Object, _mockWebPush.Object, _mockLogger.Object);

        // ACT
        var count = await service.ProcessDailyRemindersAsync(saturdayUtc);

        // ASSERT: Se envió la cartelera del día
        Assert.True(count > 0);
        _mockWebPush.Verify(w => w.SendNotificationToQuinielaAsync(
            1,
            It.Is<PushNotificationPayload>(p => p.Title.Contains("Sábado de fútbol") && p.Message.Contains("AME vs CHI")),
            It.IsAny<CancellationToken>()),
            Times.Once);

        _mockLogs.Verify(l => l.InsertAsync(It.Is<PushNotificationLog>(log =>
            log.NotificationType == "MATCHDAY_MORNING_ROUNDUP" &&
            log.WeekId == 100 &&
            log.QuinielaId == 1 &&
            log.DateLocal == "2026-10-03")),
            Times.Once);
    }

    [Fact]
    public async Task ProcessDailyReminders_OneHourBeforeKickoff_ShouldSendLastHourAlertToMembersWithIncompletePicks()
    {
        // ARRANGE: Viernes a las 18:00 hora de México (00:00 UTC del sábado)
        // El primer partido arranca a las 19:00 hora de México (01:00 UTC del sábado) -> Falta exactamente 1 hora (60 min)
        var fridaySimulatedUtc = new DateTime(2026, 9, 26, 0, 0, 0, DateTimeKind.Utc);
        var matchKickoffUtc = new DateTime(2026, 9, 26, 1, 0, 0, DateTimeKind.Utc);

        var quiniela = new Quiniela { Id = 1, LeagueId = 10, Name = "Quiniela Amigos", Active = true };
        var season = new Season { Id = 5, LeagueId = 10, Active = true };
        var week = new Week { Id = 100, SeasonId = 5, WeekNumber = 10, Status = "PUBLISHED", FirstGameUtc = matchKickoffUtc, Active = true };

        var match1 = new DbMatch
        {
            Id = 50,
            WeekId = 100,
            DateUtc = matchKickoffUtc,
            HomeTeam = new Team { Name = "Puebla", Abbreviation = "PUE" },
            AwayTeam = new Team { Name = "Monterrey", Abbreviation = "MTY" }
        };
        var match2 = new DbMatch
        {
            Id = 51,
            WeekId = 100,
            DateUtc = matchKickoffUtc.AddHours(2),
            HomeTeam = new Team { Name = "Tijuana", Abbreviation = "TIJ" },
            AwayTeam = new Team { Name = "Atlas", Abbreviation = "ATS" }
        };

        var memberIncomplete = new QuinielaMember { Id = 10, QuinielaId = 1, UserId = 100, Active = true };
        var memberComplete = new QuinielaMember { Id = 11, QuinielaId = 1, UserId = 101, Active = true };

        var completePicks = new List<Pick>
        {
            new() { Id = 1, MemberId = 11, MatchId = 50, PickAbbr = "PUE" },
            new() { Id = 2, MemberId = 11, MatchId = 51, PickAbbr = "ATS" }
        };
        var incompletePicks = new List<Pick>
        {
            new() { Id = 3, MemberId = 10, MatchId = 50, PickAbbr = "PUE" }
            // Le falta match 51
        };

        _mockQuinielas.Setup(q => q.GetAllAsync()).ReturnsAsync(new List<Quiniela> { quiniela });
        _mockSeasons.Setup(s => s.GetAllAsync()).ReturnsAsync(new List<Season> { season });
        _mockWeeks.Setup(w => w.GetBySeasonIdAsync(5)).ReturnsAsync(new List<Week> { week });
        _mockMatches.Setup(m => m.GetByWeekIdAsync(100)).ReturnsAsync(new List<DbMatch> { match1, match2 });
        _mockMembers.Setup(m => m.GetMembersAsync(1)).ReturnsAsync(new List<QuinielaMember> { memberIncomplete, memberComplete });
        _mockPicks.Setup(p => p.GetAllPicksForWeekAsync(1, 100)).ReturnsAsync(completePicks.Concat(incompletePicks).ToList());

        // Aseguramos que la cartelera de la mañana (10:30) ya se haya enviado para aislar la regla de 1 hora
        _mockLogs.Setup(l => l.HasNotificationBeenSentTodayAsync("MATCHDAY_MORNING_ROUNDUP", 100, 1, null, "2026-09-25", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockLogs.Setup(l => l.HasNotificationBeenSentTodayAsync("LAST_HOUR_PICKS_REMINDER", 100, 1, 100, "2026-09-25", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockWebPush.Setup(w => w.SendNotificationToUserAsync(100, It.IsAny<PushNotificationPayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var service = new QuinielaReminderApplication(_mockUow.Object, _mockWebPush.Object, _mockLogger.Object);

        // ACT
        var count = await service.ProcessDailyRemindersAsync(fridaySimulatedUtc);

        // ASSERT
        Assert.True(count > 0);
        // Debe enviar alerta de última hora solo a User 100 (incompleto)
        // Debe enviar alerta de recordatorio a User 100 (incompleto)
        _mockWebPush.Verify(w => w.SendNotificationToUserAsync(
            100,
            It.Is<PushNotificationPayload>(p => p.Title.Contains("1 hora para el silbatazo") && p.Message.Contains("sin responder")),
            It.IsAny<CancellationToken>()),
            Times.Once);

        // Debe enviar aviso de disfrute a User 101 (completo)
        _mockWebPush.Verify(w => w.SendNotificationToUserAsync(
            101,
            It.Is<PushNotificationPayload>(p => p.Title.Contains("1 hora para el silbatazo") && p.Message.Contains("están listos")),
            It.IsAny<CancellationToken>()),
            Times.Once);

        // Debe registrar en PushNotificationLogs para ambos
        _mockLogs.Verify(l => l.InsertAsync(It.Is<PushNotificationLog>(log =>
            log.NotificationType == "LAST_HOUR_PICKS_REMINDER" &&
            log.UserId == 100 &&
            log.WeekId == 100 &&
            log.QuinielaId == 1)),
            Times.Once);

        _mockLogs.Verify(l => l.InsertAsync(It.Is<PushNotificationLog>(log =>
            log.NotificationType == "LAST_HOUR_PICKS_REMINDER" &&
            log.UserId == 101 &&
            log.WeekId == 100 &&
            log.QuinielaId == 1)),
            Times.Once);
    }

    [Fact]
    public async Task ProcessDailyReminders_OnMondayAfterNoon_WhenRegularSeasonFinished_ShouldSendLiguillaAnnouncement()
    {
        // ARRANGE: Lunes a las 13:00 CST (19:00 UTC)
        var mondayUtc = new DateTime(2026, 9, 28, 19, 0, 0, DateTimeKind.Utc);

        var quiniela = new Quiniela { Id = 1, LeagueId = 10, Name = "Quiniela Amigos", Active = true };
        var season = new Season { Id = 5, LeagueId = 10, Active = true };

        // Todas las semanas 1..17 están SCORED
        var weeks = Enumerable.Range(1, 17).Select(i => new Week
        {
            Id = 100 + i,
            SeasonId = 5,
            WeekNumber = i,
            Status = "SCORED",
            Active = true
        }).ToList();

        _mockQuinielas.Setup(q => q.GetAllAsync()).ReturnsAsync(new List<Quiniela> { quiniela });
        _mockSeasons.Setup(s => s.GetAllAsync()).ReturnsAsync(new List<Season> { season });
        _mockWeeks.Setup(w => w.GetBySeasonIdAsync(5)).ReturnsAsync(weeks);

        _mockLogs.Setup(l => l.HasLiguillaAnnouncementBeenSentAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockWebPush.Setup(w => w.SendNotificationToQuinielaAsync(1, It.IsAny<PushNotificationPayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        var service = new QuinielaReminderApplication(_mockUow.Object, _mockWebPush.Object, _mockLogger.Object);

        // ACT
        var count = await service.ProcessDailyRemindersAsync(mondayUtc);

        // ASSERT
        Assert.True(count > 0);
        _mockWebPush.Verify(w => w.SendNotificationToQuinielaAsync(
            1,
            It.Is<PushNotificationPayload>(p => p.Title.Contains("Pick'em") && p.Url == "/liguilla"),
            It.IsAny<CancellationToken>()),
            Times.Once);

        _mockLogs.Verify(l => l.InsertAsync(It.Is<PushNotificationLog>(log =>
            log.NotificationType == "LIGUILLA_ANNOUNCED" &&
            log.QuinielaId == 1)),
            Times.Once);
    }

    [Fact]
    public async Task ProcessDailyReminders_LigaFmfLiguilla_ShouldSendExactLigaMxCopy()
    {
        // ARRANGE: Lunes 13:00 CST con la fase regular de Liga MX ya calificada
        var mondayUtc = new DateTime(2026, 9, 28, 19, 0, 0, DateTimeKind.Utc);
        ArrangeFinishedRegularSeasonForLeague("FMF_LIGUILLA");

        _mockLogs.Setup(l => l.HasLiguillaAnnouncementBeenSentAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockWebPush.Setup(w => w.SendNotificationToQuinielaAsync(1, It.IsAny<PushNotificationPayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(4);

        var service = new QuinielaReminderApplication(_mockUow.Object, _mockWebPush.Object, _mockLogger.Object);

        // ACT
        await service.ProcessDailyRemindersAsync(mondayUtc);

        // ASSERT: el copy de Liga MX no cambia ni un carácter
        _mockWebPush.Verify(w => w.SendNotificationToQuinielaAsync(
            1,
            It.Is<PushNotificationPayload>(p =>
                p.Title == "🏆 ¡Llegó la Liguilla: Pick'em ya disponible!" &&
                p.Message == "¡Concluyó la fase regular! Ya puedes armar tu bracket en Pick'em Liguilla para Quiniela Amigos. ¡Haz tus pronósticos y gana la gloria!"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessDailyReminders_LigaNhlBestOf7_ShouldAnnouncePlayoffs()
    {
        // ARRANGE: Lunes 13:00 CST con la temporada regular ya calificada en una liga de playoffs NHL
        var mondayUtc = new DateTime(2026, 9, 28, 19, 0, 0, DateTimeKind.Utc);
        ArrangeFinishedRegularSeasonForLeague("NHL_BEST_OF_7");

        _mockLogs.Setup(l => l.HasLiguillaAnnouncementBeenSentAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockWebPush.Setup(w => w.SendNotificationToQuinielaAsync(1, It.IsAny<PushNotificationPayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(4);

        var service = new QuinielaReminderApplication(_mockUow.Object, _mockWebPush.Object, _mockLogger.Object);

        // ACT
        var count = await service.ProcessDailyRemindersAsync(mondayUtc);

        // ASSERT
        count.Should().Be(1);
        _mockWebPush.Verify(w => w.SendNotificationToQuinielaAsync(
            1,
            It.Is<PushNotificationPayload>(p => p.Title.Contains("Playoffs") && p.Url == "/liguilla"),
            It.IsAny<CancellationToken>()),
            Times.Once);
        _mockLogs.Verify(l => l.InsertAsync(It.Is<PushNotificationLog>(log => log.NotificationType == "LIGUILLA_ANNOUNCED")),
            Times.Once);
    }

    [Fact]
    public async Task ProcessDailyReminders_LigaNflReseededSingleGame_ShouldAnnouncePlayoffs()
    {
        // ARRANGE: Lunes 13:00 CST con la temporada regular ya calificada en una liga de playoffs NFL
        var mondayUtc = new DateTime(2026, 9, 28, 19, 0, 0, DateTimeKind.Utc);
        ArrangeFinishedRegularSeasonForLeague("NFL_RESEEDED_SINGLE_GAME");

        _mockLogs.Setup(l => l.HasLiguillaAnnouncementBeenSentAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockWebPush.Setup(w => w.SendNotificationToQuinielaAsync(1, It.IsAny<PushNotificationPayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(4);

        var service = new QuinielaReminderApplication(_mockUow.Object, _mockWebPush.Object, _mockLogger.Object);

        // ACT
        var count = await service.ProcessDailyRemindersAsync(mondayUtc);

        // ASSERT
        count.Should().Be(1);
        _mockWebPush.Verify(w => w.SendNotificationToQuinielaAsync(
            1,
            It.Is<PushNotificationPayload>(p => p.Title.Contains("Playoffs") && p.Message.Contains("temporada regular")),
            It.IsAny<CancellationToken>()),
            Times.Once);
        _mockLogs.Verify(l => l.InsertAsync(It.Is<PushNotificationLog>(log => log.NotificationType == "LIGUILLA_ANNOUNCED")),
            Times.Once);
    }

    [Fact]
    public async Task ProcessDailyReminders_LigaUefaTwoLeg_ShouldAnnounceOctavosDeFinal()
    {
        // ARRANGE: Lunes 13:00 CST con la fase regular ya calificada en una liga de eliminatoria a dos vueltas
        var mondayUtc = new DateTime(2026, 9, 28, 19, 0, 0, DateTimeKind.Utc);
        ArrangeFinishedRegularSeasonForLeague("UEFA_TWO_LEG");

        _mockLogs.Setup(l => l.HasLiguillaAnnouncementBeenSentAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockWebPush.Setup(w => w.SendNotificationToQuinielaAsync(1, It.IsAny<PushNotificationPayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(4);

        var service = new QuinielaReminderApplication(_mockUow.Object, _mockWebPush.Object, _mockLogger.Object);

        // ACT
        var count = await service.ProcessDailyRemindersAsync(mondayUtc);

        // ASSERT
        count.Should().Be(1);
        _mockWebPush.Verify(w => w.SendNotificationToQuinielaAsync(
            1,
            It.Is<PushNotificationPayload>(p => p.Title.Contains("Octavos de Final") && p.Url == "/liguilla"),
            It.IsAny<CancellationToken>()),
            Times.Once);
        _mockLogs.Verify(l => l.InsertAsync(It.Is<PushNotificationLog>(log => log.NotificationType == "LIGUILLA_ANNOUNCED")),
            Times.Once);
    }

    [Fact]
    public async Task ProcessDailyReminders_LigaSinFormatoDeEliminatoria_ShouldNotAnnounceAnything()
    {
        // ARRANGE: Lunes 13:00 CST, fase regular terminada, pero la liga no tiene formato de Pick'em
        var mondayUtc = new DateTime(2026, 9, 28, 19, 0, 0, DateTimeKind.Utc);
        ArrangeFinishedRegularSeasonForLeague(null);

        _mockLogs.Setup(l => l.HasLiguillaAnnouncementBeenSentAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockWebPush.Setup(w => w.SendNotificationToQuinielaAsync(1, It.IsAny<PushNotificationPayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(4);

        var service = new QuinielaReminderApplication(_mockUow.Object, _mockWebPush.Object, _mockLogger.Object);

        // ACT
        var count = await service.ProcessDailyRemindersAsync(mondayUtc);

        // ASSERT
        count.Should().Be(0);
        _mockWebPush.Verify(w => w.SendNotificationToQuinielaAsync(It.IsAny<int>(), It.IsAny<PushNotificationPayload>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockLogs.Verify(l => l.InsertAsync(It.Is<PushNotificationLog>(log => log.NotificationType == "LIGUILLA_ANNOUNCED")), Times.Never);
    }

    [Fact]
    public async Task ProcessDailyReminders_WhenLiguillaAnnouncementAlreadySent_ShouldNotSendDuplicate()
    {
        // ARRANGE: Lunes 13:00 CST, fase regular terminada, pero el anuncio ya se envió antes
        var mondayUtc = new DateTime(2026, 9, 28, 19, 0, 0, DateTimeKind.Utc);
        ArrangeFinishedRegularSeasonForLeague("FMF_LIGUILLA");

        _mockLogs.Setup(l => l.HasLiguillaAnnouncementBeenSentAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new QuinielaReminderApplication(_mockUow.Object, _mockWebPush.Object, _mockLogger.Object);

        // ACT
        var count = await service.ProcessDailyRemindersAsync(mondayUtc);

        // ASSERT
        count.Should().Be(0);
        _mockWebPush.Verify(w => w.SendNotificationToQuinielaAsync(It.IsAny<int>(), It.IsAny<PushNotificationPayload>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockLogs.Verify(l => l.InsertAsync(It.IsAny<PushNotificationLog>()), Times.Never);
    }
}
