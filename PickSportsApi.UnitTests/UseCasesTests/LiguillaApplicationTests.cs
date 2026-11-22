using Domain.Common;
using Domain.Entities;
using DTO.Liguilla;
using FluentAssertions;
using Interface.Persistence;
using Interface.UseCases.Knockout;
using Moq;
using UseCases.Liguilla;
using UseCases.Liguilla.Strategies;
using Xunit;

namespace PickSportsApi.UnitTests.UseCasesTests;

public class LiguillaApplicationTests
{
    private const int QuinielaId = 1;
    private const int LeagueId = 10;
    private const int SeasonId = 20;
    private const int UserId = 50;

    private readonly Mock<IUnitOfWork> _mockUow = new();
    private readonly Mock<IKnockoutSimulationService> _mockSimulation = new();
    private readonly LiguillaApplication _sut;

    public LiguillaApplicationTests()
    {
        _sut = new LiguillaApplication(
            _mockUow.Object,
            new KnockoutStrategyResolver(new IKnockoutFormatStrategy[] { new FmfLiguillaStrategy() }),
            _mockSimulation.Object);

        // Por defecto el usuario es miembro activo de la quiniela; las pruebas de rechazo lo sobrescriben con null.
        _mockUow.Setup(u => u.QuinielaMembers.GetMembershipAsync(QuinielaId, UserId))
            .ReturnsAsync(new QuinielaMember { QuinielaId = QuinielaId, UserId = UserId, Alias = "Alex", Active = true });
    }

