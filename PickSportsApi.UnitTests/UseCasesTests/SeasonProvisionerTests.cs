using Common;
using Domain.Entities;
using Interface.Persistence;
using Interface.UseCases;
using Moq;
using UseCases.Seasons;

namespace PickSportsApi.UnitTests.UseCasesTests;

public class SeasonProvisionerTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IEspnSyncService> _espn = new();
    private readonly Mock<IAppLogger<SeasonProvisioner>> _logger = new();

    private SeasonProvisioner CreateSut() => new(_uow.Object, _espn.Object, _logger.Object);

    private static League Champions() => new() { Id = 3, Code = "uefa.champions", Name = "UEFA Champions League", SportKey = "soccer", Active = true };

    private void NoCurrentSeason(int leagueId, out List<Season> inserted)
    {
        var list = new List<Season>();
        inserted = list;
        _uow.Setup(u => u.Seasons.GetCurrentSeasonAsync(leagueId)).ReturnsAsync((Season?)null);
        _uow.Setup(u => u.Seasons.InsertAsync(It.IsAny<Season>()))
            .Callback<Season>(s => { s.Id = 55; list.Add(s); })
            .ReturnsAsync(true);
    }

    [Fact]
    public async Task EnsureReadyAsync_LeagueWithoutSeason_CreatesCurrentSeasonAndSyncsCalendar()
    {
        // Arrange
        NoCurrentSeason(3, out var inserted);
        _uow.Setup(u => u.Weeks.GetBySeasonIdAsync(55)).ReturnsAsync(new List<Week>());
        _espn.Setup(e => e.SyncFullSeasonAsync(55, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncResult(true, 8, 144, 36, "L2_SCOREBOARD", null));

        // Act
        var result = await CreateSut().EnsureReadyAsync(Champions());

        // Assert
        Assert.True(result.Created);
        Assert.True(result.HasWeeks);
        Assert.Null(result.Warning);
        var season = Assert.Single(inserted);
        Assert.True(season.IsCurrent);
        Assert.Equal(3, season.LeagueId);
        Assert.Equal(DateTime.UtcNow.Year, season.Year);
        _espn.Verify(e => e.SyncFullSeasonAsync(55, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnsureReadyAsync_SoccerCalendarLongerThanConfig_RaisesLeagueWeeksCount()
    {
        // Arrange: Serie A tiene 38 jornadas y la liga nace con WeeksCount = 17
        var league = new League { Id = 5, Code = "ita.1", Name = "Serie A", SportKey = "soccer", WeeksCount = 17, Active = true };
        NoCurrentSeason(5, out _);
        _uow.SetupSequence(u => u.Weeks.GetBySeasonIdAsync(55))
            .ReturnsAsync(new List<Week>())
            .ReturnsAsync(Enumerable.Range(1, 38).Select(i => new Week { Id = i, SeasonId = 55, WeekNumber = i }).ToList());
        _espn.Setup(e => e.SyncFullSeasonAsync(55, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncResult(true, 38, 380, 20, "L2_SCOREBOARD", null));
        _uow.Setup(u => u.Leagues.UpdateAsync(It.IsAny<League>())).ReturnsAsync(true);

        // Act
        await CreateSut().EnsureReadyAsync(league);

        // Assert
        Assert.Equal(38, league.WeeksCount);
        _uow.Verify(u => u.Leagues.UpdateAsync(league), Times.Once);
    }

    [Fact]
    public void BuildSeason_NflWithDefaultSportKeySoccer_StillUsesRegularSeasonType()
    {
        // Arrange: como en la BD real, SportKey queda en el valor por defecto "soccer" y solo EspnApiPath dice football
        var nfl = new League { Id = 4, Code = "nfl", EspnApiPath = "sports/football/nfl", SportKey = "soccer" };

        // Act
        var season = SeasonProvisioner.BuildSeason(nfl, new DateTime(2026, 11, 13, 0, 0, 0, DateTimeKind.Utc));

        // Assert
        Assert.Equal("Temporada Regular 2026", season.Name);
        Assert.Equal(2, season.SeasonType);
    }

    [Fact]
    public async Task EnsureReadyAsync_ChampionsLeaguePhaseOfEightRounds_LowersWeeksCountFromDefault()
    {
        // Arrange: la fase de liga son 8 jornadas; con WeeksCount = 17 el selector mostraría 17
        var league = Champions();
        league.WeeksCount = 17;
        NoCurrentSeason(3, out _);
        _uow.SetupSequence(u => u.Weeks.GetBySeasonIdAsync(55))
            .ReturnsAsync(new List<Week>())
            .ReturnsAsync(Enumerable.Range(1, 8).Select(i => new Week { Id = i, SeasonId = 55, WeekNumber = i }).ToList());
        _espn.Setup(e => e.SyncFullSeasonAsync(55, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncResult(true, 8, 144, 36, "L2_SCOREBOARD", null));
        _uow.Setup(u => u.Leagues.UpdateAsync(It.IsAny<League>())).ReturnsAsync(true);

        // Act
        await CreateSut().EnsureReadyAsync(league);

        // Assert
        Assert.Equal(8, league.WeeksCount);
    }

    [Fact]
    public async Task EnsureReadyAsync_NflCalendarOfEighteenWeeks_RaisesWeeksCountSoWeekEighteenIsRegular()
    {
        // Arrange
        var league = new League { Id = 4, Code = "nfl", Name = "NFL", SportKey = "football", WeeksCount = 17, Active = true };
        NoCurrentSeason(4, out _);
        _uow.SetupSequence(u => u.Weeks.GetBySeasonIdAsync(55))
            .ReturnsAsync(new List<Week>())
            .ReturnsAsync(Enumerable.Range(1, 18).Select(i => new Week { Id = i, SeasonId = 55, WeekNumber = i }).ToList());
        _espn.Setup(e => e.SyncFullSeasonAsync(55, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncResult(true, 18, 272, 32, "L1_CALENDAR", null));
        _uow.Setup(u => u.Leagues.UpdateAsync(It.IsAny<League>())).ReturnsAsync(true);

        // Act
        await CreateSut().EnsureReadyAsync(league);

        // Assert
        Assert.Equal(18, league.WeeksCount);
    }

    [Fact]
    public async Task EnsureReadyAsync_L3ConfigFallback_DoesNotTouchWeeksCount()
    {
        // Arrange: L3 solo genera stubs con WeeksCount, no es información real de ESPN
        var league = Champions();
        league.WeeksCount = 17;
        NoCurrentSeason(3, out _);
        _uow.SetupSequence(u => u.Weeks.GetBySeasonIdAsync(55))
            .ReturnsAsync(new List<Week>())
            .ReturnsAsync(Enumerable.Range(1, 17).Select(i => new Week { Id = i, SeasonId = 55, WeekNumber = i }).ToList());
        _espn.Setup(e => e.SyncFullSeasonAsync(55, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncResult(true, 17, 0, 0, "L3_CONFIG", null));

        // Act
        await CreateSut().EnsureReadyAsync(league);

        // Assert
        Assert.Equal(17, league.WeeksCount);
        _uow.Verify(u => u.Leagues.UpdateAsync(It.IsAny<League>()), Times.Never);
    }

    [Theory]
    [InlineData("league-phase", false)]
    [InlineData("regular-season", false)]
    [InlineData(null, false)]
    [InlineData("knockout-round-playoffs", true)]
    [InlineData("round-of-16", true)]
    [InlineData("quarterfinals", true)]
    [InlineData("semifinals", true)]
    [InlineData("final", true)]
    [InlineData("mls-cup-playoffs", true)]
    // Slugs reales de ESPN medidos en temporadas 2025 terminadas
    [InlineData("post-season", true)]
    [InlineData("eastern-conference-playoffs---round-one", true)]
    [InlineData("western-conference-playoffs---semifinals", true)]
    [InlineData("apertura---finals", true)]
    [InlineData("2024-25-english-premier-league", false)]
    public void IsKnockoutPhase_ClassifiesEspnSeasonSlug(string? slug, bool expected)
    {
        Assert.Equal(expected, UseCases.Espn.EspnSyncService.IsKnockoutPhase(slug));
    }

    [Fact]
    public async Task EnsureReadyAsync_LigaMx_KeepsWeeksCountSoLiguillaStaysAfterRegularRounds()
    {
        // Arrange
        var league = new League { Id = 1, Code = "mex.1", Name = "Liga MX", SportKey = "soccer", WeeksCount = 17, Active = true };
        NoCurrentSeason(1, out _);
        _uow.SetupSequence(u => u.Weeks.GetBySeasonIdAsync(55))
            .ReturnsAsync(new List<Week>())
            .ReturnsAsync(Enumerable.Range(1, 22).Select(i => new Week { Id = i, SeasonId = 55, WeekNumber = i }).ToList());
        _espn.Setup(e => e.SyncFullSeasonAsync(55, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncResult(true, 22, 90, 18, "L2_SCOREBOARD", null));

        // Act
        await CreateSut().EnsureReadyAsync(league);

        // Assert
        Assert.Equal(17, league.WeeksCount);
        _uow.Verify(u => u.Leagues.UpdateAsync(It.IsAny<League>()), Times.Never);
    }

    [Fact]
    public async Task EnsureReadyAsync_SeasonWithWeeks_DoesNotCreateNorSyncAgain()
    {
        // Arrange: idempotencia, una segunda quiniela de la misma liga no duplica nada
        var season = new Season { Id = 9, LeagueId = 3, IsCurrent = true, Name = "Temporada 2026" };
        _uow.Setup(u => u.Seasons.GetCurrentSeasonAsync(3)).ReturnsAsync(season);
        _uow.Setup(u => u.Weeks.GetBySeasonIdAsync(9)).ReturnsAsync(new List<Week> { new() { Id = 1, SeasonId = 9, WeekNumber = 1 } });

        // Act
        var result = await CreateSut().EnsureReadyAsync(Champions());

        // Assert
        Assert.False(result.Created);
        Assert.True(result.HasWeeks);
        Assert.Same(season, result.Season);
        _uow.Verify(u => u.Seasons.InsertAsync(It.IsAny<Season>()), Times.Never);
        _espn.Verify(e => e.SyncFullSeasonAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnsureReadyAsync_ExistingSeasonWithoutWeeks_SyncsItWithoutCreatingAnother()
    {
        // Arrange: temporada sembrada pero vacía (caso H-011 con ESPN caído en la creación anterior)
        var season = new Season { Id = 9, LeagueId = 3, IsCurrent = true, Name = "Temporada 2026" };
        _uow.Setup(u => u.Seasons.GetCurrentSeasonAsync(3)).ReturnsAsync(season);
        _uow.Setup(u => u.Weeks.GetBySeasonIdAsync(9)).ReturnsAsync(new List<Week>());
        _espn.Setup(e => e.SyncFullSeasonAsync(9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncResult(true, 6, 100, 30, "L2_SCOREBOARD", null));

        // Act
        var result = await CreateSut().EnsureReadyAsync(Champions());

        // Assert
        Assert.False(result.Created);
        Assert.True(result.HasWeeks);
        _uow.Verify(u => u.Seasons.InsertAsync(It.IsAny<Season>()), Times.Never);
        _espn.Verify(e => e.SyncFullSeasonAsync(9, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnsureReadyAsync_EspnFails_KeepsSeasonAndReturnsWarning()
    {
        // Arrange
        NoCurrentSeason(3, out _);
        _uow.Setup(u => u.Weeks.GetBySeasonIdAsync(55)).ReturnsAsync(new List<Week>());
        _espn.Setup(e => e.SyncFullSeasonAsync(55, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncResult(false, 0, 0, 0, "ERROR", "HTTP 503"));

        // Act
        var result = await CreateSut().EnsureReadyAsync(Champions());

        // Assert
        Assert.NotNull(result.Season);
        Assert.True(result.Created);
        Assert.False(result.HasWeeks);
        Assert.Contains("HTTP 503", result.Warning);
    }

    [Fact]
    public async Task EnsureReadyAsync_EspnThrows_DoesNotPropagateAndReturnsWarning()
    {
        // Arrange
        NoCurrentSeason(3, out _);
        _uow.Setup(u => u.Weeks.GetBySeasonIdAsync(55)).ReturnsAsync(new List<Week>());
        _espn.Setup(e => e.SyncFullSeasonAsync(55, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("timeout"));

        // Act
        var result = await CreateSut().EnsureReadyAsync(Champions());

        // Assert
        Assert.NotNull(result.Season);
        Assert.False(result.HasWeeks);
        Assert.NotNull(result.Warning);
    }

    [Fact]
    public async Task EnsureReadyAsync_FallbackToConfigWithoutMatches_WarnsThatMatchesAreMissing()
    {
        // Arrange
        NoCurrentSeason(3, out _);
        _uow.Setup(u => u.Weeks.GetBySeasonIdAsync(55)).ReturnsAsync(new List<Week>());
        _espn.Setup(e => e.SyncFullSeasonAsync(55, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncResult(true, 17, 0, 0, "L3_CONFIG", null));

        // Act
        var result = await CreateSut().EnsureReadyAsync(Champions());

        // Assert
        Assert.True(result.HasWeeks);
        Assert.NotNull(result.Warning);
    }

    [Fact]
    public async Task EnsureReadyAsync_InsertFails_ReturnsNullSeasonWithWarningAndNeverSyncs()
    {
        // Arrange
        _uow.Setup(u => u.Seasons.GetCurrentSeasonAsync(3)).ReturnsAsync((Season?)null);
        _uow.Setup(u => u.Seasons.InsertAsync(It.IsAny<Season>())).ReturnsAsync(false);

        // Act
        var result = await CreateSut().EnsureReadyAsync(Champions());

        // Assert
        Assert.Null(result.Season);
        Assert.NotNull(result.Warning);
        _espn.Verify(e => e.SyncFullSeasonAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("nfl", "football", 2026, 11, "Temporada Regular 2026", 2)]
    [InlineData("uefa.champions", "soccer", 2026, 11, "Temporada 2026", 1)]
    [InlineData("ita.1", "soccer", 2027, 3, "Temporada 2027", 1)]
    [InlineData("mex.1", "soccer", 2026, 8, "Apertura 2026", 2)]
    [InlineData("mex.1", "soccer", 2027, 2, "Clausura 2027", 1)]
    public void BuildSeason_NamesSeasonByLeagueKind(string code, string sportKey, int year, int month, string expectedName, int expectedType)
    {
        // Arrange
        var league = new League { Id = 1, Code = code, SportKey = sportKey };

        // Act
        var season = SeasonProvisioner.BuildSeason(league, new DateTime(year, month, 15, 0, 0, 0, DateTimeKind.Utc));

        // Assert
        Assert.Equal(expectedName, season.Name);
        Assert.Equal(expectedType, season.SeasonType);
        Assert.Equal(year, season.Year);
        Assert.True(season.IsCurrent);
    }
}
