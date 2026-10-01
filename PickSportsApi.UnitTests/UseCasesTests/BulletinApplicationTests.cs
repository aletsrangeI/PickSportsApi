using Domain.Entities;
using DTO.Bulletin;
using FluentAssertions;
using Interface.Persistence;
using Moq;
using UseCases.Bulletin;
using UseCases.Scoring;
using Xunit;
using MatchEntity = Domain.Entities.Match;

namespace PickSportsApi.UnitTests.UseCasesTests;

public class BulletinApplicationTests
{
    private const int QuinielaId = 1;
    private const int LeagueId = 5;
    private const int SeasonId = 50;
    private const int ScoredWeekId = 70;   // Jornada 7 (SCORED)
    private const int ActiveWeekId = 80;   // Jornada 8 (PUBLISHED)
    private const int AdminUserId = 999;

    private readonly Mock<IUnitOfWork> _mockUow = new();
    private readonly ScoringEngine _engine = new();
    private readonly BulletinApplication _sut;

    private readonly DateTime _nextWeekFirstGameUtc = new(2026, 10, 2, 1, 0, 0, DateTimeKind.Utc);
    private readonly DateTime _scoredAtUtc = new(2026, 9, 21, 4, 30, 0, DateTimeKind.Utc);

    public BulletinApplicationTests()
    {
        _sut = new BulletinApplication(_mockUow.Object, _engine);
    }

    #region Test Data

    private Quiniela BuildQuiniela() => new() { Id = QuinielaId, Name = "Quiniela Amigos", LeagueId = LeagueId };

    private List<Week> BuildWeeks()
    {
        return new List<Week>
        {
            new() { Id = 60, SeasonId = SeasonId, WeekNumber = 6, Name = "Jornada 6", Status = "SCORED", ScoredAt = _scoredAtUtc.AddDays(-7) },
            new() { Id = ScoredWeekId, SeasonId = SeasonId, WeekNumber = 7, Name = "Jornada 7", Status = "SCORED", ScoredAt = _scoredAtUtc, EndDate = _scoredAtUtc },
            new() { Id = ActiveWeekId, SeasonId = SeasonId, WeekNumber = 8, Name = "Jornada 8", Status = "PUBLISHED", FirstGameUtc = _nextWeekFirstGameUtc }
        };
    }

    private List<QuinielaMember> BuildMembers()
    {
        return new List<QuinielaMember>
        {
            new() { Id = 101, QuinielaId = QuinielaId, UserId = 11, Alias = "Alex", Role = "OWNER", User = new User { Id = 11, DisplayName = "Alejandro", AvatarUrl = "https://img/alex.png" } },
            new() { Id = 102, QuinielaId = QuinielaId, UserId = 12, Alias = "Fer", Role = "MEMBER", User = new User { Id = 12, DisplayName = "Fernando" } },
            new() { Id = 103, QuinielaId = QuinielaId, UserId = 13, Alias = "Dani", Role = "MEMBER", User = new User { Id = 13, DisplayName = "Daniela" } }
        };
    }

    private List<MatchEntity> BuildScoredWeekMatches()
    {
        return new List<MatchEntity>
        {
            new()
            {
                Id = 201, WeekId = ScoredWeekId, StatusState = "post",
                HomeScore = 3, AwayScore = 0, WinnerAbbr = "AME",
                HomeTeam = new Team { Abbreviation = "AME" }, AwayTeam = new Team { Abbreviation = "TOL" }
            },
            new()
            {
                Id = 202, WeekId = ScoredWeekId, StatusState = "post",
                HomeScore = 0, AwayScore = 0, WinnerAbbr = "EMPATE",
                HomeTeam = new Team { Abbreviation = "CRU" }, AwayTeam = new Team { Abbreviation = "PUM" }
            },
            new()
            {
                Id = 203, WeekId = ScoredWeekId, StatusState = "post",
                HomeScore = 2, AwayScore = 1, WinnerAbbr = "CHI",
                HomeTeam = new Team { Abbreviation = "CHI" }, AwayTeam = new Team { Abbreviation = "SAN" }
            }
        };
    }

