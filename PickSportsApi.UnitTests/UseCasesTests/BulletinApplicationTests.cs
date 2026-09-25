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
    public async Task GetBulletinAsync_JornadaNoCalificada_NoAutoPublicaYEsPreliminar()
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
        result.isSuccess.Should().BeTrue();
        result.Data!.IsOfficial.Should().BeFalse();
        result.Data.PublishedAtUtc.Should().BeNull();
        result.Data.NextWeekInfo.Should().BeNull();
        _mockUow.Verify(u => u.WeeklyBulletins.InsertAsync(It.IsAny<WeeklyBulletin>()), Times.Never);
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
        result.isSuccess.Should().BeTrue();
        result.Data!.AdminAnnouncement.Should().Be("La jornada 8 cierra el jueves");
        _mockUow.Verify(u => u.WeeklyBulletins.InsertAsync(It.Is<WeeklyBulletin>(b =>
            b.QuinielaId == QuinielaId && b.WeekId == ActiveWeekId && b.IsPublished && b.Active)), Times.Once);
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
}
