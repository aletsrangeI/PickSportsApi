using Domain.Entities;
using Domain.Enums;
using DTO.Quiniela;
using FluentAssertions;
using Interface.Mapping;
using Interface.Persistence;
using Moq;
using UseCases.Common.Mapping;
using UseCases.Quinielas;
using Validator.Quiniela;

namespace PickSportsApi.UnitTests.UseCasesTests;

public class QuinielaApplicationSeasonTests
{
    private const int QuinielaId = 50;
    private const int OwnerId = 1;
    private const int LeagueId = 10;
    private const int OldSeasonId = 2;
    private const int NextSeasonId = 3;

    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly IAppMapper _mapper = new AppMapper();

    private QuinielaApplication CreateApp() =>
        new(_uow.Object, _mapper, new CreateQuinielaDtoValidator(), new JoinQuinielaDtoValidator());

    private static Quiniela BuildQuiniela(Action<Quiniela>? tweak = null)
    {
        var q = new Quiniela
        {
            Id = QuinielaId,
            Name = "Liga MX Clausura",
            Description = "desc",
            QuinielaType = QuinielaType.Sports,
            LeagueId = LeagueId,
            SeasonId = OldSeasonId,
            OwnerId = OwnerId,
            InviteCode = "MX-2026-AA",
            EntryFee = 100m,
            FirstPlacePct = 60m,
            SecondPlacePct = 30m,
            ThirdPlacePct = 10m,
            IsActive = true
        };
        tweak?.Invoke(q);
        return q;
    }

    /// <summary>Configura un escenario válido (todas las validaciones 1-7 pasan).</summary>
    private Quiniela SetupValidScenario(List<QuinielaMember>? members = null, Action<Quiniela>? tweak = null)
    {
        var quiniela = BuildQuiniela(tweak);
        _uow.Setup(u => u.Quinielas.GetAsync(QuinielaId)).ReturnsAsync(quiniela);
        _uow.Setup(u => u.QuinielaMembers.GetMembershipAsync(QuinielaId, OwnerId))
            .ReturnsAsync(new QuinielaMember { QuinielaId = QuinielaId, UserId = OwnerId, Role = "OWNER", Active = true });
        _uow.Setup(u => u.Seasons.GetCurrentSeasonAsync(LeagueId))
            .ReturnsAsync(new Season { Id = NextSeasonId, LeagueId = LeagueId, Name = "Apertura 2026" });
        _uow.Setup(u => u.Quinielas.GetByLeagueIdAsync(LeagueId)).ReturnsAsync(new List<Quiniela> { quiniela });
        _uow.Setup(u => u.Leagues.GetAsync(LeagueId)).ReturnsAsync(new League { Id = LeagueId, Code = "mex.1", Name = "Liga MX", Active = true });
        _uow.Setup(u => u.Quinielas.GetByInviteCodeAsync(It.IsAny<string>())).ReturnsAsync((Quiniela?)null);
        _uow.Setup(u => u.QuinielaMembers.GetMembersAsync(QuinielaId))
            .ReturnsAsync(members ?? new List<QuinielaMember>());
        return quiniela;
    }

    // ---------------- CreateAsync ----------------

    [Fact]
    public async Task CreateAsync_SportsQuiniela_AssignsCurrentSeasonOfLeague()
    {
        // Arrange
        var user = new User { Id = 1, Username = "alex", Active = true };
        var league = new League { Id = LeagueId, Code = "mex.1", Name = "Liga MX", Active = true };
        _uow.Setup(u => u.Users.GetAsync(1)).ReturnsAsync(user);
        _uow.Setup(u => u.Leagues.GetAsync(LeagueId)).ReturnsAsync(league);
        _uow.Setup(u => u.Seasons.GetCurrentSeasonAsync(LeagueId)).ReturnsAsync(new Season { Id = NextSeasonId, Name = "Apertura 2026" });
        _uow.Setup(u => u.Quinielas.GetByInviteCodeAsync(It.IsAny<string>())).ReturnsAsync((Quiniela?)null);
        Quiniela? saved = null;
        _uow.Setup(u => u.Quinielas.InsertAsync(It.IsAny<Quiniela>()))
            .Callback<Quiniela>(q => { saved = q; q.Id = 99; }).ReturnsAsync(true);
        _uow.Setup(u => u.QuinielaMembers.InsertAsync(It.IsAny<QuinielaMember>())).ReturnsAsync(true);
        _uow.Setup(u => u.Quinielas.GetWithDetailsAsync(99)).ReturnsAsync((Quiniela?)null);

        // Act
        var result = await CreateApp().CreateAsync(1, new CreateQuinielaDto
        {
            Name = "Mi quiniela", LeagueId = LeagueId, EntryFee = 0m,
            FirstPlacePct = 70m, SecondPlacePct = 20m, ThirdPlacePct = 10m
        });

        // Assert
        result.isSuccess.Should().BeTrue();
        saved.Should().NotBeNull();
        saved!.SeasonId.Should().Be(NextSeasonId);
    }

