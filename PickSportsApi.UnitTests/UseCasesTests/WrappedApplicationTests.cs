using System.Text.Json;
using Domain.Entities;
using DTO.Wrapped;
using FluentAssertions;
using Interface.Persistence;
using Microsoft.EntityFrameworkCore;
using Moq;
using UseCases.Bulletin;
using UseCases.Wrapped;
using Xunit;
using MatchEntity = Domain.Entities.Match;

namespace PickSportsApi.UnitTests.UseCasesTests;

public class WrappedApplicationTests
{
    private const int QuinielaId = 1;
    private const int LeagueId = 5;
    private const int SeasonId = 50;
    private const int OwnerUserId = 11;
    private const int MemberUserId = 12;
    private const int NoPicksUserId = 13;
    private const int GlobalAdminUserId = 999;
    private const string CachedSeasonName = "SNAPSHOT-CACHED";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IQuinielaRepository> _quinielas = new();
    private readonly Mock<ISeasonRepository> _seasons = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IQuinielaMemberRepository> _members = new();
    private readonly Mock<IPickRepository> _picks = new();
    private readonly Mock<IMatchRepository> _matches = new();
    private readonly Mock<IWeeklyAwardRepository> _awards = new();
    private readonly Mock<ISeasonWrappedSnapshotRepository> _snapshots = new();
    private readonly WrappedApplication _sut;

    private readonly Season _season = new() { Id = SeasonId, LeagueId = LeagueId, Name = "Apertura 2026", IsFinished = true, EndDate = new DateTime(2026, 12, 1) };
    private readonly Week _week1 = new() { Id = 70, SeasonId = SeasonId, WeekNumber = 1, FirstGameUtc = new DateTime(2026, 7, 10, 1, 0, 0, DateTimeKind.Utc) };
    private readonly Week _week2 = new() { Id = 71, SeasonId = SeasonId, WeekNumber = 2, FirstGameUtc = new DateTime(2026, 7, 17, 1, 0, 0, DateTimeKind.Utc) };
    private readonly List<QuinielaMember> _memberList;
    private readonly List<MatchEntity> _matchList;
    private readonly List<Pick> _pickList;

