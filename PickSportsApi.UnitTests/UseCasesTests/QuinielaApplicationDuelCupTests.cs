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

/// <summary>SPEC-013 §2.2: la copa y el castigo del campeon de duelos pasan a la quiniela de la siguiente temporada.</summary>
public class QuinielaApplicationDuelCupTests
{
    private const int QuinielaId = 50, OwnerId = 1, LeagueId = 10, OldSeasonId = 2, NextSeasonId = 3, NewQuinielaId = 200;

    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IDuelRepository> _duels = new();

    private async Task<QuinielaDuelSeason?> RunAsync(QuinielaDuelSeason? previousInfo)
    {
        var quiniela = new Quiniela
        {
            Id = QuinielaId, Name = "Liga", QuinielaType = QuinielaType.Sports, LeagueId = LeagueId,
            SeasonId = OldSeasonId, OwnerId = OwnerId, InviteCode = "MX-1", IsActive = true,
        };
        _uow.Setup(u => u.Quinielas.GetAsync(QuinielaId)).ReturnsAsync(quiniela);
        _uow.Setup(u => u.QuinielaMembers.GetMembershipAsync(QuinielaId, OwnerId))
            .ReturnsAsync(new QuinielaMember { QuinielaId = QuinielaId, UserId = OwnerId, Role = "OWNER", Active = true });
        _uow.Setup(u => u.Seasons.GetCurrentSeasonAsync(LeagueId))
            .ReturnsAsync(new Season { Id = NextSeasonId, LeagueId = LeagueId, Name = "Clausura" });
        _uow.Setup(u => u.Quinielas.GetByLeagueIdAsync(LeagueId)).ReturnsAsync(new List<Quiniela> { quiniela });
        _uow.Setup(u => u.Leagues.GetAsync(LeagueId)).ReturnsAsync(new League { Id = LeagueId, Code = "mex.1", Name = "Liga MX", Active = true });
        _uow.Setup(u => u.Quinielas.GetByInviteCodeAsync(It.IsAny<string>())).ReturnsAsync((Quiniela?)null);
        _uow.Setup(u => u.QuinielaMembers.GetMembersAsync(QuinielaId)).ReturnsAsync(new List<QuinielaMember>());
        _uow.Setup(u => u.Quinielas.InsertAsync(It.IsAny<Quiniela>()))
            .Callback<Quiniela>(q => q.Id = NewQuinielaId).ReturnsAsync(true);
        _uow.Setup(u => u.Quinielas.UpdateAsync(It.IsAny<Quiniela>())).ReturnsAsync(true);
        _uow.Setup(u => u.Duels).Returns(_duels.Object);
        _duels.Setup(d => d.GetSeasonInfoAsync(QuinielaId)).ReturnsAsync(previousInfo);

        QuinielaDuelSeason? created = null;
        _duels.Setup(d => d.UpsertSeasonInfoAsync(It.IsAny<QuinielaDuelSeason>()))
            .Callback<QuinielaDuelSeason>(i => created = i).ReturnsAsync(true);

        var app = new QuinielaApplication(_uow.Object, new AppMapper(), new CreateQuinielaDtoValidator(), new JoinQuinielaDtoValidator());
        var result = await app.StartNextSeasonAsync(QuinielaId, OwnerId, new StartNextSeasonDto());
        result.isSuccess.Should().BeTrue();
        return created;
    }

    [Fact]
    public async Task StartNextSeason_CampeonFijoCopaYCastigo_SeCopianALaNuevaQuiniela()
    {
        var created = await RunAsync(new QuinielaDuelSeason
        {
            QuinielaId = QuinielaId, ClosedAt = DateTime.UtcNow, ChampionMemberId = 1,
            CupName = "Copa Coliseo", PunishmentText = "Paga la cena",
        });

        created.Should().NotBeNull();
        created!.QuinielaId.Should().Be(NewQuinielaId);
        created.CupName.Should().Be("Copa Coliseo");
        created.PunishmentText.Should().Be("Paga la cena");
        created.ClosedAt.Should().BeNull();
        created.ChampionMemberId.Should().BeNull();
    }

    [Fact]
    public async Task StartNextSeason_TemporadaDeDuelosAbierta_NoCreaFila()
    {
        var created = await RunAsync(new QuinielaDuelSeason { QuinielaId = QuinielaId, ClosedAt = null, CupName = "Copa" });

        created.Should().BeNull();
    }

    [Fact]
    public async Task StartNextSeason_CerradaSinCopaNiCastigo_NoCreaFila()
    {
        var created = await RunAsync(new QuinielaDuelSeason
        {
            QuinielaId = QuinielaId, ClosedAt = DateTime.UtcNow, CupName = "  ", PunishmentText = null,
        });

        created.Should().BeNull();
    }

    [Fact]
    public async Task StartNextSeason_SinInfoDeDuelos_NoCreaFila()
    {
        var created = await RunAsync(null);

        created.Should().BeNull();
    }
}
