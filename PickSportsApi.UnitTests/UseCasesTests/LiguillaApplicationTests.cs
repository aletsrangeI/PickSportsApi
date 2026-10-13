using Domain.Entities;
using DTO.Liguilla;
using FluentAssertions;
using Interface.Persistence;
using Moq;
using UseCases.Liguilla;
using Xunit;

namespace PickSportsApi.UnitTests.UseCasesTests;

public class LiguillaApplicationTests
{
    private const int QuinielaId = 1;
    private const int LeagueId = 10;
    private const int SeasonId = 20;
    private const int UserId = 50;

    private readonly Mock<IUnitOfWork> _mockUow = new();
    private readonly LiguillaApplication _sut;

    public LiguillaApplicationTests()
    {
        _sut = new LiguillaApplication(_mockUow.Object);
    }

    [Fact]
    public async Task GetOverview_WhenRegularSeasonNotScoredAndNotAdmin_ReturnsHiddenAndUnavailable()
    {
        // Arrange: Torneo va en J12 (no todas <= 17 están SCORED)
        var quiniela = new Quiniela { Id = QuinielaId, LeagueId = LeagueId, Name = "Quiniela Amigos" };
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
        var quiniela = new Quiniela { Id = QuinielaId, LeagueId = LeagueId, Name = "Quiniela Amigos" };
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
    public async Task SubmitBracket_WhenKickoffDeadlineExpired_ThrowsInvalidOperationException()
    {
        // Arrange
        var quiniela = new Quiniela { Id = QuinielaId, LeagueId = LeagueId };
        var season = new Season { Id = SeasonId, LeagueId = LeagueId, IsCurrent = true };
        var qfNode = new LiguillaNode
        {
            SeasonId = SeasonId,
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
        var quiniela = new Quiniela { Id = QuinielaId, LeagueId = LeagueId };
        var season = new Season { Id = SeasonId, LeagueId = LeagueId, IsCurrent = true };
        var qfNode = new LiguillaNode
        {
            SeasonId = SeasonId,
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
    public async Task SubmitBracket_WhenValid_PersistsAndReturnsDetail()
    {
        // Arrange
        var quiniela = new Quiniela { Id = QuinielaId, LeagueId = LeagueId };
        var season = new Season { Id = SeasonId, LeagueId = LeagueId, IsCurrent = true };
        var user = new User { Id = UserId, Username = "Alex" };
        var member = new QuinielaMember { QuinielaId = QuinielaId, UserId = UserId, Alias = "AlexQuiniela" };
        var qfNode = new LiguillaNode
        {
            SeasonId = SeasonId,
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