    public WrappedApplicationTests()
    {
        _uow.SetupGet(u => u.Quinielas).Returns(_quinielas.Object);
        _uow.SetupGet(u => u.Seasons).Returns(_seasons.Object);
        _uow.SetupGet(u => u.Users).Returns(_users.Object);
        _uow.SetupGet(u => u.QuinielaMembers).Returns(_members.Object);
        _uow.SetupGet(u => u.Picks).Returns(_picks.Object);
        _uow.SetupGet(u => u.Matches).Returns(_matches.Object);
        _uow.SetupGet(u => u.WeeklyAwards).Returns(_awards.Object);
        _uow.SetupGet(u => u.SeasonWrappedSnapshots).Returns(_snapshots.Object);

        _memberList = new List<QuinielaMember>
        {
            new() { Id = 101, QuinielaId = QuinielaId, UserId = OwnerUserId, Alias = "Alex", Role = "OWNER", TotalPoints = 5, TotalHits = 5, User = new User { Id = OwnerUserId, DisplayName = "Alejandro" } },
            new() { Id = 102, QuinielaId = QuinielaId, UserId = MemberUserId, Alias = "", Role = "MEMBER", TotalPoints = 3, TotalHits = 3, User = new User { Id = MemberUserId, DisplayName = "Fernando" } },
            new() { Id = 103, QuinielaId = QuinielaId, UserId = NoPicksUserId, Alias = "Dani", Role = "MEMBER", User = new User { Id = NoPicksUserId, DisplayName = "Daniela" } }
        };

        _matchList = new List<MatchEntity>
        {
            BuildMatch(1, _week1, "AME", "GDL", "AME"),
            BuildMatch(2, _week1, "MTY", "UANL", "UANL"),
            BuildMatch(3, _week2, "LEO", "QRO", "EMPATE")
        };

        _pickList = new List<Pick>();
        foreach (var m in _matchList)
        {
            _pickList.Add(BuildPick(101, m, true));
            _pickList.Add(BuildPick(102, m, m.Id != 2));
        }

        _quinielas.Setup(r => r.GetAsync(QuinielaId)).ReturnsAsync(new Quiniela { Id = QuinielaId, Name = "Amigos", LeagueId = LeagueId });
        _seasons.Setup(r => r.GetAsync(SeasonId)).ReturnsAsync(_season);
        _seasons.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Season> { _season });
        _members.Setup(r => r.GetMembershipAsync(QuinielaId, It.IsAny<int>()))
            .ReturnsAsync((int _, int userId) => _memberList.FirstOrDefault(m => m.UserId == userId));
        _members.Setup(r => r.GetMembersAsync(QuinielaId)).ReturnsAsync(_memberList);
        _picks.Setup(r => r.GetAllPicksForQuinielaAsync(QuinielaId)).ReturnsAsync(_pickList);
        _matches.Setup(r => r.GetBySeasonIdAsync(SeasonId)).ReturnsAsync(_matchList);
        _awards.Setup(r => r.GetAllAwardsForQuinielaAsync(QuinielaId)).ReturnsAsync(new List<WeeklyAward>());
        _users.Setup(r => r.GetAsync(GlobalAdminUserId)).ReturnsAsync(new User { Id = GlobalAdminUserId, Role = "ADMIN", DisplayName = "Root" });
        _users.Setup(r => r.GetAsync(MemberUserId)).ReturnsAsync(new User { Id = MemberUserId, Role = "USER", DisplayName = "Fernando" });
        _uow.Setup(u => u.Save(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _sut = new WrappedApplication(_uow.Object, new SeasonWrappedEngine(), new LeagueDerbyDetector());
    }

    #region Test Data

    private static MatchEntity BuildMatch(int id, Week week, string home, string away, string winner) => new()
    {
        Id = id,
        WeekId = week.Id,
        Week = week,
        DateUtc = week.FirstGameUtc!.Value.AddHours(id),
        StatusState = "post",
        HomeScore = winner == home ? 2 : (winner == "EMPATE" ? 1 : 0),
        AwayScore = winner == away ? 2 : (winner == "EMPATE" ? 1 : 0),
        WinnerAbbr = winner,
        HomeTeam = new Team { Id = id * 10, Abbreviation = home, Name = home + " FC" },
        AwayTeam = new Team { Id = id * 10 + 1, Abbreviation = away, Name = away + " FC" }
    };

    private static Pick BuildPick(int memberId, MatchEntity match, bool hit) => new()
    {
        Id = memberId * 100 + match.Id,
        QuinielaId = QuinielaId,
        MemberId = memberId,
        MatchId = match.Id,
        Match = match,
        PickAbbr = hit ? match.WinnerAbbr! : "OTRO",
        IsHit = hit,
        Created = match.Week.FirstGameUtc!.Value.AddHours(-5)
    };

    private static string Payload(string seasonName = CachedSeasonName) => JsonSerializer.Serialize(
        new SeasonWrappedDto
        {
            SeasonId = SeasonId,
            SeasonName = seasonName,
            FinalPosition = 1,
            TotalMembers = 3,
            ArchetypeCode = "OJO_CLINICO",
            ArchetypeEmoji = "x",
            ArchetypeTitle = "t",
            ArchetypeDescription = "d",
            ThemeColor = "#112233",
            FinalCard = new WrappedFinalCardDto { DisplayName = "Alex", SeasonName = seasonName, ArchetypeEmoji = "x", ArchetypeTitle = "t", ThemeColor = "#112233" }
        }, JsonOptions);

    #endregion

    #region GetWrappedAsync

    [Fact]
    public async Task GetWrapped_WhenQuinielaMissing_ShouldFail()
    {
        _quinielas.Setup(r => r.GetAsync(QuinielaId)).ReturnsAsync((Quiniela?)null);

        var result = await _sut.GetWrappedAsync(QuinielaId, SeasonId, OwnerUserId);

        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("Quiniela no encontrada.");
    }

    [Fact]
    public async Task GetWrapped_WhenSeasonMissing_ShouldFail()
    {
        _seasons.Setup(r => r.GetAsync(SeasonId)).ReturnsAsync((Season?)null);

        var result = await _sut.GetWrappedAsync(QuinielaId, SeasonId, OwnerUserId);

        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("Temporada no encontrada.");
    }

    [Fact]
    public async Task GetWrapped_WhenUserIsNotMember_ShouldFail()
    {
        var result = await _sut.GetWrappedAsync(QuinielaId, SeasonId, 4242);

        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("No perteneces a esta quiniela.");
    }

    [Fact]
    public async Task GetWrapped_WhenSeasonNotFinished_ShouldFail()
    {
        _season.IsFinished = false;

        var result = await _sut.GetWrappedAsync(QuinielaId, SeasonId, OwnerUserId);

        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("La temporada aun no ha finalizado.");
    }

    [Fact]
    public async Task GetWrapped_WhenUserHasNoPicksInSeason_ShouldFailWithoutInserting()
    {
        var result = await _sut.GetWrappedAsync(QuinielaId, SeasonId, NoPicksUserId);

        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("No hay informacion suficiente para generar tu Wrapped.");
        _snapshots.Verify(r => r.InsertAsync(It.IsAny<SeasonWrappedSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task GetWrapped_WhenPicksBelongToAnotherSeason_ShouldFailAsNoParticipation()
    {
        var otherWeek = new Week { Id = 99, SeasonId = 777, WeekNumber = 1, FirstGameUtc = DateTime.UtcNow };
        // Los partidos SI se devuelven para la temporada; solo el filtro Match.Week.SeasonId de los picks explica el resultado.
        foreach (var p in _pickList) p.Match = new MatchEntity { Id = p.MatchId, WeekId = 99, Week = otherWeek };

        var result = await _sut.GetWrappedAsync(QuinielaId, SeasonId, OwnerUserId);

        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("No hay informacion suficiente para generar tu Wrapped.");
    }

    [Fact]
    public async Task GetWrapped_WhenSnapshotExists_ShouldReturnCachedWithoutRecalculating()
    {
        // Arrange: payload con un nombre de temporada distinto al real; si se recalculara cambiaria (o el motor lanzaria).
        _snapshots.Setup(r => r.GetByKeyAsync(QuinielaId, SeasonId, OwnerUserId))
            .ReturnsAsync(new SeasonWrappedSnapshot { QuinielaId = QuinielaId, SeasonId = SeasonId, UserId = OwnerUserId, WrappedPayloadJson = Payload() });

        var result = await _sut.GetWrappedAsync(QuinielaId, SeasonId, OwnerUserId);

        result.isSuccess.Should().BeTrue();
        result.Data.SeasonName.Should().Be(CachedSeasonName);
        _snapshots.Verify(r => r.InsertAsync(It.IsAny<SeasonWrappedSnapshot>()), Times.Never);
        _snapshots.Verify(r => r.UpdateAsync(It.IsAny<SeasonWrappedSnapshot>()), Times.Never);
        _picks.Verify(r => r.GetAllPicksForQuinielaAsync(It.IsAny<int>()), Times.Never);
        _uow.Verify(u => u.Save(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetWrapped_WhenSnapshotAbsent_ShouldCalculateInsertOnceAndSaveOnce()
    {
        SeasonWrappedSnapshot? inserted = null;
        _snapshots.Setup(r => r.GetByKeyAsync(QuinielaId, SeasonId, OwnerUserId)).ReturnsAsync((SeasonWrappedSnapshot?)null);
        _snapshots.Setup(r => r.InsertAsync(It.IsAny<SeasonWrappedSnapshot>()))
            .Callback<SeasonWrappedSnapshot>(s => inserted = s).ReturnsAsync(true);

        var result = await _sut.GetWrappedAsync(QuinielaId, SeasonId, OwnerUserId);

        result.isSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        _snapshots.Verify(r => r.InsertAsync(It.IsAny<SeasonWrappedSnapshot>()), Times.Once);
        _uow.Verify(u => u.Save(It.IsAny<CancellationToken>()), Times.Once);
        inserted.Should().NotBeNull();
        inserted!.QuinielaId.Should().Be(QuinielaId);
        inserted.SeasonId.Should().Be(SeasonId);
        inserted.UserId.Should().Be(OwnerUserId);
        // Regresion: el repositorio filtra por Active; un snapshot inactivo nunca se relee y choca con el indice unico.
        inserted.Active.Should().BeTrue();
        var roundTrip = JsonSerializer.Deserialize<SeasonWrappedDto>(inserted.WrappedPayloadJson, JsonOptions);
        roundTrip.Should().NotBeNull();
        inserted.WrappedPayloadJson.Should().Contain("\"seasonName\"");
    }

    #endregion

    #region GenerateAllAsync

    [Fact]
    public async Task GenerateAll_WhenQuinielaMissing_ShouldFail()
    {
        _quinielas.Setup(r => r.GetAsync(QuinielaId)).ReturnsAsync((Quiniela?)null);

        var result = await _sut.GenerateAllAsync(QuinielaId, SeasonId, OwnerUserId);

        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("Quiniela no encontrada.");
    }

    [Fact]
    public async Task GenerateAll_WhenSeasonMissing_ShouldFail()
    {
        _seasons.Setup(r => r.GetAsync(SeasonId)).ReturnsAsync((Season?)null);

        var result = await _sut.GenerateAllAsync(QuinielaId, SeasonId, OwnerUserId);

        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("Temporada no encontrada.");
    }

    [Fact]
    public async Task GenerateAll_WhenRequesterIsPlainMember_ShouldFailWithPermissionMessage()
    {
        var result = await _sut.GenerateAllAsync(QuinielaId, SeasonId, MemberUserId);

        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("Solo un administrador puede generar los Wrapped.");
        _snapshots.Verify(r => r.InsertAsync(It.IsAny<SeasonWrappedSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task GenerateAll_WhenSeasonNotFinished_ShouldFail()
    {
        _season.IsFinished = false;

        var result = await _sut.GenerateAllAsync(QuinielaId, SeasonId, OwnerUserId);

        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("La temporada aun no ha finalizado.");
    }

    [Fact]
    public async Task GenerateAll_WhenOwner_ShouldInsertForMembersWithPicksAndSkipOthers()
    {
        _snapshots.Setup(r => r.GetByKeyAsync(QuinielaId, SeasonId, It.IsAny<int>())).ReturnsAsync((SeasonWrappedSnapshot?)null);

        var result = await _sut.GenerateAllAsync(QuinielaId, SeasonId, OwnerUserId);

        result.isSuccess.Should().BeTrue();
        result.Data.SeasonId.Should().Be(SeasonId);
        result.Data.Generated.Should().Be(2);
        result.Data.Skipped.Should().Be(1);
        _snapshots.Verify(r => r.InsertAsync(It.IsAny<SeasonWrappedSnapshot>()), Times.Exactly(2));
        _uow.Verify(u => u.Save(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateAll_WhenGlobalAdminNotMember_ShouldBeAllowed()
    {
        _snapshots.Setup(r => r.GetByKeyAsync(QuinielaId, SeasonId, It.IsAny<int>())).ReturnsAsync((SeasonWrappedSnapshot?)null);

        var result = await _sut.GenerateAllAsync(QuinielaId, SeasonId, GlobalAdminUserId);

        result.isSuccess.Should().BeTrue();
        result.Data.Generated.Should().Be(2);
    }

    [Fact]
    public async Task GenerateAll_WhenSnapshotExists_ShouldUpdateInsteadOfInsert()
    {
        var existing = new SeasonWrappedSnapshot { QuinielaId = QuinielaId, SeasonId = SeasonId, UserId = OwnerUserId, WrappedPayloadJson = Payload() };
        _snapshots.Setup(r => r.GetByKeyAsync(QuinielaId, SeasonId, It.IsAny<int>())).ReturnsAsync((SeasonWrappedSnapshot?)null);
        _snapshots.Setup(r => r.GetByKeyAsync(QuinielaId, SeasonId, OwnerUserId)).ReturnsAsync(existing);

        var result = await _sut.GenerateAllAsync(QuinielaId, SeasonId, OwnerUserId);

        result.isSuccess.Should().BeTrue();
        _snapshots.Verify(r => r.UpdateAsync(existing), Times.Once);
        _snapshots.Verify(r => r.InsertAsync(It.IsAny<SeasonWrappedSnapshot>()), Times.Once);
        existing.WrappedPayloadJson.Should().NotContain(CachedSeasonName);
    }

    #endregion

    #region GetAvailabilityAsync

    [Fact]
    public async Task Availability_WhenFinishedSeasonAndUserHasPicks_ShouldBeAvailable()
    {
        var result = await _sut.GetAvailabilityAsync(QuinielaId, OwnerUserId);

        result.isSuccess.Should().BeTrue();
        result.Data.IsAvailable.Should().BeTrue();
        result.Data.SeasonId.Should().Be(SeasonId);
        result.Data.SeasonName.Should().Be("Apertura 2026");
    }

    [Fact]
    public async Task Availability_WhenSeasonNotFinished_ShouldNotBeAvailable()
    {
        _season.IsFinished = false;

        var result = await _sut.GetAvailabilityAsync(QuinielaId, OwnerUserId);

        result.isSuccess.Should().BeTrue();
        result.Data.IsAvailable.Should().BeFalse();
        result.Data.SeasonId.Should().BeNull();
    }

    [Fact]
    public async Task Availability_WhenUserHasNoPicks_ShouldNotBeAvailable()
    {
        var result = await _sut.GetAvailabilityAsync(QuinielaId, NoPicksUserId);

        result.isSuccess.Should().BeTrue();
        result.Data.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task Availability_WhenSeveralFinishedSeasons_ShouldPickTheLatestByEndDate()
    {
        var newer = new Season { Id = 51, LeagueId = LeagueId, Name = "Clausura 2027", IsFinished = true, EndDate = new DateTime(2027, 6, 1) };
        var newerWeek = new Week { Id = 90, SeasonId = 51, WeekNumber = 1, FirstGameUtc = new DateTime(2027, 2, 1, 0, 0, 0, DateTimeKind.Utc) };
        var newerMatch = BuildMatch(9, newerWeek, "AME", "GDL", "AME");
        _pickList.Add(BuildPick(101, newerMatch, true));
        _seasons.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Season> { _season, newer });

        var result = await _sut.GetAvailabilityAsync(QuinielaId, OwnerUserId);

        result.Data.IsAvailable.Should().BeTrue();
        result.Data.SeasonId.Should().Be(51);
    }

    [Fact]
    public async Task Availability_WhenUserIsNotMember_ShouldFail()
    {
        var result = await _sut.GetAvailabilityAsync(QuinielaId, 4242);

        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("No perteneces a esta quiniela.");
    }

    #endregion

    #region SetSeasonFinishedAsync

    [Fact]
    public async Task SetSeasonFinished_WhenNotGlobalAdmin_ShouldFail()
    {
        var result = await _sut.SetSeasonFinishedAsync(SeasonId, true, MemberUserId);

        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("Solo un administrador puede finalizar la temporada.");
        _seasons.Verify(r => r.UpdateAsync(It.IsAny<Season>()), Times.Never);
    }

    [Fact]
    public async Task SetSeasonFinished_WhenAdminAndSeasonMissing_ShouldFail()
    {
        _seasons.Setup(r => r.GetAsync(SeasonId)).ReturnsAsync((Season?)null);

        var result = await _sut.SetSeasonFinishedAsync(SeasonId, true, GlobalAdminUserId);

        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("Temporada no encontrada.");
    }

    [Fact]
    public async Task SetSeasonFinished_WhenAdminFinishes_ShouldSetFlagTimestampAndSaveOnce()
    {
        _season.IsFinished = false;

        var result = await _sut.SetSeasonFinishedAsync(SeasonId, true, GlobalAdminUserId);

        result.isSuccess.Should().BeTrue();
        _season.IsFinished.Should().BeTrue();
        _season.FinishedAtUtc.Should().NotBeNull();
        _seasons.Verify(r => r.UpdateAsync(_season), Times.Once);
        _uow.Verify(u => u.Save(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetSeasonFinished_WhenAdminReopens_ShouldClearFlagAndTimestamp()
    {
        _season.IsFinished = true;
        _season.FinishedAtUtc = DateTime.UtcNow;

        var result = await _sut.SetSeasonFinishedAsync(SeasonId, false, GlobalAdminUserId);

        result.isSuccess.Should().BeTrue();
        _season.IsFinished.Should().BeFalse();
        _season.FinishedAtUtc.Should().BeNull();
    }

    #endregion

    #region Correcciones de revision

    [Fact]
    public async Task GetWrapped_WhenSeasonBelongsToAnotherLeague_ShouldReportSeasonNotFound()
    {
        _season.LeagueId = 999;

        var result = await _sut.GetWrappedAsync(QuinielaId, SeasonId, OwnerUserId);

        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("Temporada no encontrada.");
    }

    [Fact]
    public async Task GenerateAll_WhenSeasonBelongsToAnotherLeague_ShouldReportSeasonNotFound()
    {
        _season.LeagueId = 999;

        var result = await _sut.GenerateAllAsync(QuinielaId, SeasonId, OwnerUserId);

        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("Temporada no encontrada.");
    }

    [Fact]
    public async Task GenerateAll_WhenRequesterIsNotMemberNorGlobalAdmin_ShouldGetPermissionMessage()
    {
        var result = await _sut.GenerateAllAsync(QuinielaId, SeasonId, 4242);

        result.isSuccess.Should().BeFalse();
        result.Message.Should().Contain("Solo un administrador puede generar los Wrapped.");
    }

    [Fact]
    public async Task GenerateAll_WhenMembersAreUnclaimedOrDuplicated_ShouldSkipThem()
    {
        _memberList.Add(new QuinielaMember { Id = 104, QuinielaId = QuinielaId, UserId = 0, Alias = "Sin cuenta", Role = "MEMBER", User = null! });
        _memberList.Add(new QuinielaMember { Id = 105, QuinielaId = QuinielaId, UserId = MemberUserId, Alias = "Dup", Role = "MEMBER", User = new User { Id = MemberUserId, DisplayName = "Fernando" } });
        _pickList.Add(BuildPick(104, _matchList[0], true));
        _pickList.Add(BuildPick(105, _matchList[0], true));
        _snapshots.Setup(r => r.GetByKeyAsync(QuinielaId, SeasonId, It.IsAny<int>())).ReturnsAsync((SeasonWrappedSnapshot?)null);

        var result = await _sut.GenerateAllAsync(QuinielaId, SeasonId, OwnerUserId);

        result.isSuccess.Should().BeTrue();
        result.Data.Generated.Should().Be(2);
        result.Data.Skipped.Should().Be(3); // Daniela sin picks, UserId 0 y duplicado de Fernando
        _snapshots.Verify(r => r.InsertAsync(It.Is<SeasonWrappedSnapshot>(s => s.UserId <= 0)), Times.Never);
        _snapshots.Verify(r => r.InsertAsync(It.Is<SeasonWrappedSnapshot>(s => s.UserId == MemberUserId)), Times.Once);
    }

    [Fact]
    public async Task GetWrapped_WhenInsertCollidesWithParallelRequest_ShouldReturnWinnerSnapshot()
    {
        var winner = new SeasonWrappedSnapshot { QuinielaId = QuinielaId, SeasonId = SeasonId, UserId = OwnerUserId, WrappedPayloadJson = Payload() };
        _snapshots.SetupSequence(r => r.GetByKeyAsync(QuinielaId, SeasonId, OwnerUserId))
            .ReturnsAsync((SeasonWrappedSnapshot?)null)
            .ReturnsAsync(winner);
        _snapshots.Setup(r => r.InsertAsync(It.IsAny<SeasonWrappedSnapshot>())).ReturnsAsync(true);
        _uow.Setup(u => u.Save(It.IsAny<CancellationToken>())).ThrowsAsync(new DbUpdateException("duplicate key"));

        var result = await _sut.GetWrappedAsync(QuinielaId, SeasonId, OwnerUserId);

        result.isSuccess.Should().BeTrue();
        result.Data.SeasonName.Should().Be(CachedSeasonName);
    }

    [Fact]
    public async Task GetWrapped_WhenInsertFailsAndNoSnapshotExists_ShouldReturnNormalError()
    {
        _snapshots.Setup(r => r.GetByKeyAsync(QuinielaId, SeasonId, OwnerUserId)).ReturnsAsync((SeasonWrappedSnapshot?)null);
        _snapshots.Setup(r => r.InsertAsync(It.IsAny<SeasonWrappedSnapshot>())).ReturnsAsync(true);
        _uow.Setup(u => u.Save(It.IsAny<CancellationToken>())).ThrowsAsync(new DbUpdateException("fk"));

        var result = await _sut.GetWrappedAsync(QuinielaId, SeasonId, OwnerUserId);

        result.isSuccess.Should().BeFalse();
        result.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetWrapped_WhenPickReferencesUnknownMatch_ShouldNotBreakAndNotSendOrphanPick()
    {
        var orphan = BuildMatch(77, _week1, "AME", "GDL", "AME");
        _pickList.Add(BuildPick(101, orphan, true)); // el partido 77 no esta en Matches.GetBySeasonIdAsync
        _snapshots.Setup(r => r.GetByKeyAsync(QuinielaId, SeasonId, OwnerUserId)).ReturnsAsync((SeasonWrappedSnapshot?)null);
        _snapshots.Setup(r => r.InsertAsync(It.IsAny<SeasonWrappedSnapshot>())).ReturnsAsync(true);

        var result = await _sut.GetWrappedAsync(QuinielaId, SeasonId, OwnerUserId);
        var input = await _sut.BuildEngineInputAsync(QuinielaId, _season, 101);

        result.isSuccess.Should().BeTrue();
        input.Picks.Should().NotContain(p => p.MatchId == 77);
        input.Picks.Should().OnlyContain(p => input.Matches.Any(m => m.MatchId == p.MatchId));
    }

    [Fact]
    public async Task BuildEngineInput_ShouldMapPickFlagsMemberTotalsDerbyAndNames()
    {
        var derby = _matchList[0]; // AME vs GDL
        var pick = _pickList.First(p => p.MemberId == 101 && p.MatchId == derby.Id);
        pick.IsUpsetHit = true; pick.IsHumillacion = true; pick.IsSomnifero = true; pick.IsEmpateFallido = true;
        pick.IsAutoFilled = true; pick.LastModified = new DateTime(2026, 7, 9, 12, 0, 0, DateTimeKind.Utc);
        _memberList[0].TotalUpsets = 4; _memberList[0].TotalHumillaciones = 2; _memberList[0].BestStreak = 6;
        _memberList[0].User.AvatarUrl = "https://img/alex.png";

        var input = await _sut.BuildEngineInputAsync(QuinielaId, _season, 101);

        input.SeasonId.Should().Be(SeasonId);
        input.SeasonName.Should().Be("Apertura 2026");
        input.TargetMemberId.Should().Be(101);
        var mappedPick = input.Picks.Single(p => p.MemberId == 101 && p.MatchId == derby.Id);
        mappedPick.IsUpsetHit.Should().BeTrue();
        mappedPick.IsHumillacion.Should().BeTrue();
        mappedPick.IsSomnifero.Should().BeTrue();
        mappedPick.IsEmpateFallido.Should().BeTrue();
        mappedPick.IsAutoFilled.Should().BeTrue();
        mappedPick.SubmittedUtc.Should().Be(pick.LastModified!.Value);
        var otherPick = input.Picks.Single(p => p.MemberId == 101 && p.MatchId == _matchList[1].Id);
        otherPick.IsUpsetHit.Should().BeFalse();
        otherPick.SubmittedUtc.Should().Be(_pickList.First(p => p.MemberId == 101 && p.MatchId == _matchList[1].Id).Created);

        var alex = input.Members.Single(m => m.MemberId == 101);
        alex.UserId.Should().Be(OwnerUserId);
        alex.DisplayName.Should().Be("Alex");
        alex.AvatarUrl.Should().Be("https://img/alex.png");
        (alex.TotalPoints, alex.TotalHits, alex.TotalUpsets, alex.TotalHumillaciones, alex.BestStreak).Should().Be((5, 5, 4, 2, 6));
        input.Members.Single(m => m.MemberId == 102).DisplayName.Should().Be("Fernando"); // alias vacio => DisplayName

        input.Matches.Single(m => m.MatchId == derby.Id).DerbyName.Should().Be("El Clásico Nacional");
        input.Matches.Single(m => m.MatchId == _matchList[1].Id).DerbyName.Should().Be("El Clásico Regio");
        input.Matches.Single(m => m.MatchId == _matchList[2].Id).DerbyName.Should().Be("El Clásico del Bajío");
        input.Matches.Single(m => m.MatchId == derby.Id).WeekNumber.Should().Be(1);
    }

    #endregion
}

