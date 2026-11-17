using System.Net;
using System.Text;
using Common;
using Domain.Entities;
using FluentAssertions;
using Interface.Persistence;
using Moq;
using UseCases.Broadcasters;
using UseCases.Espn;
using Match = Domain.Entities.Match;

namespace PickSportsApi.UnitTests.UseCasesTests;

/// <summary>
/// L2 (scoreboard por rango de fechas) de EspnSyncService: ventana de temporada para hockey,
/// deteccion de truncamiento (ESPN no pagina: tope duro de 1000 eventos) y exclusion de pretemporada.
/// </summary>
public class EspnSyncServiceScoreboardTests
{
    private const int LeagueId = 3;
    private const int SeasonId = 30;
    private const int EspnEventLimit = 1000;

    private readonly Mock<IUnitOfWork> _uow = new() { DefaultValue = DefaultValue.Mock };
    private readonly List<string> _requestedUrls = new();
    private readonly List<string> _insertedGameIds = new();

    public EspnSyncServiceScoreboardTests()
    {
        _uow.Setup(u => u.Weeks.GetBySeasonIdAsync(It.IsAny<int>())).ReturnsAsync(new List<Week>());
        _uow.Setup(u => u.Teams.GetByEspnIdAsync(It.IsAny<int>(), It.IsAny<string>())).ReturnsAsync((Team?)null);
        _uow.Setup(u => u.Matches.GetByEspnGameIdAsync(It.IsAny<string>())).ReturnsAsync((Match?)null);
        _uow.Setup(u => u.Matches.InsertAsync(It.IsAny<Match>()))
            .Callback<Match>(m => _insertedGameIds.Add(m.EspnGameId))
            .ReturnsAsync(true);
        _uow.Setup(u => u.Save(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string Event(string id, string isoDate, string slug = "regular-season") => $$$"""
        {"id":"{{{id}}}","date":"{{{isoDate}}}","season":{"slug":"{{{slug}}}"},
         "competitions":[{"status":{"type":{"name":"STATUS_FINAL","state":"post","detail":"Final"}},
           "competitors":[
             {"homeAway":"home","score":"2","winner":true,"team":{"id":"1","name":"A","abbreviation":"AAA","displayName":"Team A"}},
             {"homeAway":"away","score":"1","team":{"id":"2","name":"B","abbreviation":"BBB","displayName":"Team B"}}]}]}
        """;

    private static string Scoreboard(IEnumerable<string> events) => "{\"events\":[" + string.Join(",", events) + "]}";

    private static string Scoreboard(params string[] events) => Scoreboard((IEnumerable<string>)events);

    private static string Query(HttpRequestMessage request) => request.RequestUri!.Query;

    private EspnSyncService CreateService(Func<HttpRequestMessage, string> respond)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("EspnClient")).Returns(() => new HttpClient(new StubHandler(request =>
        {
            _requestedUrls.Add(request.RequestUri!.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(respond(request), Encoding.UTF8) };
        })));
        var logger = new Mock<IAppLogger<EspnSyncService>>();
        return new EspnSyncService(factory.Object, _uow.Object, new EspnScoreboardParser(), logger.Object, new BroadcastRuleEngine());
    }

    private void Arrange(League league, Season season)
    {
        league.Id = LeagueId;
        season.Id = SeasonId;
        season.LeagueId = LeagueId;
        _uow.Setup(u => u.Seasons.GetAsync(SeasonId)).ReturnsAsync(season);
        _uow.Setup(u => u.Leagues.GetAsync(LeagueId)).ReturnsAsync(league);
    }

    private static League Nhl() => new() { Code = "nhl", EspnApiPath = "sports/hockey/nhl", SportKey = "hockey" };

    private static Season NhlSeason() => new()
    {
        Year = 2027,
        Name = "Temporada 2026-27",
        StartDate = new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc),
        EndDate = new DateTime(2027, 7, 1, 0, 0, 0, DateTimeKind.Utc),
        IsCurrent = true,
        SeasonType = 2
    };

    /// <summary>Respuesta vacia para cualquier URL que no sea un scoreboard (p. ej. el calendario L1).</summary>
    private static string NotScoreboard(HttpRequestMessage request) => "{}";

    private bool Requested(string datesValue) =>
        _requestedUrls.Any(u => u.Contains("/scoreboard") && u.Contains($"dates={datesValue}&"));

    // ── Truncamiento ──────────────────────────────────────────────────────────

