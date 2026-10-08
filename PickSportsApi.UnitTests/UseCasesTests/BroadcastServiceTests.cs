using Common;
using Domain.Entities;
using DTO.Broadcast;
using FluentAssertions;
using Interface.Persistence;
using Interface.UseCases;
using Moq;
using UseCases.Broadcasters;
using Xunit;
using DbMatch = Domain.Entities.Match;

namespace PickSportsApi.UnitTests.UseCasesTests;

public class BroadcastServiceTests
{
    private const int WeekId = 100;

    private readonly Mock<IUnitOfWork> _mockUow = new();
    private readonly Mock<IWeekRepository> _mockWeeks = new();
    private readonly Mock<ISeasonRepository> _mockSeasons = new();
    private readonly Mock<ILeagueRepository> _mockLeagues = new();
    private readonly Mock<IMatchRepository> _mockMatches = new();
    private readonly Mock<IUserRepository> _mockUsers = new();
    private readonly Mock<IQuinielaMemberRepository> _mockMembers = new();
    private readonly Mock<ILigaMxBroadcastScraper> _mockScraper = new();

    private readonly League _ligaMx = new() { Id = 1, Code = "mex.1", Name = "Liga MX", EspnApiPath = "soccer/mex.1" };

    public BroadcastServiceTests()
    {
        _mockUow.Setup(u => u.Weeks).Returns(_mockWeeks.Object);
        _mockUow.Setup(u => u.Seasons).Returns(_mockSeasons.Object);
        _mockUow.Setup(u => u.Leagues).Returns(_mockLeagues.Object);
        _mockUow.Setup(u => u.Matches).Returns(_mockMatches.Object);
        _mockUow.Setup(u => u.Users).Returns(_mockUsers.Object);
        _mockUow.Setup(u => u.QuinielaMembers).Returns(_mockMembers.Object);

        _mockWeeks.Setup(w => w.GetAsync(WeekId)).ReturnsAsync(new Week { Id = WeekId, SeasonId = 5, WeekNumber = 11 });
        _mockSeasons.Setup(s => s.GetAsync(5)).ReturnsAsync(new Season { Id = 5, LeagueId = 1 });
        _mockLeagues.Setup(l => l.GetAsync(1)).ReturnsAsync(_ligaMx);
        _mockMatches.Setup(m => m.UpdateAsync(It.IsAny<DbMatch>())).ReturnsAsync(true);
        _mockMembers.Setup(m => m.GetByUserIdAsync(It.IsAny<int>())).ReturnsAsync(new List<QuinielaMember>());
    }

    private BroadcastService CreateService() =>
        new(_mockUow.Object, new BroadcastRuleEngine(), _mockScraper.Object, new Mock<IAppLogger<BroadcastService>>().Object);

    private static DbMatch CreateMatch(int id, string home, string away, string? broadcasters = null, string? source = null) => new()
    {
        Id = id,
        WeekId = WeekId,
        HomeTeam = new Team { Abbreviation = home },
        AwayTeam = new Team { Abbreviation = away },
        Broadcasters = broadcasters,
        BroadcastersSource = source
    };

    private void SetupMatches(params DbMatch[] matches) =>
        _mockMatches.Setup(m => m.GetByWeekIdAsync(WeekId)).ReturnsAsync(matches);

    private void SetupScraper(params ScrapedMatchBroadcast[] scraped) =>
        _mockScraper.Setup(s => s.FetchCurrentWeekAsync(It.IsAny<CancellationToken>())).ReturnsAsync(scraped);

    [Fact]
    public async Task ApplyDefaultBroadcastersAsync_PartidosSinCanales_AsignaReglaDeLocalia()
    {
        // Arrange
        var chivas = CreateMatch(1, "GDL", "AME");
        var america = CreateMatch(2, "AME", "TOL");
        SetupMatches(chivas, america);

        // Act
        var applied = await CreateService().ApplyDefaultBroadcastersAsync(WeekId);

        // Assert
        applied.Should().Be(2);
        BroadcastChannelCatalog.Deserialize(chivas.Broadcasters).Should().Equal("Amazon Prime Video");
        BroadcastChannelCatalog.Deserialize(america.Broadcasters).Should().Equal("Canal 5", "TUDN", "ViX Premium");
        chivas.BroadcastersSource.Should().Be(BroadcastChannelCatalog.SourceRule);
    }

    [Fact]
    public async Task ApplyDefaultBroadcastersAsync_LigaDistintaALigaMx_NoModificaPartidos()
    {
        // Arrange
        _mockLeagues.Setup(l => l.GetAsync(1)).ReturnsAsync(new League { Id = 1, Code = "nfl", Name = "NFL", EspnApiPath = "football/nfl" });
        var match = CreateMatch(1, "KC", "BUF");
        SetupMatches(match);

        // Act
        var applied = await CreateService().ApplyDefaultBroadcastersAsync(WeekId);

        // Assert
        applied.Should().Be(0);
        match.Broadcasters.Should().BeNull();
        _mockMatches.Verify(m => m.UpdateAsync(It.IsAny<DbMatch>()), Times.Never);
    }