    [Fact]
    public async Task CreateAsync_LeagueWithoutCurrentSeason_LeavesSeasonNull()
    {
        // Arrange
        _uow.Setup(u => u.Users.GetAsync(1)).ReturnsAsync(new User { Id = 1, Username = "alex", Active = true });
        _uow.Setup(u => u.Leagues.GetAsync(LeagueId)).ReturnsAsync(new League { Id = LeagueId, Code = "mex.1", Active = true });
        _uow.Setup(u => u.Seasons.GetCurrentSeasonAsync(LeagueId)).ReturnsAsync((Season?)null);
        _uow.Setup(u => u.Quinielas.GetByInviteCodeAsync(It.IsAny<string>())).ReturnsAsync((Quiniela?)null);
        Quiniela? saved = null;
        _uow.Setup(u => u.Quinielas.InsertAsync(It.IsAny<Quiniela>()))
            .Callback<Quiniela>(q => { saved = q; q.Id = 99; }).ReturnsAsync(true);
        _uow.Setup(u => u.QuinielaMembers.InsertAsync(It.IsAny<QuinielaMember>())).ReturnsAsync(true);

        // Act
        var result = await CreateApp().CreateAsync(1, new CreateQuinielaDto
        {
            Name = "Mi quiniela", LeagueId = LeagueId, FirstPlacePct = 70m, SecondPlacePct = 20m, ThirdPlacePct = 10m
        });

        // Assert
        result.isSuccess.Should().BeTrue();
        saved!.SeasonId.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_CustomEvents_LeavesSeasonNullAndNeverQueriesSeasons()
    {
        // Arrange
        _uow.Setup(u => u.Users.GetAsync(1)).ReturnsAsync(new User { Id = 1, Username = "alex", Active = true });
        _uow.Setup(u => u.Quinielas.GetByInviteCodeAsync(It.IsAny<string>())).ReturnsAsync((Quiniela?)null);
        Quiniela? saved = null;
        _uow.Setup(u => u.Quinielas.InsertAsync(It.IsAny<Quiniela>()))
            .Callback<Quiniela>(q => { saved = q; q.Id = 99; }).ReturnsAsync(true);
        _uow.Setup(u => u.QuinielaMembers.InsertAsync(It.IsAny<QuinielaMember>())).ReturnsAsync(true);
        _uow.Setup(u => u.CustomEvents.GetOrCreateEventGroupForQuinielaAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(new EventGroup());

        // Act
        var result = await CreateApp().CreateAsync(1, new CreateQuinielaDto
        {
            Name = "Evento", QuinielaType = "CustomEvents", FirstPlacePct = 70m, SecondPlacePct = 20m, ThirdPlacePct = 10m
        });

        // Assert
        result.isSuccess.Should().BeTrue();
        saved!.QuinielaType.Should().Be(QuinielaType.CustomEvents);
        saved.SeasonId.Should().BeNull();
        _uow.Verify(u => u.Seasons.GetCurrentSeasonAsync(It.IsAny<int>()), Times.Never);
    }

    // ---------------- StartNextSeasonAsync: validaciones ----------------

    [Fact]
    public async Task StartNextSeason_QuinielaNotFound_Fails()
    {
        // Arrange
        _uow.Setup(u => u.Quinielas.GetAsync(QuinielaId)).ReturnsAsync((Quiniela?)null);

        // Act
        var result = await CreateApp().StartNextSeasonAsync(QuinielaId, OwnerId, new StartNextSeasonDto());

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Be("La quiniela no existe.");
        _uow.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StartNextSeason_CustomEvents_Fails()
    {
        // Arrange
        SetupValidScenario(tweak: q => q.QuinielaType = QuinielaType.CustomEvents);

        // Act
        var result = await CreateApp().StartNextSeasonAsync(QuinielaId, OwnerId, new StartNextSeasonDto());

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Be("Las quinielas de eventos personalizados no tienen temporadas.");
    }

    [Fact]
    public async Task StartNextSeason_PlainMember_Fails()
    {
        // Arrange
        SetupValidScenario();
        _uow.Setup(u => u.QuinielaMembers.GetMembershipAsync(QuinielaId, 7))
            .ReturnsAsync(new QuinielaMember { UserId = 7, Role = "MEMBER", Active = true });
        _uow.Setup(u => u.Users.GetAsync(7)).ReturnsAsync(new User { Id = 7, Role = "USER", Active = true });

        // Act
        var result = await CreateApp().StartNextSeasonAsync(QuinielaId, 7, new StartNextSeasonDto());

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Be("Solo el Owner o un Admin puede abrir la nueva temporada.");
    }

    [Fact]
    public async Task StartNextSeason_SystemAdminWithoutMembership_IsAllowed()
    {
        // Arrange
        SetupValidScenario();
        _uow.Setup(u => u.QuinielaMembers.GetMembershipAsync(QuinielaId, 9)).ReturnsAsync((QuinielaMember?)null);
        _uow.Setup(u => u.Users.GetAsync(9)).ReturnsAsync(new User { Id = 9, Role = "ADMIN", Active = true });
        _uow.Setup(u => u.Quinielas.InsertAsync(It.IsAny<Quiniela>())).ReturnsAsync(true);
        _uow.Setup(u => u.Quinielas.UpdateAsync(It.IsAny<Quiniela>())).ReturnsAsync(true);

        // Act
        var result = await CreateApp().StartNextSeasonAsync(QuinielaId, 9, new StartNextSeasonDto());

        // Assert
        result.isSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task StartNextSeason_AlreadyArchived_Fails()
    {
        // Arrange
        SetupValidScenario(tweak: q => { q.IsActive = false; q.ArchivedAt = DateTime.UtcNow; });

        // Act
        var result = await CreateApp().StartNextSeasonAsync(QuinielaId, OwnerId, new StartNextSeasonDto());

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Be("Esta quiniela ya está archivada.");
    }

    [Fact]
    public async Task StartNextSeason_WithoutSeasonAssigned_Fails()
    {
        // Arrange
        SetupValidScenario(tweak: q => q.SeasonId = null);

        // Act
        var result = await CreateApp().StartNextSeasonAsync(QuinielaId, OwnerId, new StartNextSeasonDto());

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Be("La quiniela no tiene temporada asignada.");
    }

    [Fact]
    public async Task StartNextSeason_NoCurrentSeasonInLeague_Fails()
    {
        // Arrange
        SetupValidScenario();
        _uow.Setup(u => u.Seasons.GetCurrentSeasonAsync(LeagueId)).ReturnsAsync((Season?)null);

        // Act
        var result = await CreateApp().StartNextSeasonAsync(QuinielaId, OwnerId, new StartNextSeasonDto());

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Be("Aún no existe la siguiente temporada de esta liga.");
    }

    [Fact]
    public async Task StartNextSeason_CurrentSeasonIsSameAsQuinielaSeason_Fails()
    {
        // Arrange
        SetupValidScenario();
        _uow.Setup(u => u.Seasons.GetCurrentSeasonAsync(LeagueId))
            .ReturnsAsync(new Season { Id = OldSeasonId, LeagueId = LeagueId, Name = "Clausura 2026" });

        // Act
        var result = await CreateApp().StartNextSeasonAsync(QuinielaId, OwnerId, new StartNextSeasonDto());

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Be("Aún no existe la siguiente temporada de esta liga.");
    }

    [Fact]
    public async Task StartNextSeason_DuplicateForNextSeasonAndOwner_Fails()
    {
        // Arrange
        var quiniela = SetupValidScenario();
        var existing = BuildQuiniela(q => { q.Id = 77; q.SeasonId = NextSeasonId; });
        _uow.Setup(u => u.Quinielas.GetByLeagueIdAsync(LeagueId)).ReturnsAsync(new List<Quiniela> { quiniela, existing });

        // Act
        var result = await CreateApp().StartNextSeasonAsync(QuinielaId, OwnerId, new StartNextSeasonDto());

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Be("Ya existe la quiniela de la siguiente temporada.");
        _uow.Verify(u => u.Quinielas.InsertAsync(It.IsAny<Quiniela>()), Times.Never);
    }

    [Fact]
    public async Task StartNextSeason_NextSeasonQuinielaOfAnotherOwner_DoesNotBlock()
    {
        // Arrange
        var quiniela = SetupValidScenario();
        var otherOwners = BuildQuiniela(q => { q.Id = 77; q.SeasonId = NextSeasonId; q.OwnerId = 55; });
        _uow.Setup(u => u.Quinielas.GetByLeagueIdAsync(LeagueId)).ReturnsAsync(new List<Quiniela> { quiniela, otherOwners });
        _uow.Setup(u => u.Quinielas.InsertAsync(It.IsAny<Quiniela>())).ReturnsAsync(true);
        _uow.Setup(u => u.Quinielas.UpdateAsync(It.IsAny<Quiniela>())).ReturnsAsync(true);

        // Act
        var result = await CreateApp().StartNextSeasonAsync(QuinielaId, OwnerId, new StartNextSeasonDto());

        // Assert
        result.isSuccess.Should().BeTrue();
    }

    // ---------------- StartNextSeasonAsync: caso feliz ----------------

    [Fact]
    public async Task StartNextSeason_Valid_CopiesMembersResetsStatsAndArchivesPreviousLast()
    {
        // Arrange
        var members = new List<QuinielaMember>
        {
            new() { Id = 1, QuinielaId = QuinielaId, UserId = 1, Alias = "Alex", Role = "OWNER", PaidFee = true,
                    TotalPoints = 50, TotalHits = 40, TotalUpsets = 5, TotalHumillaciones = 2, CurrentStreak = 3, BestStreak = 7, Active = true },
            new() { Id = 2, QuinielaId = QuinielaId, UserId = 2, Alias = "Beto", Role = "ADMIN", PaidFee = true,
                    TotalPoints = 30, TotalHits = 20, TotalUpsets = 1, TotalHumillaciones = 0, CurrentStreak = 1, BestStreak = 4, Active = true },
            new() { Id = 3, QuinielaId = QuinielaId, UserId = 3, Alias = "Chuy", Role = "MEMBER", PaidFee = false,
                    TotalPoints = 10, TotalHits = 9, Active = true },
            new() { Id = 4, QuinielaId = QuinielaId, UserId = 4, Alias = "Inactivo", Role = "MEMBER", Active = false }
        };
        var previous = SetupValidScenario(members);

        var calls = new List<string>();
        Quiniela? created = null;
        var insertedMembers = new List<QuinielaMember>();
        Quiniela? archived = null;

        _uow.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("begin")).Returns(Task.CompletedTask);
        _uow.Setup(u => u.Quinielas.InsertAsync(It.IsAny<Quiniela>()))
            .Callback<Quiniela>(q => { created = q; q.Id = 200; calls.Add("insert-quiniela"); })
            .ReturnsAsync(true);
        _uow.Setup(u => u.QuinielaMembers.InsertAsync(It.IsAny<QuinielaMember>()))
            .Callback<QuinielaMember>(m => { insertedMembers.Add(m); calls.Add("insert-member"); })
            .ReturnsAsync(true);
        _uow.Setup(u => u.Quinielas.UpdateAsync(It.IsAny<Quiniela>()))
            .Callback<Quiniela>(q => { archived = q; calls.Add("archive"); })
            .ReturnsAsync(true);
        _uow.Setup(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("commit")).Returns(Task.CompletedTask);

        // Act
        var result = await CreateApp().StartNextSeasonAsync(QuinielaId, OwnerId, new StartNextSeasonDto { Name = "  Apertura 2026  " });

        // Assert: respuesta
        result.isSuccess.Should().BeTrue();
        result.Message.Should().Be("Nueva temporada creada exitosamente.");
        result.Data.Should().NotBeNull();
        result.Data.UserRole.Should().Be("OWNER");
        result.Data.MembersCount.Should().Be(3);
        result.Data.SeasonId.Should().Be(NextSeasonId);
        result.Data.Name.Should().Be("Apertura 2026");

        // Assert: quiniela nueva
        created.Should().NotBeNull();
        created!.SeasonId.Should().Be(NextSeasonId);
        created.IsActive.Should().BeTrue();
        created.ArchivedAt.Should().BeNull();
        created.OwnerId.Should().Be(OwnerId);
        created.LeagueId.Should().Be(LeagueId);
        created.EntryFee.Should().Be(100m);
        created.FirstPlacePct.Should().Be(60m);
        created.SecondPlacePct.Should().Be(30m);
        created.ThirdPlacePct.Should().Be(10m);
        created.InviteCode.Should().NotBe("MX-2026-AA");
        created.InviteCode.Should().StartWith("MX-");

        // Assert: miembros copiados y en cero
        insertedMembers.Should().HaveCount(3);
        insertedMembers.Select(m => (m.UserId, m.Alias, m.Role)).Should().BeEquivalentTo(new[]
        {
            (1, "Alex", "OWNER"), (2, "Beto", "ADMIN"), (3, "Chuy", "MEMBER")
        });
        insertedMembers.Should().OnlyContain(m =>
            m.QuinielaId == 200 && m.PaidFee == false && m.TotalHits == 0 && m.TotalUpsets == 0 &&
            m.TotalHumillaciones == 0 && m.TotalPoints == 0 && m.CurrentStreak == 0 && m.BestStreak == 0 && m.Active);

        // Assert: anterior archivada
        archived.Should().BeSameAs(previous);
        previous.IsActive.Should().BeFalse();
        previous.ArchivedAt.Should().NotBeNull();

        // Assert: orden exacto (archivar es lo ultimo antes del commit)
        calls.Should().Equal("begin", "insert-quiniela", "insert-member", "insert-member", "insert-member", "archive", "commit");
        _uow.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StartNextSeason_EmptyName_KeepsPreviousName()
    {
        // Arrange
        SetupValidScenario();
        Quiniela? created = null;
        _uow.Setup(u => u.Quinielas.InsertAsync(It.IsAny<Quiniela>()))
            .Callback<Quiniela>(q => created = q).ReturnsAsync(true);
        _uow.Setup(u => u.Quinielas.UpdateAsync(It.IsAny<Quiniela>())).ReturnsAsync(true);

        // Act
        var result = await CreateApp().StartNextSeasonAsync(QuinielaId, OwnerId, new StartNextSeasonDto { Name = null });

        // Assert
        result.isSuccess.Should().BeTrue();
        created!.Name.Should().Be("Liga MX Clausura");
    }

    // ---------------- StartNextSeasonAsync: fallo a medias ----------------

    [Fact]
    public async Task StartNextSeason_MemberInsertFails_RollsBackAndDoesNotArchivePrevious()
    {
        // Arrange
        var members = new List<QuinielaMember>
        {
            new() { UserId = 1, Alias = "Alex", Role = "OWNER", Active = true },
            new() { UserId = 2, Alias = "Beto", Role = "MEMBER", Active = true }
        };
        var previous = SetupValidScenario(members);
        _uow.Setup(u => u.Quinielas.InsertAsync(It.IsAny<Quiniela>()))
            .Callback<Quiniela>(q => q.Id = 200).ReturnsAsync(true);
        _uow.SetupSequence(u => u.QuinielaMembers.InsertAsync(It.IsAny<QuinielaMember>()))
            .ReturnsAsync(true)
            .ThrowsAsync(new InvalidOperationException("boom"));

        // Act
        var result = await CreateApp().StartNextSeasonAsync(QuinielaId, OwnerId, new StartNextSeasonDto());

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Data.Should().BeNull();
        previous.IsActive.Should().BeTrue();
        previous.ArchivedAt.Should().BeNull();
        _uow.Verify(u => u.Quinielas.UpdateAsync(It.IsAny<Quiniela>()), Times.Never);
        _uow.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StartNextSeason_ArchiveUpdateFails_RollsBackAndReportsFailure()
    {
        // Arrange
        SetupValidScenario();
        _uow.Setup(u => u.Quinielas.InsertAsync(It.IsAny<Quiniela>())).ReturnsAsync(true);
        _uow.Setup(u => u.Quinielas.UpdateAsync(It.IsAny<Quiniela>())).ReturnsAsync(false);

        // Act
        var result = await CreateApp().StartNextSeasonAsync(QuinielaId, OwnerId, new StartNextSeasonDto());

        // Assert
        result.isSuccess.Should().BeFalse();
        _uow.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------------- Descripción, respuesta completa y asignación perezosa ----------------

    [Fact]
    public async Task StartNextSeasonAsync_DoesNotCopyDescription_AndReturnsReloadedQuiniela()
    {
        // Arrange
        SetupValidScenario(new List<QuinielaMember>
        {
            new() { Id = 1, QuinielaId = QuinielaId, UserId = OwnerId, Alias = "Alex", Role = "OWNER", Active = true }
        });
        Quiniela? inserted = null;
        _uow.Setup(u => u.Quinielas.InsertAsync(It.IsAny<Quiniela>()))
            .Callback<Quiniela>(q => { inserted = q; q.Id = 77; }).ReturnsAsync(true);
        _uow.Setup(u => u.QuinielaMembers.InsertAsync(It.IsAny<QuinielaMember>())).ReturnsAsync(true);
        _uow.Setup(u => u.Quinielas.UpdateAsync(It.IsAny<Quiniela>())).ReturnsAsync(true);
        var reloaded = BuildQuiniela(q =>
        {
            q.Id = 77;
            q.SeasonId = NextSeasonId;
            q.Season = new Season { Id = NextSeasonId, Name = "Clausura 2027" };
            q.League = new League { Id = LeagueId, Code = "mex.1", Name = "Liga MX", Sport = new Sport { Name = "Soccer" } };
            q.Owner = new User { Id = OwnerId, Username = "alex", DisplayName = "Alex" };
        });
        _uow.Setup(u => u.Quinielas.GetWithDetailsAsync(77)).ReturnsAsync(reloaded);

        // Act
        var result = await CreateApp().StartNextSeasonAsync(QuinielaId, OwnerId, new StartNextSeasonDto());

        // Assert
        result.isSuccess.Should().BeTrue();
        inserted!.Description.Should().BeNull();
        result.Data.SportName.Should().Be("Soccer");
        result.Data.SeasonName.Should().Be("Clausura 2027");
        result.Data.OwnerName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetUserQuinielasAsync_QuinielaWithoutSeason_GetsLeagueCurrentSeasonAndPersistsIt()
    {
        // Arrange
        var sinTemporada = BuildQuiniela(q => { q.SeasonId = null; q.Members = new List<QuinielaMember> { new() { UserId = OwnerId, Role = "OWNER", Active = true } }; });
        _uow.Setup(u => u.Quinielas.GetUserQuinielasAsync(OwnerId)).ReturnsAsync(new List<Quiniela> { sinTemporada });
        _uow.Setup(u => u.Seasons.GetCurrentSeasonAsync(LeagueId)).ReturnsAsync(new Season { Id = NextSeasonId, Name = "Clausura 2027" });
        _uow.Setup(u => u.Quinielas.UpdateAsync(It.IsAny<Quiniela>())).ReturnsAsync(true);

        // Act
        var result = await CreateApp().GetUserQuinielasAsync(OwnerId);

        // Assert
        result.Data.Single().SeasonId.Should().Be(NextSeasonId);
        result.Data.Single().SeasonName.Should().Be("Clausura 2027");
        _uow.Verify(u => u.Quinielas.UpdateAsync(It.Is<Quiniela>(q => q.SeasonId == NextSeasonId)), Times.Once);
    }

    [Fact]
    public async Task GetUserQuinielasAsync_QuinielaWithoutSeason_LeagueWithoutCurrentSeason_StaysNull()
    {
        // Arrange
        var sinTemporada = BuildQuiniela(q => { q.SeasonId = null; q.Members = new List<QuinielaMember> { new() { UserId = OwnerId, Role = "OWNER", Active = true } }; });
        _uow.Setup(u => u.Quinielas.GetUserQuinielasAsync(OwnerId)).ReturnsAsync(new List<Quiniela> { sinTemporada });
        _uow.Setup(u => u.Seasons.GetCurrentSeasonAsync(LeagueId)).ReturnsAsync((Season?)null);

        // Act
        var result = await CreateApp().GetUserQuinielasAsync(OwnerId);

        // Assert
        result.Data.Single().SeasonId.Should().BeNull();
        _uow.Verify(u => u.Quinielas.UpdateAsync(It.IsAny<Quiniela>()), Times.Never);
    }

    [Fact]
    public async Task GetUserQuinielasAsync_QuinielaWithSeason_IsNotTouched()
    {
        // Arrange
        var conTemporada = BuildQuiniela(q => q.Members = new List<QuinielaMember> { new() { UserId = OwnerId, Role = "OWNER", Active = true } });
        _uow.Setup(u => u.Quinielas.GetUserQuinielasAsync(OwnerId)).ReturnsAsync(new List<Quiniela> { conTemporada });

        // Act
        await CreateApp().GetUserQuinielasAsync(OwnerId);

        // Assert
        _uow.Verify(u => u.Seasons.GetCurrentSeasonAsync(It.IsAny<int>()), Times.Never);
        _uow.Verify(u => u.Quinielas.UpdateAsync(It.IsAny<Quiniela>()), Times.Never);
    }

    // ---------------- GetByIdAsync ----------------

    [Fact]
    public async Task GetByIdAsync_ArchivedQuiniela_IsReturnedWithArchivedFlag()
    {
        // Arrange
        var archived = BuildQuiniela(q =>
        {
            q.IsActive = false;
            q.ArchivedAt = DateTime.UtcNow;
            q.Season = new Season { Id = OldSeasonId, Name = "Clausura 2026" };
            q.Owner = new User { Id = OwnerId, Username = "alex" };
        });
        _uow.Setup(u => u.Quinielas.GetWithDetailsAsync(QuinielaId)).ReturnsAsync(archived);

        // Act
        var result = await CreateApp().GetByIdAsync(QuinielaId, OwnerId);

        // Assert
        result.isSuccess.Should().BeTrue();
        result.Data.IsArchived.Should().BeTrue();
        result.Data.SeasonId.Should().Be(OldSeasonId);
        result.Data.SeasonName.Should().Be("Clausura 2026");
    }

    [Fact]
    public async Task GetByIdAsync_ArchivedQuiniela_UserWhoIsNotMemberNorOwner_IsNotFound()
    {
        // Arrange
        var archived = BuildQuiniela(q =>
        {
            q.IsActive = false;
            q.ArchivedAt = DateTime.UtcNow;
            q.Owner = new User { Id = OwnerId, Username = "alex" };
        });
        _uow.Setup(u => u.Quinielas.GetWithDetailsAsync(QuinielaId)).ReturnsAsync(archived);

        // Act
        var result = await CreateApp().GetByIdAsync(QuinielaId, 9999);

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Data.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_HiddenQuiniela_IsNotFound()
    {
        // Arrange (IsActive=false y ArchivedAt=null significa oculta)
        var hidden = BuildQuiniela(q => q.IsActive = false);
        _uow.Setup(u => u.Quinielas.GetWithDetailsAsync(QuinielaId)).ReturnsAsync(hidden);

        // Act
        var result = await CreateApp().GetByIdAsync(QuinielaId, OwnerId);

        // Assert
        result.isSuccess.Should().BeFalse();
        result.Message.Should().Be("La quiniela no existe o está inactiva.");
    }
}