    /// <summary>
    /// Picks tal como quedan persistidos después de que el worker de calificación (ScoreWeekAsync)
    /// evaluó la jornada: los flags IsHit/IsUpsetHit/IsHumillacion/IsSomnifero/IsEmpateFallido ya están fijados.
    /// </summary>
    private List<Pick> BuildScoredWeekPicks()
    {
        return new List<Pick>
        {
            // Alex: 3 aciertos, 1 sorpresa
            new() { Id = 1, QuinielaId = QuinielaId, MemberId = 101, MatchId = 201, PickAbbr = "AME", IsHit = true, IsUpsetHit = true },
            new() { Id = 2, QuinielaId = QuinielaId, MemberId = 101, MatchId = 202, PickAbbr = "EMPATE", IsHit = true },
            new() { Id = 3, QuinielaId = QuinielaId, MemberId = 101, MatchId = 203, PickAbbr = "CHI", IsHit = true },
            // Fer: 0 aciertos, humillado + somnífero + empate fallido
            new() { Id = 4, QuinielaId = QuinielaId, MemberId = 102, MatchId = 201, PickAbbr = "TOL", IsHit = false, IsHumillacion = true },
            new() { Id = 5, QuinielaId = QuinielaId, MemberId = 102, MatchId = 202, PickAbbr = "CRU", IsHit = false, IsSomnifero = true },
            new() { Id = 6, QuinielaId = QuinielaId, MemberId = 102, MatchId = 203, PickAbbr = "EMPATE", IsHit = false, IsEmpateFallido = true },
            // Dani: 1 acierto, somnífero
            new() { Id = 7, QuinielaId = QuinielaId, MemberId = 103, MatchId = 201, PickAbbr = "AME", IsHit = true },
            new() { Id = 8, QuinielaId = QuinielaId, MemberId = 103, MatchId = 202, PickAbbr = "PUM", IsHit = false, IsSomnifero = true },
            new() { Id = 9, QuinielaId = QuinielaId, MemberId = 103, MatchId = 203, PickAbbr = "SAN", IsHit = false }
        };
    }

    /// <summary>
    /// Histórico de la temporada para la Tabla General.
    /// Jornada 6: Fer 3 aciertos, Dani 1, Alex 0 → Tabla previa: 1° Fer, 2° Dani, 3° Alex.
    /// Acumulado a la Jornada 7: Alex 3 (1 sorpresa), Fer 3 (1 humillación), Dani 2 → 1° Alex, 2° Fer, 3° Dani.
    /// </summary>
    private List<Pick> BuildSeasonPicks()
    {
        var week6 = new Week { Id = 60, SeasonId = SeasonId, WeekNumber = 6 };
        var week7 = new Week { Id = ScoredWeekId, SeasonId = SeasonId, WeekNumber = 7 };

        Pick Week6Pick(int memberId, int matchId, bool hit) => new()
        {
            QuinielaId = QuinielaId, MemberId = memberId, MatchId = matchId, PickAbbr = "X", IsHit = hit,
            Match = new MatchEntity { Id = matchId, WeekId = week6.Id, Week = week6 }
        };

        var picks = new List<Pick>
        {
            Week6Pick(102, 601, true), Week6Pick(102, 602, true), Week6Pick(102, 603, true),
            Week6Pick(103, 601, true), Week6Pick(103, 602, false), Week6Pick(103, 603, false),
            Week6Pick(101, 601, false), Week6Pick(101, 602, false), Week6Pick(101, 603, false)
        };

        foreach (var pick in BuildScoredWeekPicks())
        {
            pick.Match = new MatchEntity { Id = pick.MatchId, WeekId = week7.Id, Week = week7 };
            picks.Add(pick);
        }

        return picks;
    }

