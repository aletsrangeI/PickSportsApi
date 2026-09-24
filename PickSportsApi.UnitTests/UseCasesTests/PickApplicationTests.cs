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
}