    [Fact]
    public async Task SubmitBracket_WhenUserIsNotMember_ThrowsAndPersistsNothing()
    {
        // Arrange
        _mockUow.Setup(u => u.QuinielaMembers.GetMembershipAsync(QuinielaId, UserId))
            .ReturnsAsync((QuinielaMember?)null);
        var dto = new SubmitLiguillaBracketDto
        {
            QfWinner1TeamId = 1, QfWinner2TeamId = 2, QfWinner3TeamId = 3, QfWinner4TeamId = 4,
            SfWinner1TeamId = 1, SfWinner2TeamId = 2, ChampionTeamId = 1
        };

        // Act
        var act = () => _sut.SubmitBracketAsync(QuinielaId, UserId, dto);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("No perteneces a esta quiniela.");
        _mockUow.Verify(u => u.Liguilla.AddBracketAsync(It.IsAny<LiguillaBracket>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockUow.Verify(u => u.Liguilla.ReplacePicksAsync(It.IsAny<int>(), It.IsAny<IReadOnlyList<LiguillaBracketPick>>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockUow.Verify(u => u.Save(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SubmitPicks_WhenUserIsNotMember_ThrowsAndPersistsNothing()
    {
        // Arrange
        _mockUow.Setup(u => u.QuinielaMembers.GetMembershipAsync(QuinielaId, UserId))
            .ReturnsAsync((QuinielaMember?)null);
        var picks = new Dictionary<string, IReadOnlyList<int>> { ["QF"] = new[] { 1, 2, 3, 4 } };

        // Act
        var act = () => _sut.SubmitPicksAsync(QuinielaId, UserId, picks);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("No perteneces a esta quiniela.");
        _mockUow.Verify(u => u.Liguilla.AddBracketAsync(It.IsAny<LiguillaBracket>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockUow.Verify(u => u.Liguilla.ReplacePicksAsync(It.IsAny<int>(), It.IsAny<IReadOnlyList<LiguillaBracketPick>>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockUow.Verify(u => u.Save(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static League FmfLeague() => new() { Id = LeagueId, KnockoutFormatCode = "FMF_LIGUILLA", WeeksCount = 17 };

    [Fact]
    public async Task GetOverview_WhenRegularSeasonNotScoredAndNotAdmin_ReturnsHiddenAndUnavailable()
    {
        // Arrange: Torneo va en J12 (no todas <= 17 están SCORED)
        var quiniela = new Quiniela { Id = QuinielaId, League = FmfLeague(), LeagueId = LeagueId, Name = "Quiniela Amigos" };
        var season = new Season { Id = SeasonId, LeagueId = LeagueId, IsCurrent = true };
        var weeks = new List<Week>
        {
            new() { SeasonId = SeasonId, WeekNumber = 12, Status = "PUBLISHED" }
        };

        _mockUow.Setup(u => u.Quinielas.GetAsync(QuinielaId)).ReturnsAsync(quiniela);
        _mockUow.Setup(u => u.Seasons.GetCurrentSeasonAsync(LeagueId)).ReturnsAsync(season);
        _mockUow.Setup(u => u.Weeks.GetBySeasonIdAsync(SeasonId)).ReturnsAsync(weeks);
        _mockUow.Setup(u => u.Liguilla.GetNodesBySeasonAsync(SeasonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LiguillaNode>());

        // Act (usuario no admin)
        var result = await _sut.GetOverviewAsync(QuinielaId, UserId, isAdmin: false);

        // Assert
        result.IsAvailable.Should().BeFalse();
        result.Status.Should().Be("HIDDEN");
    }

    [Fact]
    public async Task GetOverview_WhenAdminPreview_ReturnsIsAvailableTrue()
    {
        // Arrange: Torneo va en J12 pero el usuario es Administrador
        var quiniela = new Quiniela { Id = QuinielaId, League = FmfLeague(), LeagueId = LeagueId, Name = "Quiniela Amigos" };
        var season = new Season { Id = SeasonId, LeagueId = LeagueId, IsCurrent = true };
        var weeks = new List<Week>
        {
            new() { SeasonId = SeasonId, WeekNumber = 12, Status = "PUBLISHED" }
        };

        _mockUow.Setup(u => u.Quinielas.GetAsync(QuinielaId)).ReturnsAsync(quiniela);
        _mockUow.Setup(u => u.Seasons.GetCurrentSeasonAsync(LeagueId)).ReturnsAsync(season);
        _mockUow.Setup(u => u.Weeks.GetBySeasonIdAsync(SeasonId)).ReturnsAsync(weeks);
        _mockUow.Setup(u => u.Liguilla.GetNodesBySeasonAsync(SeasonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LiguillaNode>());
        _mockUow.Setup(u => u.Liguilla.GetBracketAsync(QuinielaId, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LiguillaBracket?)null);

        // Act (usuario admin)
        var result = await _sut.GetOverviewAsync(QuinielaId, UserId, isAdmin: true);

        // Assert
        result.IsAvailable.Should().BeTrue();
        result.Status.Should().Be("ADMIN_PREVIEW_HIDDEN");
    }

    [Fact]
    public async Task GetOverview_FmfLeague_ReturnsFormatCodeAndThreeRoundsWithPoints()
    {
        // Arrange
        var quiniela = new Quiniela { Id = QuinielaId, League = FmfLeague(), LeagueId = LeagueId, Name = "Quiniela Amigos" };
        var season = new Season { Id = SeasonId, LeagueId = LeagueId, IsCurrent = true };

        _mockUow.Setup(u => u.Quinielas.GetAsync(QuinielaId)).ReturnsAsync(quiniela);
        _mockUow.Setup(u => u.Seasons.GetCurrentSeasonAsync(LeagueId)).ReturnsAsync(season);
        _mockUow.Setup(u => u.Weeks.GetBySeasonIdAsync(SeasonId)).ReturnsAsync(new List<Week>());
        _mockUow.Setup(u => u.Liguilla.GetNodesBySeasonAsync(SeasonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LiguillaNode>());
        _mockUow.Setup(u => u.Liguilla.GetBracketAsync(QuinielaId, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LiguillaBracket?)null);

        // Act
        var result = await _sut.GetOverviewAsync(QuinielaId, UserId, isAdmin: true);

        // Assert
        result.FormatCode.Should().Be("FMF_LIGUILLA");
        result.Rounds.Select(r => r.Code).Should().Equal("QF", "SF", "FINAL");
        result.Rounds.Select(r => r.PointsPerHit).Should().Equal(10, 25, 50);
        result.Rounds.Select(r => r.Order).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task GetOverview_FmfRegularSeasonFinishedAndQuarterFinalsNotReady_ReturnsWaitingField()
    {
        // Arrange: las 17 jornadas regulares puntuadas y ESPN aun no publica los cuartos
        var quiniela = new Quiniela { Id = QuinielaId, League = FmfLeague(), LeagueId = LeagueId, Name = "Quiniela Amigos" };
        var season = new Season { Id = SeasonId, LeagueId = LeagueId, IsCurrent = true };
        var weeks = Enumerable.Range(1, 17)
            .Select(number => new Week { SeasonId = SeasonId, WeekNumber = number, Status = "SCORED" })
            .ToList();

        _mockUow.Setup(u => u.Quinielas.GetAsync(QuinielaId)).ReturnsAsync(quiniela);
        _mockUow.Setup(u => u.Seasons.GetCurrentSeasonAsync(LeagueId)).ReturnsAsync(season);
        _mockUow.Setup(u => u.Weeks.GetBySeasonIdAsync(SeasonId)).ReturnsAsync(weeks);
        _mockUow.Setup(u => u.Liguilla.GetNodesBySeasonAsync(SeasonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LiguillaNode>());
        _mockUow.Setup(u => u.Liguilla.GetBracketAsync(QuinielaId, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LiguillaBracket?)null);

        // Act
        var result = await _sut.GetOverviewAsync(QuinielaId, UserId, isAdmin: true);

        // Assert
        result.Status.Should().Be("WAITING_FIELD");
    }

    [Fact]
    public async Task SeedSimulationNodes_FmfFormat_DelegatesToSimulationServiceWithoutTouchingNodesDirectly()
    {
        // Arrange
        var quiniela = new Quiniela { Id = QuinielaId, League = FmfLeague(), LeagueId = LeagueId };
        _mockUow.Setup(u => u.Quinielas.GetAsync(QuinielaId)).ReturnsAsync(quiniela);
        _mockSimulation.Setup(s => s.SeedAsync(QuinielaId, It.IsAny<CancellationToken>())).ReturnsAsync(7);

        // Act
        var seeded = await _sut.SeedSimulationNodesAsync(QuinielaId);

        // Assert
        seeded.Should().BeTrue();
        _mockSimulation.Verify(s => s.SeedAsync(QuinielaId, It.IsAny<CancellationToken>()), Times.Once);
        _mockUow.Verify(u => u.Liguilla.AddNodeAsync(It.IsAny<LiguillaNode>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockUow.Verify(u => u.Liguilla.UpdateNodeAsync(It.IsAny<LiguillaNode>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetMyBracket_WithGenericPicks_ReturnsPicksByRound()
    {
        // Arrange
        const int bracketId = 99;
        var quiniela = new Quiniela { Id = QuinielaId, League = FmfLeague(), LeagueId = LeagueId };
        var bracket = new LiguillaBracket { Id = bracketId, QuinielaId = QuinielaId, UserId = UserId, IsLocked = true };
        var picks = new List<LiguillaBracketPick>
        {
            new() { BracketId = bracketId, RoundCode = "QF", Slot = 2, TeamId = 12 },
            new() { BracketId = bracketId, RoundCode = "QF", Slot = 1, TeamId = 11 },
            new() { BracketId = bracketId, RoundCode = "FINAL", Slot = 1, TeamId = 11 }
        };

        _mockUow.Setup(u => u.Liguilla.GetBracketAsync(QuinielaId, UserId, It.IsAny<CancellationToken>())).ReturnsAsync(bracket);
        _mockUow.Setup(u => u.Quinielas.GetAsync(QuinielaId)).ReturnsAsync(quiniela);
        _mockUow.Setup(u => u.Seasons.GetCurrentSeasonAsync(LeagueId))
            .ReturnsAsync(new Season { Id = SeasonId, LeagueId = LeagueId, IsCurrent = true });
        _mockUow.Setup(u => u.Liguilla.GetNodesBySeasonAsync(SeasonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LiguillaNode>());
        _mockUow.Setup(u => u.Liguilla.GetPicksByBracketAsync(bracketId, It.IsAny<CancellationToken>())).ReturnsAsync(picks);
        _mockUow.Setup(u => u.Users.GetAsync(UserId)).ReturnsAsync(new User { Id = UserId, Username = "Alex" });
        _mockUow.Setup(u => u.QuinielaMembers.GetMembershipAsync(QuinielaId, UserId))
            .ReturnsAsync(new QuinielaMember { QuinielaId = QuinielaId, UserId = UserId, Alias = "Alex" });

        // Act
        var result = await _sut.GetMyBracketAsync(QuinielaId, UserId);

        // Assert
        result.Should().NotBeNull();
        result!.PicksByRound.Keys.Should().BeEquivalentTo("QF", "FINAL");
        result.PicksByRound["QF"].Should().Equal(11, 12);
        result.PicksByRound["FINAL"].Should().Equal(11);
    }

    [Fact]
    public async Task SubmitBracket_WhenKickoffDeadlineExpired_ThrowsInvalidOperationException()
    {
        // Arrange
        var quiniela = new Quiniela { Id = QuinielaId, League = FmfLeague(), LeagueId = LeagueId };
        var season = new Season { Id = SeasonId, LeagueId = LeagueId, IsCurrent = true };
        var qfNode = new LiguillaNode
        {
            SeasonId = SeasonId,
            Round = "QF",
            NodeNumber = 1,
            KickoffUtc = DateTime.UtcNow.AddMinutes(-5) // Ya inició hace 5 minutos
        };

        _mockUow.Setup(u => u.Quinielas.GetAsync(QuinielaId)).ReturnsAsync(quiniela);
        _mockUow.Setup(u => u.Seasons.GetCurrentSeasonAsync(LeagueId)).ReturnsAsync(season);
        _mockUow.Setup(u => u.Liguilla.GetNodesBySeasonAsync(SeasonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LiguillaNode> { qfNode });

        var dto = new SubmitLiguillaBracketDto
        {
            QfWinner1TeamId = 1,
            QfWinner2TeamId = 2,
            QfWinner3TeamId = 3,
            QfWinner4TeamId = 4,
            SfWinner1TeamId = 1,
            SfWinner2TeamId = 2,
            ChampionTeamId = 1
        };

        // Act
        var act = () => _sut.SubmitBracketAsync(QuinielaId, UserId, dto);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*tiempo límite*ha expirado*");
    }

    [Fact]
    public async Task SubmitBracket_WhenChampionNotFromSelectedFinalists_ThrowsArgumentException()
    {
        // Arrange
        var quiniela = new Quiniela { Id = QuinielaId, League = FmfLeague(), LeagueId = LeagueId };
        var season = new Season { Id = SeasonId, LeagueId = LeagueId, IsCurrent = true };
        var qfNode = new LiguillaNode
        {
            SeasonId = SeasonId,
            Round = "QF",
            NodeNumber = 1,
            KickoffUtc = DateTime.UtcNow.AddDays(1) // Mañana
        };

        _mockUow.Setup(u => u.Quinielas.GetAsync(QuinielaId)).ReturnsAsync(quiniela);
        _mockUow.Setup(u => u.Seasons.GetCurrentSeasonAsync(LeagueId)).ReturnsAsync(season);
        _mockUow.Setup(u => u.Liguilla.GetNodesBySeasonAsync(SeasonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LiguillaNode> { qfNode });

        // DTO inválido: Finalistas son 1 y 2, pero pone como campeón al 3
        var dto = new SubmitLiguillaBracketDto
        {
            QfWinner1TeamId = 1,
            QfWinner2TeamId = 2,
            QfWinner3TeamId = 3,
            QfWinner4TeamId = 4,
            SfWinner1TeamId = 1,
            SfWinner2TeamId = 2,
            ChampionTeamId = 3 // Incoherente
        };

        // Act
        var act = () => _sut.SubmitBracketAsync(QuinielaId, UserId, dto);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Campeón debe ser uno de los dos equipos finalistas*");
    }

    [Fact]
    public async Task SubmitBracket_ConSeasonIdExplicito_UsaLosNodosDeEsaTemporadaYNoLaVigente()
    {
        // Arrange: la quiniela es de la temporada 15 (archivada); la vigente de la liga es la 20
        const int quinielaSeasonId = 15;
        var quiniela = new Quiniela { Id = QuinielaId, League = FmfLeague(), LeagueId = LeagueId, SeasonId = quinielaSeasonId };
        var expiredNode = new LiguillaNode { SeasonId = quinielaSeasonId, Round = "QF", NodeNumber = 1, KickoffUtc = DateTime.UtcNow.AddMinutes(-5) };
        var openNodeOfCurrent = new LiguillaNode { SeasonId = SeasonId, Round = "QF", NodeNumber = 1, KickoffUtc = DateTime.UtcNow.AddDays(1) };

        _mockUow.Setup(u => u.Quinielas.GetAsync(QuinielaId)).ReturnsAsync(quiniela);
        _mockUow.Setup(u => u.Seasons.GetCurrentSeasonAsync(LeagueId))
            .ReturnsAsync(new Season { Id = SeasonId, LeagueId = LeagueId, IsCurrent = true });
        _mockUow.Setup(u => u.Liguilla.GetNodesBySeasonAsync(quinielaSeasonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LiguillaNode> { expiredNode });
        _mockUow.Setup(u => u.Liguilla.GetNodesBySeasonAsync(SeasonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LiguillaNode> { openNodeOfCurrent });

        var dto = new SubmitLiguillaBracketDto
        {
            QfWinner1TeamId = 1, QfWinner2TeamId = 2, QfWinner3TeamId = 3, QfWinner4TeamId = 4,
            SfWinner1TeamId = 1, SfWinner2TeamId = 2, ChampionTeamId = 1
        };

        // Act
        var act = () => _sut.SubmitBracketAsync(QuinielaId, UserId, dto);

        // Assert: expira porque usó la temporada 15, no la vigente (que habría aceptado)
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*tiempo límite*ha expirado*");
        _mockUow.Verify(u => u.Liguilla.GetNodesBySeasonAsync(SeasonId, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetStandings_ConSeasonIdExplicito_UsaNombreYNodosDeLaTemporadaDeLaQuiniela()
    {
        // Arrange
        const int quinielaSeasonId = 15;
        var quiniela = new Quiniela { Id = QuinielaId, League = FmfLeague(), LeagueId = LeagueId, SeasonId = quinielaSeasonId };
        _mockUow.Setup(u => u.Quinielas.GetAsync(QuinielaId)).ReturnsAsync(quiniela);
        _mockUow.Setup(u => u.Seasons.GetAsync(quinielaSeasonId)).ReturnsAsync(new Season { Id = quinielaSeasonId, Name = "Clausura 2026" });
        _mockUow.Setup(u => u.Liguilla.GetNodesBySeasonAsync(quinielaSeasonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LiguillaNode>());
        _mockUow.Setup(u => u.Liguilla.GetAllBracketsForQuinielaAsync(QuinielaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LiguillaBracket>());
        _mockUow.Setup(u => u.QuinielaMembers.GetMembersAsync(QuinielaId)).ReturnsAsync(new List<QuinielaMember>());

        // Act
        var result = await _sut.GetStandingsAsync(QuinielaId);

        // Assert
        result.SeasonName.Should().Be("Clausura 2026");
        _mockUow.Verify(u => u.Seasons.GetCurrentSeasonAsync(It.IsAny<int>()), Times.Never);
    }

    private LiguillaApplication CreateNhlSut() =>
        new(_mockUow.Object, new KnockoutStrategyResolver(new IKnockoutFormatStrategy[] { new NhlBestOfSevenStrategy() }), _mockSimulation.Object);

    private static League NhlLeague() =>
        new() { Id = LeagueId, KnockoutFormatCode = KnockoutFormatCodes.NhlBestOf7, WeeksCount = 1 };

    [Fact]
    public async Task GetOverview_NhlNodesWithoutSeeds_ReturnsTeamsInNodesAndQualifiedTeams()
    {
        // Arrange: la NHL no tiene siembras, pero el campo esta listo y los equipos deben verse
        var quiniela = new Quiniela { Id = QuinielaId, League = NhlLeague(), LeagueId = LeagueId };
        var nodes = Enumerable.Range(1, 8).Select(number => new LiguillaNode
        {
            SeasonId = SeasonId,
            Round = "R1",
            NodeNumber = number,
            TeamAId = number * 2 - 1,
            TeamA = new Team { Id = number * 2 - 1, Name = $"Equipo {number * 2 - 1:00}" },
            TeamBId = number * 2,
            TeamB = new Team { Id = number * 2, Name = $"Equipo {number * 2:00}" },
            KickoffUtc = DateTime.UtcNow.AddDays(1)
        }).ToList();

        _mockUow.Setup(u => u.Quinielas.GetAsync(QuinielaId)).ReturnsAsync(quiniela);
        _mockUow.Setup(u => u.Seasons.GetCurrentSeasonAsync(LeagueId))
            .ReturnsAsync(new Season { Id = SeasonId, LeagueId = LeagueId, IsCurrent = true });
        _mockUow.Setup(u => u.Weeks.GetBySeasonIdAsync(SeasonId))
            .ReturnsAsync(new List<Week> { new() { SeasonId = SeasonId, WeekNumber = 1, Status = "SCORED" } });
        _mockUow.Setup(u => u.Liguilla.GetNodesBySeasonAsync(SeasonId, It.IsAny<CancellationToken>())).ReturnsAsync(nodes);
        _mockUow.Setup(u => u.Liguilla.GetBracketAsync(QuinielaId, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LiguillaBracket?)null);

        // Act
        var result = await CreateNhlSut().GetOverviewAsync(QuinielaId, UserId, isAdmin: false);

        // Assert
        result.Status.Should().Be("OPEN_FOR_PICKS");
        result.Nodes.Should().OnlyContain(n => n.TeamA != null && n.TeamB != null);
        result.QualifiedTeams.Should().HaveCount(16);
        result.QualifiedTeams.Select(t => t.Name).Should().BeInAscendingOrder(StringComparer.Ordinal);
    }

    [Fact]
    public async Task GetStandings_NhlTiedOnTotalAndChampion_LaterRoundPointsBreakTheTieBeforeEarlierOnes()
    {
        // Arrange: ambos suman 80. A: 1 acierto de R2 + 2 de CF. B: 4 de R2 (y se envio antes).
        const int userA = 1;
        const int userB = 2;
        var quiniela = new Quiniela { Id = QuinielaId, League = NhlLeague(), LeagueId = LeagueId, SeasonId = SeasonId };
        var nodes = new List<LiguillaNode>
        {
            ResolvedNode(9, "R2", 1), ResolvedNode(10, "R2", 2), ResolvedNode(11, "R2", 3), ResolvedNode(12, "R2", 4),
            ResolvedNode(13, "CF", 1), ResolvedNode(14, "CF", 3)
        };
        var bracketA = new LiguillaBracket { Id = 101, QuinielaId = QuinielaId, UserId = userA, SubmittedAtUtc = new DateTime(2026, 4, 20, 12, 0, 0, DateTimeKind.Utc) };
        var bracketB = new LiguillaBracket { Id = 102, QuinielaId = QuinielaId, UserId = userB, SubmittedAtUtc = new DateTime(2026, 4, 20, 8, 0, 0, DateTimeKind.Utc) };

        _mockUow.Setup(u => u.Quinielas.GetAsync(QuinielaId)).ReturnsAsync(quiniela);
        _mockUow.Setup(u => u.Seasons.GetAsync(SeasonId)).ReturnsAsync(new Season { Id = SeasonId, Name = "NHL 2026" });
        _mockUow.Setup(u => u.Liguilla.GetNodesBySeasonAsync(SeasonId, It.IsAny<CancellationToken>())).ReturnsAsync(nodes);
        _mockUow.Setup(u => u.Liguilla.GetAllBracketsForQuinielaAsync(QuinielaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LiguillaBracket> { bracketB, bracketA });
        _mockUow.Setup(u => u.QuinielaMembers.GetMembersAsync(QuinielaId)).ReturnsAsync(new List<QuinielaMember>
        {
            new() { QuinielaId = QuinielaId, UserId = userA, Alias = "A" },
            new() { QuinielaId = QuinielaId, UserId = userB, Alias = "B" }
        });
        _mockUow.Setup(u => u.Liguilla.GetPicksByBracketAsync(bracketA.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PicksOf(bracketA.Id, ("R2", new[] { 1, 90, 91, 92 }), ("CF", new[] { 1, 3 })));
        _mockUow.Setup(u => u.Liguilla.GetPicksByBracketAsync(bracketB.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PicksOf(bracketB.Id, ("R2", new[] { 1, 2, 3, 4 }), ("CF", new[] { 90, 91 })));

        // Act
        var result = await CreateNhlSut().GetStandingsAsync(QuinielaId);

        // Assert
        result.Standings.Select(s => s.TotalPoints).Should().Equal(80, 80);
        result.Standings.Select(s => s.Alias).Should().Equal("A", "B");
    }

    [Fact]
    public async Task GetStandings_Nhl_ExposesPointsByRoundOfEachParticipant()
    {
        // Arrange: 2 aciertos de R2 (20 c/u) y ninguno de CF
        var quiniela = new Quiniela { Id = QuinielaId, League = NhlLeague(), LeagueId = LeagueId, SeasonId = SeasonId };
        var nodes = new List<LiguillaNode>
        {
            ResolvedNode(9, "R2", 1), ResolvedNode(10, "R2", 2), ResolvedNode(11, "R2", 3), ResolvedNode(12, "R2", 4)
        };
        var bracket = new LiguillaBracket { Id = 101, QuinielaId = QuinielaId, UserId = UserId, SubmittedAtUtc = DateTime.UtcNow };

        _mockUow.Setup(u => u.Quinielas.GetAsync(QuinielaId)).ReturnsAsync(quiniela);
        _mockUow.Setup(u => u.Seasons.GetAsync(SeasonId)).ReturnsAsync(new Season { Id = SeasonId, Name = "NHL 2026" });
        _mockUow.Setup(u => u.Liguilla.GetNodesBySeasonAsync(SeasonId, It.IsAny<CancellationToken>())).ReturnsAsync(nodes);
        _mockUow.Setup(u => u.Liguilla.GetAllBracketsForQuinielaAsync(QuinielaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LiguillaBracket> { bracket });
        _mockUow.Setup(u => u.QuinielaMembers.GetMembersAsync(QuinielaId)).ReturnsAsync(new List<QuinielaMember>
        {
            new() { QuinielaId = QuinielaId, UserId = UserId, Alias = "A" }
        });
        _mockUow.Setup(u => u.Liguilla.GetPicksByBracketAsync(bracket.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PicksOf(bracket.Id, ("R2", new[] { 1, 2, 90, 91 })));

        // Act
        var result = await CreateNhlSut().GetStandingsAsync(QuinielaId);

        // Assert
        var item = result.Standings.Should().ContainSingle().Subject;
        item.PointsByRound.Should().ContainKey("R2").WhoseValue.Should().Be(40);
        item.PointsByRound.Values.Sum().Should().Be(item.TotalPoints - item.BonusPoints);
    }

    [Fact]
    public async Task SeedSimulationNodes_NonFmfFormat_DelegatesToSimulationService()
    {
        // Arrange
        var quiniela = new Quiniela { Id = QuinielaId, League = NhlLeague(), LeagueId = LeagueId };
        _mockUow.Setup(u => u.Quinielas.GetAsync(QuinielaId)).ReturnsAsync(quiniela);
        _mockSimulation.Setup(s => s.SeedAsync(QuinielaId, It.IsAny<CancellationToken>())).ReturnsAsync(15);

        // Act
        var seeded = await CreateNhlSut().SeedSimulationNodesAsync(QuinielaId);

        // Assert
        seeded.Should().BeTrue();
        _mockSimulation.Verify(s => s.SeedAsync(QuinielaId, It.IsAny<CancellationToken>()), Times.Once);
        _mockUow.Verify(u => u.Liguilla.AddNodeAsync(It.IsAny<LiguillaNode>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetOverview_AdminWithSimulatedNhlFieldBeforeSeasonEnds_IsOpenForPicks()
    {
        // Arrange: la fase regular no termino (jornada sin puntuar) pero el admin sembro el campo
        var quiniela = new Quiniela { Id = QuinielaId, League = NhlLeague(), LeagueId = LeagueId };
        var nodes = Enumerable.Range(1, 8).Select(number => new LiguillaNode
        {
            SeasonId = SeasonId,
            Round = "R1",
            NodeNumber = number,
            TeamAId = number * 2 - 1,
            TeamA = new Team { Id = number * 2 - 1, Name = $"Equipo {number * 2 - 1:00}" },
            TeamBId = number * 2,
            TeamB = new Team { Id = number * 2, Name = $"Equipo {number * 2:00}" },
            KickoffUtc = DateTime.UtcNow.AddDays(7)
        }).ToList();

        _mockUow.Setup(u => u.Quinielas.GetAsync(QuinielaId)).ReturnsAsync(quiniela);
        _mockUow.Setup(u => u.Seasons.GetCurrentSeasonAsync(LeagueId))
            .ReturnsAsync(new Season { Id = SeasonId, LeagueId = LeagueId, IsCurrent = true });
        _mockUow.Setup(u => u.Weeks.GetBySeasonIdAsync(SeasonId))
            .ReturnsAsync(new List<Week> { new() { SeasonId = SeasonId, WeekNumber = 1, Status = "OPEN" } });
        _mockUow.Setup(u => u.Liguilla.GetNodesBySeasonAsync(SeasonId, It.IsAny<CancellationToken>())).ReturnsAsync(nodes);
        _mockUow.Setup(u => u.Liguilla.GetBracketAsync(QuinielaId, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LiguillaBracket?)null);

        // Act
        var result = await CreateNhlSut().GetOverviewAsync(QuinielaId, UserId, isAdmin: true);

        // Assert
        result.Status.Should().Be("OPEN_FOR_PICKS");
        result.FormatCode.Should().Be(KnockoutFormatCodes.NhlBestOf7);
    }

    private void ArrangeNhlSeason(List<LiguillaNode> nodes)
    {
        var quiniela = new Quiniela { Id = QuinielaId, League = NhlLeague(), LeagueId = LeagueId };
        _mockUow.Setup(u => u.Quinielas.GetAsync(QuinielaId)).ReturnsAsync(quiniela);
        _mockUow.Setup(u => u.Seasons.GetCurrentSeasonAsync(LeagueId))
            .ReturnsAsync(new Season { Id = SeasonId, LeagueId = LeagueId, IsCurrent = true });
        _mockUow.Setup(u => u.Weeks.GetBySeasonIdAsync(SeasonId))
            .ReturnsAsync(new List<Week> { new() { SeasonId = SeasonId, WeekNumber = 1, Status = "SCORED" } });
        _mockUow.Setup(u => u.Liguilla.GetNodesBySeasonAsync(SeasonId, It.IsAny<CancellationToken>())).ReturnsAsync(nodes);
        _mockUow.Setup(u => u.Liguilla.GetBracketAsync(QuinielaId, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LiguillaBracket?)null);
    }

    private static List<LiguillaNode> NhlFirstRoundWithoutKickoff(int resolvedCount) =>
        Enumerable.Range(1, 8).Select(number => new LiguillaNode
        {
            SeasonId = SeasonId,
            Round = "R1",
            NodeNumber = number,
            TeamAId = number * 2 - 1,
            TeamA = new Team { Id = number * 2 - 1, Name = $"Equipo {number * 2 - 1:00}" },
            TeamBId = number * 2,
            TeamB = new Team { Id = number * 2, Name = $"Equipo {number * 2:00}" },
            IsResolved = number <= resolvedCount,
            WinnerTeamId = number <= resolvedCount ? number * 2 - 1 : null
        }).ToList();

    [Fact]
    public async Task SubmitPicks_WhenNoKickoffButAFirstRoundNodeIsResolved_ThrowsExpired()
    {
        // Arrange: ningun nodo trae KickoffUtc, pero la primera ronda ya empezo a resolverse
        ArrangeNhlSeason(NhlFirstRoundWithoutKickoff(resolvedCount: 1));
        var picks = new Dictionary<string, IReadOnlyList<int>> { ["R1"] = new[] { 1, 3, 5, 7, 9, 11, 13, 15 } };

        // Act
        var act = () => CreateNhlSut().SubmitPicksAsync(QuinielaId, UserId, picks);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*tiempo límite*ha expirado*");
        _mockUow.Verify(u => u.Liguilla.AddBracketAsync(It.IsAny<LiguillaBracket>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetOverview_WhenNoKickoffButAFirstRoundNodeIsResolved_ReturnsLockedInProgress()
    {
        // Arrange
        ArrangeNhlSeason(NhlFirstRoundWithoutKickoff(resolvedCount: 1));

        // Act
        var result = await CreateNhlSut().GetOverviewAsync(QuinielaId, UserId, isAdmin: false);

        // Assert
        result.Status.Should().Be("LOCKED_IN_PROGRESS");
    }

    [Fact]
    public async Task GetOverview_WhenNoKickoffAndNothingResolved_StaysOpenForPicks()
    {
        // Arrange
        ArrangeNhlSeason(NhlFirstRoundWithoutKickoff(resolvedCount: 0));

        // Act
        var result = await CreateNhlSut().GetOverviewAsync(QuinielaId, UserId, isAdmin: false);

        // Assert
        result.Status.Should().Be("OPEN_FOR_PICKS");
    }

    [Theory]
    [InlineData(99)]
    [InlineData(0)]
    public async Task ResolveNode_WhenWinnerIsNotOneOfTheTeams_ThrowsArgumentExceptionAndChangesNothing(int winnerTeamId)
    {
        // Arrange
        var node = new LiguillaNode { Id = 5, SeasonId = SeasonId, Round = "QF", NodeNumber = 1, TeamAId = 1, TeamBId = 8 };
        var quiniela = new Quiniela { Id = QuinielaId, League = FmfLeague(), LeagueId = LeagueId };
        _mockUow.Setup(u => u.Quinielas.GetAsync(QuinielaId)).ReturnsAsync(quiniela);
        _mockUow.Setup(u => u.Liguilla.GetNodeByIdAsync(node.Id, It.IsAny<CancellationToken>())).ReturnsAsync(node);

        // Act
        var act = () => _sut.ResolveNodeAsync(QuinielaId, new ResolveLiguillaNodeRequestDto { NodeId = node.Id, WinnerTeamId = winnerTeamId });

        // Assert
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("El ganador debe ser uno de los equipos del cruce.");
        node.IsResolved.Should().BeFalse();
        node.WinnerTeamId.Should().BeNull();
        _mockUow.Verify(u => u.Liguilla.UpdateNodeAsync(It.IsAny<LiguillaNode>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockUow.Verify(u => u.Save(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetMyBracket_NhlBracket_ExposesPointsByRound()
    {
        // Arrange: un acierto en R1 (10 puntos) sobre un nodo resuelto
        const int bracketId = 77;
        var bracket = new LiguillaBracket { Id = bracketId, QuinielaId = QuinielaId, UserId = UserId, IsLocked = true };
        _mockUow.Setup(u => u.Liguilla.GetBracketAsync(QuinielaId, UserId, It.IsAny<CancellationToken>())).ReturnsAsync(bracket);
        _mockUow.Setup(u => u.Quinielas.GetAsync(QuinielaId)).ReturnsAsync(new Quiniela { Id = QuinielaId, League = NhlLeague(), LeagueId = LeagueId });
        _mockUow.Setup(u => u.Seasons.GetCurrentSeasonAsync(LeagueId))
            .ReturnsAsync(new Season { Id = SeasonId, LeagueId = LeagueId, IsCurrent = true });
        _mockUow.Setup(u => u.Liguilla.GetNodesBySeasonAsync(SeasonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LiguillaNode> { ResolvedNode(1, "R1", 1) });
        _mockUow.Setup(u => u.Liguilla.GetPicksByBracketAsync(bracketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PicksOf(bracketId, ("R1", new[] { 1 })));
        _mockUow.Setup(u => u.Users.GetAsync(UserId)).ReturnsAsync(new User { Id = UserId, Username = "Alex" });

        // Act
        var result = await CreateNhlSut().GetMyBracketAsync(QuinielaId, UserId);

        // Assert
        result.Should().NotBeNull();
        result!.PointsByRound.Should().ContainKey("R1").WhoseValue.Should().Be(10);
    }

    private static LiguillaNode ResolvedNode(int number, string round, int winnerTeamId) => new()
    {
        SeasonId = SeasonId,
        Round = round,
        NodeNumber = number,
        IsResolved = true,
        WinnerTeamId = winnerTeamId
    };

    private static List<LiguillaBracketPick> PicksOf(int bracketId, params (string Round, int[] Teams)[] rounds) =>
        rounds.SelectMany(r => r.Teams.Select((teamId, index) => new LiguillaBracketPick
        {
            BracketId = bracketId,
            RoundCode = r.Round,
            Slot = index + 1,
            TeamId = teamId
        })).ToList();

    [Fact]
    public async Task SubmitBracket_WhenValid_PersistsAndReturnsDetail()
    {
        // Arrange
        var quiniela = new Quiniela { Id = QuinielaId, League = FmfLeague(), LeagueId = LeagueId };
        var season = new Season { Id = SeasonId, LeagueId = LeagueId, IsCurrent = true };
        var user = new User { Id = UserId, Username = "Alex" };
        var member = new QuinielaMember { QuinielaId = QuinielaId, UserId = UserId, Alias = "AlexQuiniela" };
        var qfNode = new LiguillaNode
        {
            SeasonId = SeasonId,
            Round = "QF",
            NodeNumber = 1,
            KickoffUtc = DateTime.UtcNow.AddDays(1)
        };

        _mockUow.Setup(u => u.Quinielas.GetAsync(QuinielaId)).ReturnsAsync(quiniela);
        _mockUow.Setup(u => u.Seasons.GetCurrentSeasonAsync(LeagueId)).ReturnsAsync(season);
        _mockUow.Setup(u => u.Liguilla.GetNodesBySeasonAsync(SeasonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LiguillaNode> { qfNode });
        _mockUow.Setup(u => u.Liguilla.GetBracketAsync(QuinielaId, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LiguillaBracket?)null);
        _mockUow.Setup(u => u.Users.GetAsync(UserId)).ReturnsAsync(user);
        _mockUow.Setup(u => u.QuinielaMembers.GetMembershipAsync(QuinielaId, UserId)).ReturnsAsync(member);

        var dto = new SubmitLiguillaBracketDto
        {
            QfWinner1TeamId = 1,
            QfWinner2TeamId = 2,
            QfWinner3TeamId = 3,
            QfWinner4TeamId = 4,
            SfWinner1TeamId = 1,
            SfWinner2TeamId = 2,
            ChampionTeamId = 1
        };

        // Act
        var result = await _sut.SubmitBracketAsync(QuinielaId, UserId, dto);

        // Assert
        result.UserId.Should().Be(UserId);
        result.ChampionTeamId.Should().Be(1);
        _mockUow.Verify(u => u.Liguilla.AddBracketAsync(It.IsAny<LiguillaBracket>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockUow.Verify(u => u.Save(It.IsAny<CancellationToken>()), Times.Once);
    }
}
