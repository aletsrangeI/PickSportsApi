using Domain.Entities;
using DTO.Pick;
using Interface.Persistence;
using Moq;
using UseCases.Picks;
using Validator.Pick;
using MatchEntity = Domain.Entities.Match;

namespace PickSportsApi.UnitTests.UseCasesTests;

public class PickApplicationTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly SubmitPickDtoValidator _submitValidator;
    private readonly PickApplication _pickApplication;

    public PickApplicationTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        // SubmitPickAsync consulta la temporada vigente cuando la quiniela no tiene SeasonId (asignación perezosa, SPEC-020).
        _unitOfWorkMock.SetupGet(u => u.Seasons).Returns(new Mock<ISeasonRepository>().Object);
        _submitValidator = new SubmitPickDtoValidator();
        _pickApplication = new PickApplication(_unitOfWorkMock.Object, _submitValidator);
    }

    [Fact]
    public async Task SubmitPickAsync_BeforeLock_SuccessfulUpsertAndAuditLog()
    {
        // Arrange (Escenario 1)
        int quinielaId = 1;
        int userId = 10;
        int matchId = 100;
        int weekId = 5;

        var membership = new QuinielaMember
        {
            Id = 42,
            QuinielaId = quinielaId,
            UserId = userId,
            Alias = "ElCrack",
            Role = "MEMBER"
        };

        var quiniela = new Quiniela
        {
            Id = quinielaId,
            LeagueId = 1,
            Active = true
        };

        var sport = new Sport { Id = 1, Name = "Fútbol", HasDraw = true };
        var league = new League { Id = 1, SportId = 1, Sport = sport, Active = true };

        var homeTeam = new Team { Id = 1, Abbreviation = "AME", Name = "América" };
        var awayTeam = new Team { Id = 2, Abbreviation = "GDL", Name = "Guadalajara" };

        var match = new MatchEntity
        {
            Id = matchId,
            WeekId = weekId,
            DateUtc = DateTime.UtcNow.AddHours(2),
            StatusState = "pre",
            HomeTeamId = 1,
            AwayTeamId = 2,
            HomeTeam = homeTeam,
            AwayTeam = awayTeam
        };

        var week = new Week
        {
            Id = weekId,
            WeekNumber = 1,
            Name = "Jornada 1",
            Status = "PUBLISHED",
            FirstGameUtc = DateTime.UtcNow.AddHours(2)
        };

        _unitOfWorkMock.Setup(u => u.QuinielaMembers.GetMembershipAsync(quinielaId, userId))
            .ReturnsAsync(membership);
        _unitOfWorkMock.Setup(u => u.Quinielas.GetAsync(quinielaId))
            .ReturnsAsync(quiniela);
        _unitOfWorkMock.Setup(u => u.Matches.GetAsync(matchId))
            .ReturnsAsync(match);
        _unitOfWorkMock.Setup(u => u.Weeks.GetAsync(weekId))
            .ReturnsAsync(week);
        _unitOfWorkMock.Setup(u => u.Leagues.GetAsync(1))
            .ReturnsAsync(league);
        _unitOfWorkMock.Setup(u => u.Sports.GetAsync(1))
            .ReturnsAsync(sport);
        _unitOfWorkMock.Setup(u => u.Picks.GetPickAsync(quinielaId, membership.Id, matchId))
            .ReturnsAsync((Pick?)null);

        Pick? savedPick = null;
        _unitOfWorkMock.Setup(u => u.Picks.InsertAsync(It.IsAny<Pick>()))
            .Callback<Pick>(p => { savedPick = p; p.Id = 1; })
            .ReturnsAsync(true);

        PickAuditLog? savedLog = null;
        _unitOfWorkMock.Setup(u => u.PickAuditLogs.InsertAsync(It.IsAny<PickAuditLog>()))
            .Callback<PickAuditLog>(l => { savedLog = l; })
            .ReturnsAsync(true);

        var request = new SubmitPickRequestDto
        {
            MatchId = matchId,
            PickAbbr = "AME"
        };

        // Act
        var result = await _pickApplication.SubmitPickAsync(quinielaId, userId, request);

        // Assert
        Assert.True(result.isSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal("AME", result.Data.PickAbbr);
        Assert.False(result.Data.IsAutoFilled);

        Assert.NotNull(savedPick);
        Assert.Equal("AME", savedPick.PickAbbr);

        Assert.NotNull(savedLog);
        Assert.Equal("MANUAL", savedLog.Source);
        Assert.Equal("created", savedLog.Action);
        Assert.Equal("AME", savedLog.PickAbbr);
        _unitOfWorkMock.Verify(u => u.Save(default), Times.Once);
    }

    [Fact]
    public async Task SubmitPickAsync_WhenWeekIsLocked_RejectsWithLockedError()
    {
        // Arrange (Escenario 2a)
        int quinielaId = 1;
        int userId = 10;
        int matchId = 100;
        int weekId = 5;

        var membership = new QuinielaMember { Id = 42, QuinielaId = quinielaId, UserId = userId };
        var quiniela = new Quiniela { Id = quinielaId, Active = true };
        var match = new MatchEntity { Id = matchId, WeekId = weekId, StatusState = "pre" };
        var week = new Week { Id = weekId, Status = "LOCKED" };

        _unitOfWorkMock.Setup(u => u.QuinielaMembers.GetMembershipAsync(quinielaId, userId)).ReturnsAsync(membership);
        _unitOfWorkMock.Setup(u => u.Quinielas.GetAsync(quinielaId)).ReturnsAsync(quiniela);
        _unitOfWorkMock.Setup(u => u.Matches.GetAsync(matchId)).ReturnsAsync(match);
        _unitOfWorkMock.Setup(u => u.Weeks.GetAsync(weekId)).ReturnsAsync(week);
        _unitOfWorkMock.Setup(u => u.Leagues.GetAsync(It.IsAny<int>())).ReturnsAsync(new League { Id = 1, WeeksCount = 17 });

        var request = new SubmitPickRequestDto { MatchId = matchId, PickAbbr = "AME" };

        // Act
        var result = await _pickApplication.SubmitPickAsync(quinielaId, userId, request);

        // Assert
        Assert.False(result.isSuccess);
        Assert.Contains("bloqueada", result.Message, StringComparison.OrdinalIgnoreCase);
        _unitOfWorkMock.Verify(u => u.Picks.InsertAsync(It.IsAny<Pick>()), Times.Never);
    }

    [Fact]
    public async Task SubmitPickAsync_WhenFirstGameUtcPassed_AutoLocksWeekAndRejectsPick()
    {
        // Arrange (Escenario 2b - Cierre automático al arrancar el primer partido)
        int quinielaId = 1;
        int userId = 10;
        int matchId = 100;
        int weekId = 5;

        var membership = new QuinielaMember { Id = 42, QuinielaId = quinielaId, UserId = userId };
        var quiniela = new Quiniela { Id = quinielaId, Active = true };
        var match = new MatchEntity { Id = matchId, WeekId = weekId, StatusState = "pre" };
        var week = new Week
        {
            Id = weekId,
            Status = "PUBLISHED",
            FirstGameUtc = DateTime.UtcNow.AddMinutes(-5) // Ya arrancó hace 5 minutos
        };

        _unitOfWorkMock.Setup(u => u.QuinielaMembers.GetMembershipAsync(quinielaId, userId)).ReturnsAsync(membership);
        _unitOfWorkMock.Setup(u => u.Quinielas.GetAsync(quinielaId)).ReturnsAsync(quiniela);
        _unitOfWorkMock.Setup(u => u.Matches.GetAsync(matchId)).ReturnsAsync(match);
        _unitOfWorkMock.Setup(u => u.Weeks.GetAsync(weekId)).ReturnsAsync(week);
        _unitOfWorkMock.Setup(u => u.Leagues.GetAsync(It.IsAny<int>())).ReturnsAsync(new League { Id = 1, WeeksCount = 17 });

        var request = new SubmitPickRequestDto { MatchId = matchId, PickAbbr = "AME" };

        // Act
        var result = await _pickApplication.SubmitPickAsync(quinielaId, userId, request);

        // Assert
        Assert.False(result.isSuccess);
        Assert.Equal("LOCKED", week.Status);
        Assert.NotNull(week.LockedAt);
        Assert.Contains("bloqueada", result.Message, StringComparison.OrdinalIgnoreCase);
        _unitOfWorkMock.Verify(u => u.Weeks.Update(week), Times.Once);
        _unitOfWorkMock.Verify(u => u.Save(default), Times.Once);
    }

    [Fact]
    public async Task LockAndAutofillAsync_SafeAutofill_DoesNotOverwriteExistingHumanPicks()
    {
        // Arrange (Escenario 3: Autollenado aleatorio seguro con ON CONFLICT DO NOTHING)
        int quinielaId = 1;
        int weekId = 7;
        int adminUserId = 99;

        var adminMember = new QuinielaMember
        {
            Id = 1,
            QuinielaId = quinielaId,
            UserId = adminUserId,
            Role = "OWNER",
            Alias = "ElAdmin"
        };
        var player2 = new QuinielaMember
        {
            Id = 2,
            QuinielaId = quinielaId,
            UserId = 102,
            Role = "MEMBER",
            Alias = "PlayerOlvidadizo"
        };

        var quiniela = new Quiniela { Id = quinielaId, LeagueId = 10, Active = true };
        var sport = new Sport { Id = 1, Name = "Fútbol", HasDraw = true };
        var league = new League { Id = 10, SportId = 1, Sport = sport, Active = true };

        var week = new Week { Id = weekId, Status = "PUBLISHED", FirstGameUtc = DateTime.UtcNow };

        var teamA = new Team { Id = 1, Abbreviation = "TOL" };
        var teamB = new Team { Id = 2, Abbreviation = "MTY" };
        var teamC = new Team { Id = 3, Abbreviation = "LEO" };
        var teamD = new Team { Id = 4, Abbreviation = "PAC" };

        var match1 = new MatchEntity { Id = 10, WeekId = weekId, HomeTeam = teamA, AwayTeam = teamB };
        var match2 = new MatchEntity { Id = 20, WeekId = weekId, HomeTeam = teamC, AwayTeam = teamD };

        // El admin ya tiene pick manual en Match 1
        var existingAdminPick = new Pick
        {
            Id = 501,
            QuinielaId = quinielaId,
            MemberId = adminMember.Id,
            MatchId = match1.Id,
            PickAbbr = "TOL",
            IsAutoFilled = false
        };

        _unitOfWorkMock.Setup(u => u.QuinielaMembers.GetMembershipAsync(quinielaId, adminUserId)).ReturnsAsync(adminMember);
        _unitOfWorkMock.Setup(u => u.Users.GetAsync(adminUserId)).ReturnsAsync(new User { Id = adminUserId, Role = "USER" });
        _unitOfWorkMock.Setup(u => u.Quinielas.GetAsync(quinielaId)).ReturnsAsync(quiniela);
        _unitOfWorkMock.Setup(u => u.Weeks.GetAsync(weekId)).ReturnsAsync(week);
        _unitOfWorkMock.Setup(u => u.Leagues.GetAsync(10)).ReturnsAsync(league);
        _unitOfWorkMock.Setup(u => u.Sports.GetAsync(1)).ReturnsAsync(sport);
        _unitOfWorkMock.Setup(u => u.QuinielaMembers.GetMembersAsync(quinielaId)).ReturnsAsync(new List<QuinielaMember> { adminMember, player2 });
        _unitOfWorkMock.Setup(u => u.Matches.GetByWeekIdAsync(weekId)).ReturnsAsync(new List<MatchEntity> { match1, match2 });
        _unitOfWorkMock.Setup(u => u.Picks.GetAllPicksForWeekAsync(quinielaId, weekId)).ReturnsAsync(new List<Pick> { existingAdminPick });

        List<Pick>? insertedPicks = null;
        _unitOfWorkMock.Setup(u => u.Picks.InsertMissingAutoFilledPicksAsync(It.IsAny<IEnumerable<Pick>>()))
            .Callback<IEnumerable<Pick>>(p => insertedPicks = p.ToList())
            .ReturnsAsync(3); // 1 para admin (match2), 2 para player2 (match1 y match2)

        var auditLogs = new List<PickAuditLog>();
        _unitOfWorkMock.Setup(u => u.PickAuditLogs.InsertAsync(It.IsAny<PickAuditLog>()))
            .Callback<PickAuditLog>(l => auditLogs.Add(l))
            .ReturnsAsync(true);

        // Act
        var result = await _pickApplication.LockAndAutofillAsync(quinielaId, weekId, adminUserId);

        // Assert
        Assert.True(result.isSuccess);
        Assert.Equal("LOCKED", week.Status);
        Assert.NotNull(insertedPicks);
        Assert.Equal(3, insertedPicks.Count);

        // Verificar que ningún autollenado intentó sobreescribir el match1 del admin
        Assert.DoesNotContain(insertedPicks, p => p.MemberId == adminMember.Id && p.MatchId == match1.Id);

        // Todos los picks insertados son autollenados
        Assert.All(insertedPicks, p => Assert.True(p.IsAutoFilled));

        // Todos los logs tienen Source = "AUTOFILL" y Action = "missing-autofilled"
        Assert.Equal(3, auditLogs.Count);
        Assert.All(auditLogs, l =>
        {
            Assert.Equal("AUTOFILL", l.Source);
            Assert.Equal("missing-autofilled", l.Action);
        });
    }

    [Fact]
    public async Task GetPicksAsync_WhenWeekPublished_HidesRivalPicks()
    {
        // Arrange (Escenario 4a: Privacidad previa al cierre)
        int quinielaId = 1;
        int weekId = 5;
        int userId = 10;

        var myMember = new QuinielaMember { Id = 1, QuinielaId = quinielaId, UserId = userId, Alias = "Yo" };
        var rivalMember = new QuinielaMember { Id = 2, QuinielaId = quinielaId, UserId = 20, Alias = "Rival" };

        var quiniela = new Quiniela { Id = quinielaId, LeagueId = 1 };
        var week = new Week
        {
            Id = weekId,
            Status = "PUBLISHED",
            FirstGameUtc = DateTime.UtcNow.AddDays(1)
        };

        var myPick = new Pick { Id = 1, QuinielaId = quinielaId, MemberId = 1, MatchId = 100, PickAbbr = "AME" };

        var league = new League { Id = 1, SportId = 1, Active = true };
        var sport = new Sport { Id = 1, Name = "Fútbol", HasDraw = true };

        _unitOfWorkMock.Setup(u => u.QuinielaMembers.GetMembershipAsync(quinielaId, userId)).ReturnsAsync(myMember);
        _unitOfWorkMock.Setup(u => u.Quinielas.GetAsync(quinielaId)).ReturnsAsync(quiniela);
        _unitOfWorkMock.Setup(u => u.Leagues.GetAsync(1)).ReturnsAsync(league);
        _unitOfWorkMock.Setup(u => u.Sports.GetAsync(1)).ReturnsAsync(sport);
        _unitOfWorkMock.Setup(u => u.Weeks.GetAsync(weekId)).ReturnsAsync(week);
        _unitOfWorkMock.Setup(u => u.Matches.GetByWeekIdAsync(weekId)).ReturnsAsync(new List<MatchEntity>());
        _unitOfWorkMock.Setup(u => u.QuinielaMembers.GetMembersAsync(quinielaId)).ReturnsAsync(new List<QuinielaMember> { myMember, rivalMember });
        _unitOfWorkMock.Setup(u => u.Picks.GetMemberPicksForWeekAsync(quinielaId, myMember.Id, weekId)).ReturnsAsync(new List<Pick> { myPick });

        // Act
        var result = await _pickApplication.GetPicksAsync(quinielaId, weekId, userId);

        // Assert
        Assert.True(result.isSuccess);
        Assert.NotNull(result.Data);
        Assert.False(result.Data.IsRevealed);
        Assert.False(result.Data.IsLocked);
        Assert.Single(result.Data.Picks);
        Assert.Equal(myMember.Id, result.Data.Picks.First().MemberId);
    }

    [Fact]
    public async Task GetPicksAsync_WhenWeekLocked_RevealsAllMemberPicks()
    {
        // Arrange (Escenario 4b: Revelación de picks rivales tras el bloqueo)
        int quinielaId = 1;
        int weekId = 5;
        int userId = 10;

        var myMember = new QuinielaMember { Id = 1, QuinielaId = quinielaId, UserId = userId, Alias = "Yo" };
        var rivalMember = new QuinielaMember { Id = 2, QuinielaId = quinielaId, UserId = 20, Alias = "Rival" };

        var quiniela = new Quiniela { Id = quinielaId, LeagueId = 1 };
        var week = new Week
        {
            Id = weekId,
            Status = "LOCKED",
            FirstGameUtc = DateTime.UtcNow.AddHours(-1)
        };

        var myPick = new Pick { Id = 1, QuinielaId = quinielaId, MemberId = 1, MatchId = 100, PickAbbr = "AME" };
        var rivalPick = new Pick { Id = 2, QuinielaId = quinielaId, MemberId = 2, MatchId = 100, PickAbbr = "EMPATE" };

        var league = new League { Id = 1, SportId = 1, Active = true };
        var sport = new Sport { Id = 1, Name = "Fútbol", HasDraw = true };

        _unitOfWorkMock.Setup(u => u.QuinielaMembers.GetMembershipAsync(quinielaId, userId)).ReturnsAsync(myMember);
        _unitOfWorkMock.Setup(u => u.Quinielas.GetAsync(quinielaId)).ReturnsAsync(quiniela);
        _unitOfWorkMock.Setup(u => u.Leagues.GetAsync(1)).ReturnsAsync(league);
        _unitOfWorkMock.Setup(u => u.Sports.GetAsync(1)).ReturnsAsync(sport);
        _unitOfWorkMock.Setup(u => u.Weeks.GetAsync(weekId)).ReturnsAsync(week);
        _unitOfWorkMock.Setup(u => u.Matches.GetByWeekIdAsync(weekId)).ReturnsAsync(new List<MatchEntity>());
        _unitOfWorkMock.Setup(u => u.QuinielaMembers.GetMembersAsync(quinielaId)).ReturnsAsync(new List<QuinielaMember> { myMember, rivalMember });
        _unitOfWorkMock.Setup(u => u.Picks.GetAllPicksForWeekAsync(quinielaId, weekId)).ReturnsAsync(new List<Pick> { myPick, rivalPick });

        // Act
        var result = await _pickApplication.GetPicksAsync(quinielaId, weekId, userId);

        // Assert
        Assert.True(result.isSuccess);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.IsRevealed);
        Assert.True(result.Data.IsLocked);
        Assert.Equal(2, result.Data.Picks.Count());
    }

    [Fact]
    public async Task SubmitPickAsync_JornadaDeLiguilla_RechazaElPick()
    {
        // Arrange: Liga MX con 17 jornadas regulares; el partido pertenece a la J18 (Liguilla)
        int quinielaId = 1, userId = 10, matchId = 100, weekId = 18;
        var membership = new QuinielaMember { Id = 42, QuinielaId = quinielaId, UserId = userId };
        var quiniela = new Quiniela { Id = quinielaId, LeagueId = 1, Active = true };
        var match = new MatchEntity { Id = matchId, WeekId = weekId, StatusState = "pre", DateUtc = DateTime.UtcNow.AddDays(2) };
        var week = new Week { Id = weekId, WeekNumber = 18, Status = "PUBLISHED", FirstGameUtc = DateTime.UtcNow.AddDays(2) };

        _unitOfWorkMock.Setup(u => u.QuinielaMembers.GetMembershipAsync(quinielaId, userId)).ReturnsAsync(membership);
        _unitOfWorkMock.Setup(u => u.Quinielas.GetAsync(quinielaId)).ReturnsAsync(quiniela);
        _unitOfWorkMock.Setup(u => u.Matches.GetAsync(matchId)).ReturnsAsync(match);
        _unitOfWorkMock.Setup(u => u.Weeks.GetAsync(weekId)).ReturnsAsync(week);
        _unitOfWorkMock.Setup(u => u.Leagues.GetAsync(1)).ReturnsAsync(new League { Id = 1, WeeksCount = 17 });

        // Act
        var result = await _pickApplication.SubmitPickAsync(quinielaId, userId, new SubmitPickRequestDto { MatchId = matchId, PickAbbr = "AME" });

        // Assert
        Assert.False(result.isSuccess);
        Assert.Contains("Liguilla", result.Message);
        _unitOfWorkMock.Verify(u => u.Picks.InsertAsync(It.IsAny<Pick>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.Weeks.Update(It.IsAny<Week>()), Times.Never);
    }

    [Fact]
    public async Task LockAndAutofillAsync_JornadaDeLiguilla_NoBloqueaNiAutollena()
    {
        // Arrange
        int quinielaId = 1, weekId = 18, adminUserId = 99;
        var week = new Week { Id = weekId, WeekNumber = 18, Status = "PUBLISHED" };

        _unitOfWorkMock.Setup(u => u.QuinielaMembers.GetMembershipAsync(quinielaId, adminUserId))
            .ReturnsAsync(new QuinielaMember { Id = 1, QuinielaId = quinielaId, UserId = adminUserId, Role = "OWNER" });
        _unitOfWorkMock.Setup(u => u.Users.GetAsync(adminUserId)).ReturnsAsync(new User { Id = adminUserId, Role = "USER" });
        _unitOfWorkMock.Setup(u => u.Quinielas.GetAsync(quinielaId)).ReturnsAsync(new Quiniela { Id = quinielaId, LeagueId = 1, Active = true });
        _unitOfWorkMock.Setup(u => u.Weeks.GetAsync(weekId)).ReturnsAsync(week);
        _unitOfWorkMock.Setup(u => u.Leagues.GetAsync(1)).ReturnsAsync(new League { Id = 1, WeeksCount = 17 });

        // Act
        var result = await _pickApplication.LockAndAutofillAsync(quinielaId, weekId, adminUserId);

        // Assert
        Assert.False(result.isSuccess);
        Assert.Contains("Liguilla", result.Message);
        Assert.Equal("PUBLISHED", week.Status);
        _unitOfWorkMock.Verify(u => u.Weeks.Update(It.IsAny<Week>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.Picks.InsertMissingAutoFilledPicksAsync(It.IsAny<IEnumerable<Pick>>()), Times.Never);
    }

    // ---- SPEC-020: una quiniela por temporada ----

    private void ArrangeSubmit(Quiniela quiniela, Week week, MatchEntity match, QuinielaMember membership)
    {
        _unitOfWorkMock.Setup(u => u.QuinielaMembers.GetMembershipAsync(quiniela.Id, membership.UserId)).ReturnsAsync(membership);
        _unitOfWorkMock.Setup(u => u.Quinielas.GetAsync(quiniela.Id)).ReturnsAsync(quiniela);
        _unitOfWorkMock.Setup(u => u.Matches.GetAsync(match.Id)).ReturnsAsync(match);
        _unitOfWorkMock.Setup(u => u.Weeks.GetAsync(week.Id)).ReturnsAsync(week);
        var sport = new Sport { Id = 1, Name = "Fútbol", HasDraw = true };
        _unitOfWorkMock.Setup(u => u.Leagues.GetAsync(1)).ReturnsAsync(new League { Id = 1, SportId = 1, Sport = sport, WeeksCount = 17 });
        _unitOfWorkMock.Setup(u => u.Sports.GetAsync(1)).ReturnsAsync(sport);
        _unitOfWorkMock.Setup(u => u.Picks.GetPickAsync(quiniela.Id, membership.Id, match.Id)).ReturnsAsync((Pick?)null);
        _unitOfWorkMock.Setup(u => u.Picks.InsertAsync(It.IsAny<Pick>())).ReturnsAsync(true);
        _unitOfWorkMock.Setup(u => u.PickAuditLogs.InsertAsync(It.IsAny<PickAuditLog>())).ReturnsAsync(true);
    }

    private static (Quiniela quiniela, Week week, MatchEntity match, QuinielaMember membership) BuildSubmitScenario(int? quinielaSeasonId, int weekSeasonId, DateTime? archivedAt = null)
    {
        var quiniela = new Quiniela { Id = 1, LeagueId = 1, Active = true, SeasonId = quinielaSeasonId, ArchivedAt = archivedAt };
        var week = new Week { Id = 5, SeasonId = weekSeasonId, WeekNumber = 3, Status = "PUBLISHED", FirstGameUtc = DateTime.UtcNow.AddHours(2) };
        var match = new MatchEntity
        {
            Id = 100, WeekId = 5, DateUtc = DateTime.UtcNow.AddHours(2), StatusState = "pre",
            HomeTeam = new Team { Id = 1, Abbreviation = "AME" }, AwayTeam = new Team { Id = 2, Abbreviation = "GDL" }
        };
        var membership = new QuinielaMember { Id = 42, QuinielaId = 1, UserId = 10, Alias = "ElCrack", Role = "MEMBER" };
        return (quiniela, week, match, membership);
    }

    [Fact]
    public async Task SubmitPickAsync_PartidoDeOtraTemporada_RechazaConMensajeExacto()
    {
        // Arrange: la quiniela es de la temporada 3 y el partido pertenece a la temporada 2
        var (quiniela, week, match, membership) = BuildSubmitScenario(quinielaSeasonId: 3, weekSeasonId: 2);
        ArrangeSubmit(quiniela, week, match, membership);

        // Act
        var result = await _pickApplication.SubmitPickAsync(1, 10, new SubmitPickRequestDto { MatchId = 100, PickAbbr = "AME" });

        // Assert
        Assert.False(result.isSuccess);
        Assert.Equal("El partido no pertenece a la temporada de esta quiniela.", result.Message);
        _unitOfWorkMock.Verify(u => u.Picks.InsertAsync(It.IsAny<Pick>()), Times.Never);
    }

    [Fact]
    public async Task SubmitPickAsync_PartidoDeLaTemporadaDeLaQuiniela_Acepta()
    {
        // Arrange
        var (quiniela, week, match, membership) = BuildSubmitScenario(quinielaSeasonId: 3, weekSeasonId: 3);
        ArrangeSubmit(quiniela, week, match, membership);

        // Act
        var result = await _pickApplication.SubmitPickAsync(1, 10, new SubmitPickRequestDto { MatchId = 100, PickAbbr = "AME" });

        // Assert
        Assert.True(result.isSuccess);
        _unitOfWorkMock.Verify(u => u.Picks.InsertAsync(It.Is<Pick>(p => p.PickAbbr == "AME")), Times.Once);
    }

    [Fact]
    public async Task SubmitPickAsync_QuinielaArchivada_RechazaConMensajeExacto()
    {
        // Arrange: archivada, aunque la temporada coincida
        var (quiniela, week, match, membership) = BuildSubmitScenario(quinielaSeasonId: 3, weekSeasonId: 3, archivedAt: DateTime.UtcNow.AddDays(-1));
        ArrangeSubmit(quiniela, week, match, membership);

        // Act
        var result = await _pickApplication.SubmitPickAsync(1, 10, new SubmitPickRequestDto { MatchId = 100, PickAbbr = "AME" });

        // Assert
        Assert.False(result.isSuccess);
        Assert.Equal("La quiniela está archivada y es de solo lectura.", result.Message);
        _unitOfWorkMock.Verify(u => u.Picks.InsertAsync(It.IsAny<Pick>()), Times.Never);
    }

    [Fact]
    public async Task SubmitPickAsync_QuinielaArchivadaYDeOtraTemporada_PriorizaMensajeDeArchivada()
    {
        // Arrange
        var (quiniela, week, match, membership) = BuildSubmitScenario(quinielaSeasonId: 3, weekSeasonId: 2, archivedAt: DateTime.UtcNow);
        ArrangeSubmit(quiniela, week, match, membership);

        // Act
        var result = await _pickApplication.SubmitPickAsync(1, 10, new SubmitPickRequestDto { MatchId = 100, PickAbbr = "AME" });

        // Assert
        Assert.Equal("La quiniela está archivada y es de solo lectura.", result.Message);
    }

    [Fact]
    public async Task SubmitPickAsync_QuinielaSinSeasonId_AceptaCualquierTemporadaComoAntes()
    {
        // Arrange: SeasonId null conserva el comportamiento previo, sin importar la temporada de la jornada
        var (quiniela, week, match, membership) = BuildSubmitScenario(quinielaSeasonId: null, weekSeasonId: 99);
        ArrangeSubmit(quiniela, week, match, membership);

        // Act
        var result = await _pickApplication.SubmitPickAsync(1, 10, new SubmitPickRequestDto { MatchId = 100, PickAbbr = "GDL" });

        // Assert
        Assert.True(result.isSuccess);
        _unitOfWorkMock.Verify(u => u.Picks.InsertAsync(It.Is<Pick>(p => p.PickAbbr == "GDL")), Times.Once);
    }

    private void ArrangeAutofillAdmin(Quiniela quiniela, Week week)
    {
        _unitOfWorkMock.Setup(u => u.QuinielaMembers.GetMembershipAsync(quiniela.Id, 99))
            .ReturnsAsync(new QuinielaMember { Id = 1, QuinielaId = quiniela.Id, UserId = 99, Role = "OWNER" });
        _unitOfWorkMock.Setup(u => u.Users.GetAsync(99)).ReturnsAsync(new User { Id = 99, Role = "USER" });
        _unitOfWorkMock.Setup(u => u.Quinielas.GetAsync(quiniela.Id)).ReturnsAsync(quiniela);
        _unitOfWorkMock.Setup(u => u.Weeks.GetAsync(week.Id)).ReturnsAsync(week);
        _unitOfWorkMock.Setup(u => u.Leagues.GetAsync(1)).ReturnsAsync(new League { Id = 1, WeeksCount = 17 });
    }

    [Fact]
    public async Task LockAndAutofillAsync_JornadaDeOtraTemporada_RechazaYNoBloquea()
    {
        // Arrange
        var quiniela = new Quiniela { Id = 1, LeagueId = 1, Active = true, SeasonId = 3 };
        var week = new Week { Id = 7, SeasonId = 2, WeekNumber = 5, Status = "PUBLISHED" };
        ArrangeAutofillAdmin(quiniela, week);

        // Act
        var result = await _pickApplication.LockAndAutofillAsync(1, 7, 99);

        // Assert
        Assert.False(result.isSuccess);
        Assert.Equal("La jornada no pertenece a la temporada de esta quiniela.", result.Message);
        Assert.Equal("PUBLISHED", week.Status);
        _unitOfWorkMock.Verify(u => u.Weeks.Update(It.IsAny<Week>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.Picks.InsertMissingAutoFilledPicksAsync(It.IsAny<IEnumerable<Pick>>()), Times.Never);
    }

    [Fact]
    public async Task LockAndAutofillAsync_QuinielaArchivada_RechazaYNoBloquea()
    {
        // Arrange
        var quiniela = new Quiniela { Id = 1, LeagueId = 1, Active = true, SeasonId = 3, ArchivedAt = DateTime.UtcNow };
        var week = new Week { Id = 7, SeasonId = 3, WeekNumber = 5, Status = "PUBLISHED" };
        ArrangeAutofillAdmin(quiniela, week);

        // Act
        var result = await _pickApplication.LockAndAutofillAsync(1, 7, 99);

        // Assert
        Assert.False(result.isSuccess);
        Assert.Equal("La quiniela está archivada y es de solo lectura.", result.Message);
        Assert.Equal("PUBLISHED", week.Status);
        _unitOfWorkMock.Verify(u => u.Weeks.Update(It.IsAny<Week>()), Times.Never);
    }
}