    [Fact]
    public async Task SyncFullSeasonAsync_YearResponseHitsEspnLimit_RequestsMonthByMonthAndReturnsUnion()
    {
        // Arrange: dates=2026 llega al tope duro de 1000 eventos (truncado); los meses traen el detalle real.
        Arrange(Nhl(), NhlSeason());
        var truncated = Enumerable.Range(0, EspnEventLimit).Select(i => Event($"trunc-{i}", "2026-10-01T12:00Z")).ToList();
        var service = CreateService(request =>
        {
            var q = Query(request);
            if (!request.RequestUri!.AbsolutePath.EndsWith("/scoreboard")) return NotScoreboard(request);
            if (q.Contains("dates=2026&")) return Scoreboard(truncated);
            if (q.Contains("dates=202610&")) return Scoreboard(Event("oct-1", "2026-10-10T12:00Z"), Event("oct-2", "2026-10-20T12:00Z"));
            if (q.Contains("dates=202611&")) return Scoreboard(Event("nov-1", "2026-11-10T12:00Z"), Event("dup", "2026-11-30T12:00Z"));
            if (q.Contains("dates=202612&")) return Scoreboard(Event("dup", "2026-11-30T12:00Z"), Event("dec-1", "2026-12-05T12:00Z"));
            if (q.Contains("dates=2027&")) return Scoreboard(Event("feb-1", "2027-02-10T12:00Z"));
            return Scoreboard();
        });

        // Act
        var result = await service.SyncFullSeasonAsync(SeasonId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        for (var month = 1; month <= 12; month++)
            Requested($"2026{month:00}").Should().BeTrue($"el mes {month:00} de 2026 debe pedirse al detectar truncamiento");
        _insertedGameIds.Should().BeEquivalentTo(new[] { "oct-1", "oct-2", "nov-1", "dup", "dec-1", "feb-1" });
        _insertedGameIds.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task SyncFullSeasonAsync_YearResponseBelowLimit_DoesNotRequestMonths()
    {
        // Arrange
        Arrange(Nhl(), NhlSeason());
        var service = CreateService(request =>
        {
            if (!request.RequestUri!.AbsolutePath.EndsWith("/scoreboard")) return NotScoreboard(request);
            if (Query(request).Contains("dates=2026&")) return Scoreboard(Event("a", "2026-10-10T12:00Z"));
            if (Query(request).Contains("dates=2027&")) return Scoreboard(Event("b", "2027-02-10T12:00Z"));
            return Scoreboard();
        });

        // Act
        await service.SyncFullSeasonAsync(SeasonId);

        // Assert
        _requestedUrls.Where(u => u.Contains("dates=2026") && !u.Contains("dates=2026&")).Should().BeEmpty();
        _insertedGameIds.Should().BeEquivalentTo(new[] { "a", "b" });
    }

    // ── Ventana de temporada en hockey ───────────────────────────────────────

    [Fact]
    public async Task SyncFullSeasonAsync_Hockey_UsesSeasonWindowAndDropsPreviousSeasonGames()
    {
        // Arrange: dates=2026 mezcla el final de la temporada anterior (feb-2026) con la nueva.
        Arrange(Nhl(), NhlSeason());
        var service = CreateService(request =>
        {
            if (!request.RequestUri!.AbsolutePath.EndsWith("/scoreboard")) return NotScoreboard(request);
            if (Query(request).Contains("dates=2026&"))
                return Scoreboard(Event("prev-season", "2026-02-10T12:00Z"), Event("new-oct", "2026-10-05T12:00Z"));
            if (Query(request).Contains("dates=2027&"))
                return Scoreboard(Event("new-mar", "2027-03-01T12:00Z"), Event("after-end", "2027-08-01T12:00Z"));
            return Scoreboard();
        });

        // Act
        await service.SyncFullSeasonAsync(SeasonId);

        // Assert
        Requested("2026").Should().BeTrue("la ventana 2026-09 a 2027-07 abarca dos anios calendario");
        Requested("2027").Should().BeTrue();
        _insertedGameIds.Should().BeEquivalentTo(new[] { "new-oct", "new-mar" });
    }

    [Fact]
    public async Task SyncFullSeasonAsync_HockeyCurrentSeasonWithoutDates_ResolvesWindowFromEspnAndRenamesSeason()
    {
        // Arrange: la temporada aun no guarda ventana; ESPN la publica en leagues[0].season.
        var season = new Season { Year = 2027, Name = "Temporada 2027", IsCurrent = true, SeasonType = 2 };
        Arrange(Nhl(), season);
        var service = CreateService(request =>
        {
            if (!request.RequestUri!.AbsolutePath.EndsWith("/scoreboard")) return NotScoreboard(request);
            if (string.IsNullOrEmpty(Query(request)))
                return "{\"leagues\":[{\"season\":{\"startDate\":\"2026-09-15T07:00Z\",\"endDate\":\"2027-07-01T06:59Z\"}}],\"events\":[]}";
            if (Query(request).Contains("dates=2026&")) return Scoreboard(Event("prev", "2026-02-10T12:00Z"), Event("new", "2026-10-05T12:00Z"));
            return Scoreboard();
        });

        // Act
        await service.SyncFullSeasonAsync(SeasonId);

        // Assert
        season.Name.Should().Be("Temporada 2026-27");
        season.StartDate.Should().NotBeNull();
        season.EndDate.Should().NotBeNull();
        _insertedGameIds.Should().BeEquivalentTo(new[] { "new" });
    }

    // ── Pretemporada ──────────────────────────────────────────────────────────

    [Fact]
    public async Task SyncFullSeasonAsync_WindowedBranch_ExcludesPreseasonGames()
    {
        // Arrange
        Arrange(Nhl(), NhlSeason());
        var service = CreateService(request =>
        {
            if (!request.RequestUri!.AbsolutePath.EndsWith("/scoreboard")) return NotScoreboard(request);
            if (Query(request).Contains("dates=2026&"))
                return Scoreboard(
                    Event("pre-1", "2026-09-20T12:00Z", "preseason"),
                    Event("reg-1", "2026-10-05T12:00Z", "regular-season"));
            return Scoreboard();
        });

        // Act
        await service.SyncFullSeasonAsync(SeasonId);

        // Assert
        _insertedGameIds.Should().BeEquivalentTo(new[] { "reg-1" });
    }

    // ── Sin cambios: Liga MX y NFL ───────────────────────────────────────────

    [Fact]
    public async Task SyncFullSeasonAsync_LigaMxClausura_KeepsSingleYearRequestAndNoWindowFiltering()
    {
        // Arrange
        var league = new League { Code = "mex.1", EspnApiPath = "sports/soccer/mex.1", SportKey = "soccer" };
        var season = new Season { Year = 2026, Name = "Clausura 2026", IsCurrent = true, SeasonType = 2 };
        Arrange(league, season);
        var service = CreateService(request =>
            Query(request).Contains("dates=2026&")
                ? Scoreboard(Event("mx-1", "2026-02-10T12:00Z", "torneo-clausura"), Event("mx-pre", "2026-01-05T12:00Z", "preseason"))
                : Scoreboard());

        // Act
        await service.SyncFullSeasonAsync(SeasonId);

        // Assert
        _requestedUrls.Should().OnlyContain(u => u.Contains("/scoreboard?dates=2026&limit=1000"));
        _insertedGameIds.Should().BeEquivalentTo(new[] { "mx-1", "mx-pre" });
        season.StartDate.Should().BeNull();
        season.Name.Should().Be("Clausura 2026");
    }

    [Fact]
    public async Task SyncFullSeasonAsync_Nfl_KeepsSingleYearRequestAndNoWindowFiltering()
    {
        // Arrange
        var league = new League { Code = "nfl", EspnApiPath = "sports/football/nfl", SportKey = "football", WeeksCount = 18 };
        var season = new Season { Year = 2026, Name = "Temporada 2026", IsCurrent = true, SeasonType = 2 };
        Arrange(league, season);
        var service = CreateService(request =>
        {
            if (!request.RequestUri!.AbsolutePath.EndsWith("/scoreboard")) return NotScoreboard(request);
            return Query(request).Contains("dates=2026&")
                ? Scoreboard(Event("nfl-1", "2026-09-13T17:00Z"), Event("nfl-pre", "2026-08-15T17:00Z", "preseason"))
                : Scoreboard();
        });

        // Act
        await service.SyncFullSeasonAsync(SeasonId);

        // Assert
        _requestedUrls.Where(u => u.Contains("/scoreboard")).Should().OnlyContain(u => u.Contains("dates=2026&limit=1000"));
        _insertedGameIds.Should().BeEquivalentTo(new[] { "nfl-1", "nfl-pre" });
        season.StartDate.Should().BeNull();
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }
}
