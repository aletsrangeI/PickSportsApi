using Domain.Entities;
using DTO.Quiniela;
using Interface.Persistence;
using Interface.UseCases;
using Moq;
using UseCases.Common.Mapping;
using UseCases.Quinielas;
using Validator.Quiniela;

namespace PickSportsApi.UnitTests.UseCasesTests;

/// <summary>H-011: crear una quiniela de una liga sin temporada sembrada la deja lista.</summary>
public class QuinielaApplicationProvisioningTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ISeasonProvisioner> _provisioner = new();
    private Quiniela? _saved;

    private QuinielaApplication Arrange(League league)
    {
        var user = new User { Id = 1, Username = "alex", DisplayName = "Alex", Active = true };
        _uow.Setup(u => u.Users.GetAsync(1)).ReturnsAsync(user);
        _uow.Setup(u => u.Leagues.GetAsync(league.Id)).ReturnsAsync(league);
        _uow.Setup(u => u.Quinielas.GetByInviteCodeAsync(It.IsAny<string>())).ReturnsAsync((Quiniela?)null);
        _uow.Setup(u => u.Quinielas.InsertAsync(It.IsAny<Quiniela>()))
            .Callback<Quiniela>(q => { _saved = q; q.Id = 99; })
            .ReturnsAsync(true);
        _uow.Setup(u => u.QuinielaMembers.InsertAsync(It.IsAny<QuinielaMember>())).ReturnsAsync(true);
        _uow.Setup(u => u.Quinielas.GetWithDetailsAsync(99)).ReturnsAsync(() => _saved);

        return new QuinielaApplication(_uow.Object, new AppMapper(), new CreateQuinielaDtoValidator(),
            new JoinQuinielaDtoValidator(), _provisioner.Object);
    }

    private static CreateQuinielaDto Request(int leagueId) => new()
    {
        Name = "Champions entre amigos", LeagueId = leagueId, EntryFee = 0m,
        FirstPlacePct = 70m, SecondPlacePct = 20m, ThirdPlacePct = 10m
    };

    [Fact]
    public async Task CreateAsync_LeagueWithoutSeason_AssignsProvisionedSeason()
    {
        // Arrange
        var league = new League { Id = 3, Code = "uefa.champions", Name = "Champions", Active = true };
        var app = Arrange(league);
        _provisioner.Setup(p => p.EnsureReadyAsync(league, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SeasonProvisionResult(new Season { Id = 55, LeagueId = 3 }, true, true, null));

        // Act
        var result = await app.CreateAsync(1, Request(3));

        // Assert
        Assert.True(result.isSuccess, result.Message);
        Assert.Equal(55, _saved!.SeasonId);
        Assert.Equal("Quiniela creada exitosamente.", result.Message);
        _uow.Verify(u => u.Seasons.GetCurrentSeasonAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_EspnSyncPending_StillCreatesQuinielaAndSurfacesWarning()
    {
        // Arrange
        var league = new League { Id = 3, Code = "uefa.champions", Name = "Champions", Active = true };
        var app = Arrange(league);
        _provisioner.Setup(p => p.EnsureReadyAsync(league, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SeasonProvisionResult(new Season { Id = 55, LeagueId = 3 }, true, false, "ESPN no respondió."));

        // Act
        var result = await app.CreateAsync(1, Request(3));

        // Assert
        Assert.True(result.isSuccess);
        Assert.Equal(55, _saved!.SeasonId);
        Assert.Contains("ESPN no respondió.", result.Message);
    }

    [Fact]
    public async Task CreateAsync_CustomEvents_DoesNotProvisionSeason()
    {
        // Arrange
        var app = Arrange(new League { Id = 3, Code = "x", Name = "x", Active = true });
        _uow.Setup(u => u.CustomEvents.GetOrCreateEventGroupForQuinielaAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(new EventGroup());

        // Act
        var result = await app.CreateAsync(1, new CreateQuinielaDto
        {
            Name = "Evento", QuinielaType = "CustomEvents", EntryFee = 0m,
            FirstPlacePct = 70m, SecondPlacePct = 20m, ThirdPlacePct = 10m
        });

        // Assert
        Assert.True(result.isSuccess, result.Message);
        _provisioner.Verify(p => p.EnsureReadyAsync(It.IsAny<League>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
