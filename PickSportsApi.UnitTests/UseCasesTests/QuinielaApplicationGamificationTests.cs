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

public class QuinielaApplicationGamificationTests
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

    private Quiniela Setup(Action<Quiniela>? tweak = null, string? memberRole = "OWNER")
    {
        var q = new Quiniela
        {
            Id = QuinielaId, Name = "Liga MX", QuinielaType = QuinielaType.Sports, LeagueId = LeagueId,
            SeasonId = OldSeasonId, OwnerId = OwnerId, InviteCode = "MX-AA", IsActive = true
        };
        tweak?.Invoke(q);
        _uow.Setup(u => u.Quinielas.GetAsync(QuinielaId)).ReturnsAsync(q);
        _uow.Setup(u => u.QuinielaMembers.GetMembershipAsync(QuinielaId, OwnerId))
            .ReturnsAsync(memberRole == null ? null : new QuinielaMember { QuinielaId = QuinielaId, UserId = OwnerId, Role = memberRole, Active = true });
        _uow.Setup(u => u.Quinielas.UpdateAsync(It.IsAny<Quiniela>())).ReturnsAsync(true);
        return q;
    }

    [Fact]
    public async Task SetGamification_QuinielaNotFound_Fails()
    {
        _uow.Setup(u => u.Quinielas.GetAsync(QuinielaId)).ReturnsAsync((Quiniela?)null);

        var result = await CreateApp().SetGamificationAsync(QuinielaId, OwnerId, true);

        result.isSuccess.Should().BeFalse();
        result.Message.Should().Be("La quiniela no existe.");
        _uow.Verify(u => u.Quinielas.UpdateAsync(It.IsAny<Quiniela>()), Times.Never);
    }

    [Fact]
    public async Task SetGamification_CustomEvents_Fails()
    {
        Setup(q => q.QuinielaType = QuinielaType.CustomEvents);

        var result = await CreateApp().SetGamificationAsync(QuinielaId, OwnerId, true);

        result.isSuccess.Should().BeFalse();
        result.Message.Should().Be("La gamificación solo aplica a quinielas deportivas.");
    }

    [Fact]
    public async Task SetGamification_PlainMember_Fails()
    {
        Setup(memberRole: "MEMBER");
        _uow.Setup(u => u.Users.GetAsync(OwnerId)).ReturnsAsync(new User { Id = OwnerId, Role = "USER" });

        var result = await CreateApp().SetGamificationAsync(QuinielaId, OwnerId, true);

        result.isSuccess.Should().BeFalse();
        result.Message.Should().Be("Solo el Owner o un Admin puede cambiar la gamificación.");
        _uow.Verify(u => u.Quinielas.UpdateAsync(It.IsAny<Quiniela>()), Times.Never);
    }

    [Fact]
    public async Task SetGamification_NoSeason_Fails()
    {
        Setup(q => q.SeasonId = null);

        var result = await CreateApp().SetGamificationAsync(QuinielaId, OwnerId, true);

        result.isSuccess.Should().BeFalse();
        result.Message.Should().Be("La quiniela no tiene temporada asignada.");
    }

    [Fact]
    public async Task SetGamification_Archived_Fails()
    {
        Setup(q => q.IsActive = false);

        var result = await CreateApp().SetGamificationAsync(QuinielaId, OwnerId, true);

        result.isSuccess.Should().BeFalse();
        result.Message.Should().Be("La quiniela está archivada.");
    }

    [Fact]
    public async Task SetGamification_SystemAdminWithoutMembership_Succeeds()
    {
        var q = Setup(memberRole: null);
        _uow.Setup(u => u.Users.GetAsync(OwnerId)).ReturnsAsync(new User { Id = OwnerId, Role = "ADMIN" });

        var result = await CreateApp().SetGamificationAsync(QuinielaId, OwnerId, true);

        result.isSuccess.Should().BeTrue();
        q.GamificationEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task SetGamification_Owner_EnablesAndPersists()
    {
        var q = Setup();

        var result = await CreateApp().SetGamificationAsync(QuinielaId, OwnerId, true);

        result.isSuccess.Should().BeTrue();
        result.Message.Should().Be("Gamificación actualizada.");
        result.Data.GamificationEnabled.Should().BeTrue();
        result.Data.UserRole.Should().Be("OWNER");
        q.GamificationEnabled.Should().BeTrue();
        _uow.Verify(u => u.Quinielas.UpdateAsync(q), Times.Once);
    }

    [Fact]
    public async Task SetGamification_Disable_Works()
    {
        var q = Setup(x => x.GamificationEnabled = true);

        var result = await CreateApp().SetGamificationAsync(QuinielaId, OwnerId, false);

        result.isSuccess.Should().BeTrue();
        result.Data.GamificationEnabled.Should().BeFalse();
        q.GamificationEnabled.Should().BeFalse();
        _uow.Verify(u => u.Quinielas.UpdateAsync(q), Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StartNextSeason_CopiesGamificationEnabled(bool enabled)
    {
        // Arrange
        var quiniela = Setup(q => q.GamificationEnabled = enabled);
        _uow.Setup(u => u.Seasons.GetCurrentSeasonAsync(LeagueId))
            .ReturnsAsync(new Season { Id = NextSeasonId, LeagueId = LeagueId, Name = "Apertura 2026" });
        _uow.Setup(u => u.Quinielas.GetByLeagueIdAsync(LeagueId)).ReturnsAsync(new List<Quiniela> { quiniela });
        _uow.Setup(u => u.Leagues.GetAsync(LeagueId)).ReturnsAsync(new League { Id = LeagueId, Code = "mex.1", Name = "Liga MX", Active = true });
        _uow.Setup(u => u.Quinielas.GetByInviteCodeAsync(It.IsAny<string>())).ReturnsAsync((Quiniela?)null);
        _uow.Setup(u => u.QuinielaMembers.GetMembersAsync(QuinielaId)).ReturnsAsync(new List<QuinielaMember>());
        Quiniela? inserted = null;
        _uow.Setup(u => u.Quinielas.InsertAsync(It.IsAny<Quiniela>()))
            .Callback<Quiniela>(q => { inserted = q; q.Id = 99; }).ReturnsAsync(true);
        _uow.Setup(u => u.Quinielas.GetWithDetailsAsync(99)).ReturnsAsync((Quiniela?)null);

        // Act
        var result = await CreateApp().StartNextSeasonAsync(QuinielaId, OwnerId, new StartNextSeasonDto());

        // Assert
        result.isSuccess.Should().BeTrue();
        inserted.Should().NotBeNull();
        inserted!.GamificationEnabled.Should().Be(enabled);
    }
}