    /// <summary>
    /// Configura el grafo completo de mocks para la jornada 7 calificada.
    /// </summary>
    private void SetupScoredWeekGraph(WeeklyBulletin? existingBulletin = null, bool setupSeason = true)
    {
        var weeks = BuildWeeks();
        var scoredWeek = weeks.Single(w => w.Id == ScoredWeekId);

        _mockUow.Setup(u => u.Quinielas.GetAsync(QuinielaId)).ReturnsAsync(BuildQuiniela());
        _mockUow.Setup(u => u.Weeks.GetAsync(ScoredWeekId)).ReturnsAsync(scoredWeek);
        _mockUow.Setup(u => u.QuinielaMembers.GetMembersAsync(QuinielaId)).ReturnsAsync(BuildMembers());
        _mockUow.Setup(u => u.Matches.GetByWeekIdAsync(ScoredWeekId)).ReturnsAsync(BuildScoredWeekMatches());
        _mockUow.Setup(u => u.Picks.GetAllPicksForWeekAsync(QuinielaId, ScoredWeekId)).ReturnsAsync(BuildScoredWeekPicks());
        _mockUow.Setup(u => u.Picks.GetAllPicksForQuinielaAsync(QuinielaId)).ReturnsAsync(BuildSeasonPicks());
        _mockUow.Setup(u => u.WeeklyAwards.GetAllAwardsForQuinielaAsync(QuinielaId)).ReturnsAsync(new List<WeeklyAward>());

        if (setupSeason)
        {
            _mockUow.Setup(u => u.Seasons.GetCurrentSeasonAsync(LeagueId))
                .ReturnsAsync(new Season { Id = SeasonId, LeagueId = LeagueId, Name = "Apertura 2026", IsCurrent = true });
            _mockUow.Setup(u => u.Weeks.GetBySeasonIdAsync(SeasonId)).ReturnsAsync(weeks);
        }

        var stored = existingBulletin;
        _mockUow.Setup(u => u.WeeklyBulletins.GetByQuinielaAndWeekAsync(QuinielaId, ScoredWeekId))
            .ReturnsAsync(() => stored);
        _mockUow.Setup(u => u.WeeklyBulletins.InsertAsync(It.IsAny<WeeklyBulletin>()))
            .Callback<WeeklyBulletin>(b => stored = b)
            .ReturnsAsync(true);
        _mockUow.Setup(u => u.WeeklyBulletins.UpdateAsync(It.IsAny<WeeklyBulletin>()))
            .Callback<WeeklyBulletin>(b => stored = b)
            .ReturnsAsync(true);
        _mockUow.Setup(u => u.Save(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    #endregion

    [Fact]
    public async Task GetBulletinAsync_QuinielaInexistente_RetornaFallo()
    {
        // Arrange
        _mockUow.Setup(u => u.Quinielas.GetAsync(999)).ReturnsAsync((Quiniela)null!);

        // Act
        var result = await _sut.GetBulletinAsync(999, null);

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Be("Quiniela no encontrada.");
    }

    [Fact]
    public async Task GetBulletinAsync_SinWeekId_ResuelveUltimaJornadaScored_Y_AutoPublicaEdicion()
    {
        // Arrange
        SetupScoredWeekGraph();

        // Act
        var result = await _sut.GetBulletinAsync(QuinielaId, null);

        // Assert: resuelve la Jornada 7 (última SCORED) y no la 6 ni la 8
        result.isSuccess.Should().BeTrue();
        result.Data!.WeekId.Should().Be(ScoredWeekId);
        result.Data.WeekNumber.Should().Be(7);
        result.Data.IsOfficial.Should().BeTrue();
        result.Data.WeekStatus.Should().Be("SCORED");

        // Assert: auto-publicación del registro WeeklyBulletin
        _mockUow.Verify(u => u.WeeklyBulletins.InsertAsync(It.Is<WeeklyBulletin>(b =>
            b.QuinielaId == QuinielaId && b.WeekId == ScoredWeekId && b.IsPublished && b.Active)), Times.Once);
        result.Data.PublishedAtUtc.Should().Be(_scoredAtUtc);
    }

    [Fact]
    public async Task GetBulletinAsync_JornadaScored_MapeaPodioConDesempateEnCascada()
    {
        // Arrange
        SetupScoredWeekGraph();

        // Act
        var result = await _sut.GetBulletinAsync(QuinielaId, ScoredWeekId);

        // Assert: podio 1° Alex (3 aciertos), 2° Dani (1), 3° Fer (0)
        result.isSuccess.Should().BeTrue();
        var podium = result.Data!.Podium;
        podium.Should().HaveCount(3);
        podium[0].Position.Should().Be(1);
        podium[0].Alias.Should().Be("Alex");
        podium[0].Hits.Should().Be(3);
        podium[0].UpsetHits.Should().Be(1);
        podium[0].AvatarUrl.Should().Be("https://img/alex.png");
        podium[1].Alias.Should().Be("Dani");
        podium[2].Alias.Should().Be("Fer");
        podium[2].Humillaciones.Should().Be(1);
    }

    [Fact]
    public async Task GetBulletinAsync_JornadaScored_MapeaSalonDeLaGloriaYSalaDeLaInfamia()
    {
        // Arrange
        SetupScoredWeekGraph();

        // Act
        var result = await _sut.GetBulletinAsync(QuinielaId, ScoredWeekId);

        // Assert: Salón de la Gloria
        var awards = result.Data!.Awards;
        awards.Mvp.Should().ContainSingle().Which.Alias.Should().Be("Alex");
        awards.SurpriseKing.Should().ContainSingle().Which.Alias.Should().Be("Alex");
        awards.RompeQuinielas.Should().NotBeNull();
        awards.RompeQuinielas!.MatchLabel.Should().Be("CRU vs PUM");
        awards.RompeQuinielas.WinnerAbbr.Should().Be("EMPATE");
        awards.RompeQuinielas.AccuracyPct.Should().Be(33.3m);
        awards.RompeQuinielas.CorrectPicks.Should().Be(1);
        awards.RompeQuinielas.TotalPicks.Should().Be(3);

        // Assert: Sala de la Infamia
        awards.Humillado.Should().ContainSingle().Which.Alias.Should().Be("Fer");
        awards.EmpateFallido.Should().ContainSingle().Which.Alias.Should().Be("Fer");
        awards.Somnifero.Should().HaveCount(2);
        awards.Somnifero.Select(s => s.Alias).Should().BeEquivalentTo(new[] { "Fer", "Dani" });
    }

    [Fact]
    public async Task GetBulletinAsync_JornadaScored_IncluyeInfoDeLaSiguienteJornada()
    {
        // Arrange
        SetupScoredWeekGraph();

        // Act
        var result = await _sut.GetBulletinAsync(QuinielaId, ScoredWeekId);

        // Assert
        var next = result.Data!.NextWeekInfo;
        next.Should().NotBeNull();
        next!.WeekId.Should().Be(ActiveWeekId);
        next.WeekNumber.Should().Be(8);
        next.FirstGameUtc.Should().Be(_nextWeekFirstGameUtc);
    }

    [Fact]
    public async Task GetBulletinAsync_JornadaNoCalificada_RetornaFallo()
    {
        // Arrange
        var weeks = BuildWeeks();
        var activeWeek = weeks.Single(w => w.Id == ActiveWeekId);
        _mockUow.Setup(u => u.Quinielas.GetAsync(QuinielaId)).ReturnsAsync(BuildQuiniela());
        _mockUow.Setup(u => u.Weeks.GetAsync(ActiveWeekId)).ReturnsAsync(activeWeek);
        _mockUow.Setup(u => u.Weeks.GetBySeasonIdAsync(SeasonId)).ReturnsAsync(weeks);
        _mockUow.Setup(u => u.QuinielaMembers.GetMembersAsync(QuinielaId)).ReturnsAsync(BuildMembers());
        _mockUow.Setup(u => u.Matches.GetByWeekIdAsync(ActiveWeekId)).ReturnsAsync(new List<MatchEntity>());
        _mockUow.Setup(u => u.Picks.GetAllPicksForWeekAsync(QuinielaId, ActiveWeekId)).ReturnsAsync(new List<Pick>());
        _mockUow.Setup(u => u.WeeklyBulletins.GetByQuinielaAndWeekAsync(QuinielaId, ActiveWeekId))
            .ReturnsAsync((WeeklyBulletin?)null);

        // Act
        var result = await _sut.GetBulletinAsync(QuinielaId, ActiveWeekId);

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Be("El boletín solo está disponible para jornadas concluidas y calificadas.");
        result.Data.Should().BeNull();
        _mockUow.Verify(u => u.WeeklyBulletins.InsertAsync(It.IsAny<WeeklyBulletin>()), Times.Never);
    }

    [Theory]
    [InlineData("LOCKED")]
    [InlineData("PUBLISHED")]
    public async Task GetBulletinAsync_JornadaNoCalificadaPorEstado_RetornaFallo(string status)
    {
        // Arrange
        var week = new Week
        {
            Id = ActiveWeekId,
            SeasonId = SeasonId,
            WeekNumber = 8,
            Name = "Jornada 8",
            Status = status
        };
        _mockUow.Setup(u => u.Quinielas.GetAsync(QuinielaId)).ReturnsAsync(BuildQuiniela());
        _mockUow.Setup(u => u.Weeks.GetAsync(ActiveWeekId)).ReturnsAsync(week);

        // Act
        var result = await _sut.GetBulletinAsync(QuinielaId, ActiveWeekId);

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Be("El boletín solo está disponible para jornadas concluidas y calificadas.");
        result.Data.Should().BeNull();
    }

    [Fact]
    public async Task GetBulletinAsync_BoletinExistente_ExponeAnuncioDelAdministrador()
    {
        // Arrange
        var bulletin = new WeeklyBulletin
        {
            Id = 500,
            QuinielaId = QuinielaId,
            WeekId = ScoredWeekId,
            AdminAnnouncement = "Recordatorio: Se adelanta el partido de Cruz Azul al jueves 7:00 PM",
            PublishedAtUtc = _scoredAtUtc,
            IsPublished = true,
            Active = true
        };
        SetupScoredWeekGraph(bulletin);

        // Act
        var result = await _sut.GetBulletinAsync(QuinielaId, ScoredWeekId);

        // Assert
        result.isSuccess.Should().BeTrue();
        result.Data!.AdminAnnouncement.Should().Be("Recordatorio: Se adelanta el partido de Cruz Azul al jueves 7:00 PM");
        _mockUow.Verify(u => u.WeeklyBulletins.InsertAsync(It.IsAny<WeeklyBulletin>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAnnouncementAsync_UsuarioSinRolAdmin_RetornaFallo()
    {
        // Arrange
        _mockUow.Setup(u => u.Quinielas.GetAsync(QuinielaId)).ReturnsAsync(BuildQuiniela());
        _mockUow.Setup(u => u.QuinielaMembers.GetMembershipAsync(QuinielaId, 12))
            .ReturnsAsync(new QuinielaMember { Id = 102, Alias = "Fer", Role = "MEMBER" });

        // Act
        var result = await _sut.UpdateAnnouncementAsync(QuinielaId, ScoredWeekId, 12,
            new UpdateAnnouncementDto { Announcement = "Mensaje no autorizado" });

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Be("Solo los administradores u owners pueden publicar anuncios en el boletín.");
        _mockUow.Verify(u => u.WeeklyBulletins.InsertAsync(It.IsAny<WeeklyBulletin>()), Times.Never);
        _mockUow.Verify(u => u.WeeklyBulletins.UpdateAsync(It.IsAny<WeeklyBulletin>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAnnouncementAsync_AdminEditaBoletínExistente_PersisteYRetornaEdicion()
    {
        // Arrange
        var bulletin = new WeeklyBulletin
        {
            Id = 500, QuinielaId = QuinielaId, WeekId = ScoredWeekId,
            AdminAnnouncement = "Anuncio viejo", PublishedAtUtc = _scoredAtUtc, IsPublished = true, Active = true
        };
        SetupScoredWeekGraph(bulletin);
        _mockUow.Setup(u => u.QuinielaMembers.GetMembershipAsync(QuinielaId, AdminUserId))
            .ReturnsAsync(new QuinielaMember { Id = 101, Alias = "Alex", Role = "OWNER" });

        // Act
        var result = await _sut.UpdateAnnouncementAsync(QuinielaId, ScoredWeekId, AdminUserId,
            new UpdateAnnouncementDto { Announcement = "  Recordatorio: Se adelanta el partido de Cruz Azul al jueves 7:00 PM  " });

        // Assert
        result.isSuccess.Should().BeTrue();
        result.Message.Should().Be("Anuncio del administrador actualizado correctamente.");
        result.Data!.AdminAnnouncement.Should().Be("Recordatorio: Se adelanta el partido de Cruz Azul al jueves 7:00 PM");
        result.Data.WeekNumber.Should().Be(7);
        _mockUow.Verify(u => u.WeeklyBulletins.UpdateAsync(It.Is<WeeklyBulletin>(b =>
            b.Id == 500 && b.IsPublished)), Times.Once);
        _mockUow.Verify(u => u.WeeklyBulletins.InsertAsync(It.IsAny<WeeklyBulletin>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAnnouncementAsync_AdminSobreJornadaSinBoletin_CreaRegistroPublicado()
    {
        // Arrange: anuncio para la Jornada 8 (PUBLISHED, aún sin boletín)
        var weeks = BuildWeeks();
        var activeWeek = weeks.Single(w => w.Id == ActiveWeekId);
        _mockUow.Setup(u => u.Quinielas.GetAsync(QuinielaId)).ReturnsAsync(BuildQuiniela());
        _mockUow.Setup(u => u.Weeks.GetAsync(ActiveWeekId)).ReturnsAsync(activeWeek);
        _mockUow.Setup(u => u.Weeks.GetBySeasonIdAsync(SeasonId)).ReturnsAsync(weeks);
        _mockUow.Setup(u => u.QuinielaMembers.GetMembersAsync(QuinielaId)).ReturnsAsync(BuildMembers());
        _mockUow.Setup(u => u.Matches.GetByWeekIdAsync(ActiveWeekId)).ReturnsAsync(new List<MatchEntity>());
        _mockUow.Setup(u => u.Picks.GetAllPicksForWeekAsync(QuinielaId, ActiveWeekId)).ReturnsAsync(new List<Pick>());
        _mockUow.Setup(u => u.QuinielaMembers.GetMembershipAsync(QuinielaId, AdminUserId))
            .ReturnsAsync(new QuinielaMember { Id = 101, Alias = "Alex", Role = "ADMIN" });

        WeeklyBulletin? stored = null;
        _mockUow.Setup(u => u.WeeklyBulletins.GetByQuinielaAndWeekAsync(QuinielaId, ActiveWeekId))
            .ReturnsAsync(() => stored);
        _mockUow.Setup(u => u.WeeklyBulletins.InsertAsync(It.IsAny<WeeklyBulletin>()))
            .Callback<WeeklyBulletin>(b => stored = b)
            .ReturnsAsync(true);

        // Act
        var result = await _sut.UpdateAnnouncementAsync(QuinielaId, ActiveWeekId, AdminUserId,
            new UpdateAnnouncementDto { Announcement = "La jornada 8 cierra el jueves" });

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Be("El boletín solo está disponible para jornadas concluidas y calificadas.");
        _mockUow.Verify(u => u.WeeklyBulletins.InsertAsync(It.IsAny<WeeklyBulletin>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAnnouncementAsync_AnuncioQueExcedeMaximo_RetornaFallo()
    {
        // Arrange
        SetupScoredWeekGraph();
        _mockUow.Setup(u => u.QuinielaMembers.GetMembershipAsync(QuinielaId, AdminUserId))
            .ReturnsAsync(new QuinielaMember { Id = 101, Alias = "Alex", Role = "OWNER" });
        var longAnnouncement = new string('x', 2001);

        // Act
        var result = await _sut.UpdateAnnouncementAsync(QuinielaId, ScoredWeekId, AdminUserId,
            new UpdateAnnouncementDto { Announcement = longAnnouncement });

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("2000");
        _mockUow.Verify(u => u.WeeklyBulletins.UpdateAsync(It.IsAny<WeeklyBulletin>()), Times.Never);
        _mockUow.Verify(u => u.WeeklyBulletins.InsertAsync(It.IsAny<WeeklyBulletin>()), Times.Never);
    }

    #region Radar de la Tabla (SPEC-019)

    [Fact]
    public async Task GetBulletinAsync_RadarDeLaTabla_DetectaTrepaCerrosYCaidaLibre()
    {
        // Arrange
        SetupScoredWeekGraph();

        // Act
        var result = await _sut.GetBulletinAsync(QuinielaId, ScoredWeekId);

        // Assert: Alex pasa de 3° a 1° (+2)
        var radar = result.Data!.Radar;
        radar.Should().NotBeNull();
        radar!.Climber.Should().NotBeNull();
        radar.Climber!.Alias.Should().Be("Alex");
        radar.Climber.PreviousRank.Should().Be(3);
        radar.Climber.CurrentRank.Should().Be(1);
        radar.Climber.PositionsDelta.Should().Be(2);
        radar.Climber.WeeklyHits.Should().Be(3);
        radar.Climber.TiedCount.Should().Be(0);
        radar.Climber.AvatarUrl.Should().Be("https://img/alex.png");

        // Assert: Fer y Dani bajan 1 lugar; cae Fer por peor posición en la Tabla Semanal
        radar.Faller.Should().NotBeNull();
        radar.Faller!.Alias.Should().Be("Fer");
        radar.Faller.PreviousRank.Should().Be(1);
        radar.Faller.CurrentRank.Should().Be(2);
        radar.Faller.PositionsDelta.Should().Be(-1);
        radar.Faller.TiedCount.Should().Be(1);
    }

    [Fact]
    public async Task GetBulletinAsync_RadarDeLaTabla_EmpateEnDeltaSeResuelvePorTablaSemanal()
    {
        // Arrange
        // Jornada 6: Alex 1, Zoe 1, Dani 0, Fer 0 → 1° Alex, 2° Zoe, 3° Dani, 4° Fer.
        // Jornada 7: Dani 3, Fer 2, Alex 0, Zoe 0 → acumulado 1° Dani, 2° Fer, 3° Alex, 4° Zoe.
        // Dani y Fer suben +2 (gana Dani, mejor en la semana); Alex y Zoe bajan -2 (cae Zoe, peor en la semana).
        SetupScoredWeekGraph();
        var members = BuildMembers();
        members.Add(new QuinielaMember { Id = 104, QuinielaId = QuinielaId, UserId = 14, Alias = "Zoe", User = new User { Id = 14 } });
        _mockUow.Setup(u => u.QuinielaMembers.GetMembersAsync(QuinielaId)).ReturnsAsync(members);

        var week6 = new Week { Id = 60, SeasonId = SeasonId, WeekNumber = 6 };
        var week7 = new Week { Id = ScoredWeekId, SeasonId = SeasonId, WeekNumber = 7 };
        Pick NewPick(Week week, int memberId, int matchId, bool hit) => new()
        {
            QuinielaId = QuinielaId, MemberId = memberId, MatchId = matchId, PickAbbr = "X", IsHit = hit,
            Match = new MatchEntity { Id = matchId, WeekId = week.Id, Week = week }
        };

        var week7Picks = new List<Pick>
        {
            NewPick(week7, 103, 201, true), NewPick(week7, 103, 202, true), NewPick(week7, 103, 203, true),
            NewPick(week7, 102, 201, true), NewPick(week7, 102, 202, true), NewPick(week7, 102, 203, false),
            NewPick(week7, 101, 201, false), NewPick(week7, 101, 202, false), NewPick(week7, 101, 203, false),
            NewPick(week7, 104, 201, false), NewPick(week7, 104, 202, false), NewPick(week7, 104, 203, false)
        };
        var seasonPicks = new List<Pick> { NewPick(week6, 101, 601, true), NewPick(week6, 104, 601, true) };
        seasonPicks.AddRange(week7Picks);

        _mockUow.Setup(u => u.Picks.GetAllPicksForWeekAsync(QuinielaId, ScoredWeekId)).ReturnsAsync(week7Picks);
        _mockUow.Setup(u => u.Picks.GetAllPicksForQuinielaAsync(QuinielaId)).ReturnsAsync(seasonPicks);

        // Act
        var result = await _sut.GetBulletinAsync(QuinielaId, ScoredWeekId);

        // Assert
        var radar = result.Data!.Radar!;
        radar.Climber!.Alias.Should().Be("Dani");
        radar.Climber.PositionsDelta.Should().Be(2);
        radar.Climber.TiedCount.Should().Be(1);
        radar.Faller!.Alias.Should().Be("Zoe");
        radar.Faller.PositionsDelta.Should().Be(-2);
        radar.Faller.TiedCount.Should().Be(1);
    }

    [Fact]
    public async Task GetBulletinAsync_RadarDeLaTabla_SinMovimientos_NoAsignaTrepaCerrosNiCaidaLibre()
    {
        // Arrange: sin historial previo la tabla anterior queda por orden alfabético (Alex, Dani, Fer),
        // igual que la tabla actual (Alex 3, Dani 1, Fer 0).
        SetupScoredWeekGraph();
        var week7 = new Week { Id = ScoredWeekId, SeasonId = SeasonId, WeekNumber = 7 };
        var onlyCurrentWeek = BuildScoredWeekPicks();
        onlyCurrentWeek.ForEach(p => p.Match = new MatchEntity { Id = p.MatchId, WeekId = week7.Id, Week = week7 });
        _mockUow.Setup(u => u.Picks.GetAllPicksForQuinielaAsync(QuinielaId)).ReturnsAsync(onlyCurrentWeek);

        // Act
        var result = await _sut.GetBulletinAsync(QuinielaId, ScoredWeekId);

        // Assert
        var radar = result.Data!.Radar;
        radar.Should().NotBeNull();
        radar!.Climber.Should().BeNull();
        radar.Faller.Should().BeNull();
    }

    [Fact]
    public async Task GetBulletinAsync_RadarDeLaTabla_PrimeraJornada_RetornaRadarNulo()
    {
        // Arrange
        SetupScoredWeekGraph();
        var firstWeek = new Week { Id = ScoredWeekId, SeasonId = SeasonId, WeekNumber = 1, Name = "Jornada 1", Status = "SCORED" };
        _mockUow.Setup(u => u.Weeks.GetAsync(ScoredWeekId)).ReturnsAsync(firstWeek);
        var picks = BuildScoredWeekPicks();
        picks.ForEach(p => p.Match = new MatchEntity { Id = p.MatchId, WeekId = firstWeek.Id, Week = firstWeek });
        _mockUow.Setup(u => u.Picks.GetAllPicksForQuinielaAsync(QuinielaId)).ReturnsAsync(picks);

        // Act
        var result = await _sut.GetBulletinAsync(QuinielaId, ScoredWeekId);

        // Assert
        result.isSuccess.Should().BeTrue();
        result.Data!.Radar.Should().BeNull();
    }

    [Fact]
    public async Task GetBulletinAsync_RadarDeLaTabla_IgnoraPicksDeOtraTemporada()
    {
        // Arrange: Dani tiene 20 aciertos en la Jornada 6 de la temporada anterior
        SetupScoredWeekGraph();
        var previousSeasonWeek = new Week { Id = 999, SeasonId = SeasonId - 1, WeekNumber = 6 };
        var picks = BuildSeasonPicks();
        for (int i = 0; i < 20; i++)
        {
            picks.Add(new Pick
            {
                QuinielaId = QuinielaId, MemberId = 103, MatchId = 9000 + i, PickAbbr = "X", IsHit = true,
                Match = new MatchEntity { Id = 9000 + i, WeekId = previousSeasonWeek.Id, Week = previousSeasonWeek }
            });
        }
        _mockUow.Setup(u => u.Picks.GetAllPicksForQuinielaAsync(QuinielaId)).ReturnsAsync(picks);

        // Act
        var result = await _sut.GetBulletinAsync(QuinielaId, ScoredWeekId);

        // Assert: mismo resultado que sin la temporada anterior
        var radar = result.Data!.Radar!;
        radar.Climber!.Alias.Should().Be("Alex");
        radar.Climber.PositionsDelta.Should().Be(2);
        radar.Faller!.Alias.Should().Be("Fer");
    }

    #endregion

    #region Pulso de la Jornada (SPEC-019)

    [Fact]
    public async Task GetBulletinAsync_PulsoDeLaJornada_CalculaEfectividadYConsentido()
    {
        // Arrange
        SetupScoredWeekGraph();

        // Act
        var result = await _sut.GetBulletinAsync(QuinielaId, ScoredWeekId);

        // Assert: 4 aciertos de 9 picks; AME elegido por 2 de 3 en AME vs TOL y ganó
        var pulse = result.Data!.Pulse;
        pulse.Should().NotBeNull();
        pulse!.TotalHits.Should().Be(4);
        pulse.TotalPicks.Should().Be(9);
        pulse.CommunityAccuracyPct.Should().Be(44.4m);
        pulse.Favorite.Should().NotBeNull();
        pulse.Favorite!.TeamAbbr.Should().Be("AME");
        pulse.Favorite.MatchLabel.Should().Be("AME vs TOL");
        pulse.Favorite.PickPct.Should().Be(66.7m);
        pulse.Favorite.PickCount.Should().Be(2);
        pulse.Favorite.TotalPicks.Should().Be(3);
        pulse.Favorite.Won.Should().BeTrue();
    }

    [Fact]
    public async Task GetBulletinAsync_PulsoDeLaJornada_ConsentidoExcluyeEmpateYResuelveEmpatePorPartidoMasTemprano()
    {
        // Arrange
        SetupScoredWeekGraph();
        var kickoff = new DateTime(2026, 9, 19, 0, 0, 0, DateTimeKind.Utc);
        var matches = new List<MatchEntity>
        {
            new() { Id = 203, WeekId = ScoredWeekId, StatusState = "post", WinnerAbbr = "SAN", DateUtc = kickoff.AddHours(4),
                    HomeTeam = new Team { Abbreviation = "CHI", Name = "Guadalajara", DisplayName = "Chivas" }, AwayTeam = new Team { Abbreviation = "SAN" } },
            new() { Id = 201, WeekId = ScoredWeekId, StatusState = "post", WinnerAbbr = "TOL", DateUtc = kickoff,
                    HomeTeam = new Team { Abbreviation = "AME", Name = "America", DisplayName = "América", LogoUrl = "https://img/ame.png" }, AwayTeam = new Team { Abbreviation = "TOL" } },
            new() { Id = 202, WeekId = ScoredWeekId, StatusState = "post", WinnerAbbr = "EMPATE", DateUtc = kickoff.AddHours(2),
                    HomeTeam = new Team { Abbreviation = "CRU" }, AwayTeam = new Team { Abbreviation = "PUM" } }
        };
        var picks = new List<Pick>
        {
            new() { MemberId = 101, MatchId = 201, PickAbbr = "AME", IsHit = false },
            new() { MemberId = 102, MatchId = 201, PickAbbr = "AME", IsHit = false },
            new() { MemberId = 103, MatchId = 201, PickAbbr = "TOL", IsHit = true },
            new() { MemberId = 101, MatchId = 202, PickAbbr = "EMPATE", IsHit = true },
            new() { MemberId = 102, MatchId = 202, PickAbbr = "EMPATE", IsHit = true },
            new() { MemberId = 103, MatchId = 202, PickAbbr = "EMPATE", IsHit = true },
            new() { MemberId = 101, MatchId = 203, PickAbbr = "CHI", IsHit = false },
            new() { MemberId = 102, MatchId = 203, PickAbbr = "CHI", IsHit = false },
            new() { MemberId = 103, MatchId = 203, PickAbbr = "SAN", IsHit = true }
        };
        _mockUow.Setup(u => u.Matches.GetByWeekIdAsync(ScoredWeekId)).ReturnsAsync(matches);
        _mockUow.Setup(u => u.Picks.GetAllPicksForWeekAsync(QuinielaId, ScoredWeekId)).ReturnsAsync(picks);

        // Act
        var result = await _sut.GetBulletinAsync(QuinielaId, ScoredWeekId);

        // Assert: el 100% a EMPATE no cuenta; AME y CHI empatan en 66.7% y gana el partido más temprano (AME)
        var favorite = result.Data!.Pulse!.Favorite!;
        favorite.TeamAbbr.Should().Be("AME");
        favorite.TeamName.Should().Be("América");
        favorite.TeamLogoUrl.Should().Be("https://img/ame.png");
        favorite.PickPct.Should().Be(66.7m);
        favorite.Won.Should().BeFalse();
    }

    [Fact]
    public async Task GetBulletinAsync_PulsoDeLaJornada_SinPicks_RetornaPulsoNulo()
    {
        // Arrange
        SetupScoredWeekGraph();
        _mockUow.Setup(u => u.Picks.GetAllPicksForWeekAsync(QuinielaId, ScoredWeekId)).ReturnsAsync(new List<Pick>());

        // Act
        var result = await _sut.GetBulletinAsync(QuinielaId, ScoredWeekId);

        // Assert
        result.isSuccess.Should().BeTrue();
        result.Data!.Pulse.Should().BeNull();
    }

    #endregion
}