    [Fact]
    public async Task SyncFromOfficialSourceAsync_ConSenalOficial_ActualizaYPreservaOverrideManual()
    {
        // Arrange
        var pumas = CreateMatch(1, "PUM", "CAZ"); // alias histórico de UNAM
        var manualJson = BroadcastChannelCatalog.Serialize(new[] { "ViX Premium" });
        var manual = CreateMatch(2, "AME", "MTY", manualJson, BroadcastChannelCatalog.SourceManual);
        SetupMatches(pumas, manual);
        SetupScraper(
            new ScrapedMatchBroadcast("UNAM", "CAZ", 11, new[] { "Canal 5", "TUDN" }),
            new ScrapedMatchBroadcast("AME", "MTY", 11, new[] { "Canal 5" }));

        // Act
        var result = await CreateService().SyncFromOfficialSourceAsync(WeekId);

        // Assert
        result.OfficialSourceAvailable.Should().BeTrue();
        result.OfficialUpdated.Should().Be(1);
        result.ManualPreserved.Should().Be(1);
        BroadcastChannelCatalog.Deserialize(pumas.Broadcasters).Should().Equal("Canal 5", "TUDN");
        pumas.BroadcastersSource.Should().Be(BroadcastChannelCatalog.SourceLigaMx);
        manual.Broadcasters.Should().Be(manualJson);
        manual.BroadcastersSource.Should().Be(BroadcastChannelCatalog.SourceManual);
    }

    [Fact]
    public async Task SyncFromOfficialSourceAsync_SinRespuestaOficial_ConservaReglaDeLocalia()
    {
        // Arrange
        var match = CreateMatch(1, "GDL", "ATS");
        SetupMatches(match);
        SetupScraper();

        // Act
        var result = await CreateService().SyncFromOfficialSourceAsync(WeekId);

        // Assert
        result.OfficialSourceAvailable.Should().BeFalse();
        result.DefaultsApplied.Should().Be(1);
        BroadcastChannelCatalog.Deserialize(match.Broadcasters).Should().Equal("Amazon Prime Video");
        match.BroadcastersSource.Should().Be(BroadcastChannelCatalog.SourceRule);
    }

    [Fact]
    public async Task SyncFromOfficialSourceAsync_SenalDeOtraJornada_SeIgnora()
    {
        // Arrange
        var match = CreateMatch(1, "TOL", "NCX");
        SetupMatches(match);
        SetupScraper(new ScrapedMatchBroadcast("TOL", "NCX", 12, new[] { "Azteca 7" }));

        // Act
        var result = await CreateService().SyncFromOfficialSourceAsync(WeekId);

        // Assert
        result.OfficialSourceAvailable.Should().BeFalse();
        result.OfficialUpdated.Should().Be(0);
        BroadcastChannelCatalog.Deserialize(match.Broadcasters).Should().Equal("Canal 5", "TUDN", "ViX Premium");
    }

    [Fact]
    public async Task SyncWeekBroadcastersAsync_UsuarioSinPermisos_RechazaSinConsultarFuente()
    {
        // Arrange
        _mockUsers.Setup(u => u.GetAsync(7)).ReturnsAsync(new User { Id = 7, Role = "USER" });

        // Act
        var response = await CreateService().SyncWeekBroadcastersAsync(WeekId, 7);

        // Assert
        response.isSuccess.Should().BeFalse();
        _mockScraper.Verify(s => s.FetchCurrentWeekAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateMatchBroadcastersAsync_AdminGlobal_GuardaOverrideManualNormalizado()
    {
        // Arrange
        _mockUsers.Setup(u => u.GetAsync(1)).ReturnsAsync(new User { Id = 1, Role = "ADMIN" });
        var match = CreateMatch(10, "AME", "GDL");
        _mockMatches.Setup(m => m.GetByIdWithTeamsAsync(10)).ReturnsAsync(match);

        // Act
        var response = await CreateService().UpdateMatchBroadcastersAsync(10, new List<string> { "vix", " ", "ViX Premium" }, 1);

        // Assert
        response.isSuccess.Should().BeTrue();
        response.Data.Broadcasters.Should().Equal("ViX Premium");
        response.Data.Source.Should().Be(BroadcastChannelCatalog.SourceManual);
        match.BroadcastersSource.Should().Be(BroadcastChannelCatalog.SourceManual);
        _mockMatches.Verify(m => m.UpdateAsync(match), Times.Once);
    }

    [Fact]
    public async Task UpdateMatchBroadcastersAsync_ListaVacia_RestableceReglaDeLocalia()
    {
        // Arrange
        _mockUsers.Setup(u => u.GetAsync(2)).ReturnsAsync(new User { Id = 2, Role = "USER" });
        _mockMembers.Setup(m => m.GetByUserIdAsync(2)).ReturnsAsync(new List<QuinielaMember> { new() { Role = "OWNER" } });
        var match = CreateMatch(10, "GDL", "AME", "[\"ViX Premium\"]", BroadcastChannelCatalog.SourceManual);
        _mockMatches.Setup(m => m.GetByIdWithTeamsAsync(10)).ReturnsAsync(match);

        // Act
        var response = await CreateService().UpdateMatchBroadcastersAsync(10, new List<string>(), 2);

        // Assert
        response.isSuccess.Should().BeTrue();
        response.Data.Broadcasters.Should().Equal("Amazon Prime Video");
        match.BroadcastersSource.Should().Be(BroadcastChannelCatalog.SourceRule);
    }

    [Fact]
    public async Task UpdateMatchBroadcastersAsync_MiembroSinRolAdmin_Rechaza()
    {
        // Arrange
        _mockUsers.Setup(u => u.GetAsync(3)).ReturnsAsync(new User { Id = 3, Role = "USER" });
        _mockMembers.Setup(m => m.GetByUserIdAsync(3)).ReturnsAsync(new List<QuinielaMember> { new() { Role = "MEMBER" } });

        // Act
        var response = await CreateService().UpdateMatchBroadcastersAsync(10, new List<string> { "TUDN" }, 3);

        // Assert
        response.isSuccess.Should().BeFalse();
        _mockMatches.Verify(m => m.UpdateAsync(It.IsAny<DbMatch>()), Times.Never);
    }
}
