using System.Net;
using System.Text.Json.Nodes;
using Domain.Common;
using Domain.Entities;
using FluentAssertions;
using Interface.Persistence;
using Interface.UseCases.Knockout;
using Microsoft.Extensions.Logging;
using Moq;
using UseCases.Liguilla.Espn;
using UseCases.Liguilla.Strategies;

namespace PickSportsApi.UnitTests.UseCasesTests.Knockout;

public class EspnKnockoutSyncServiceTests
{
    private const int LeagueId = 7;
    private const int SeasonId = 70;
    private const int FirstRoundNodeCount = 8;
    private const int DivAfcFirstNode = 7;
    private const int DivNfcFirstNode = 9;
    private const string NhlFormat = KnockoutFormatCodes.NhlBestOf7;

    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IKnockoutStrategyResolver> _resolver = new();
    private readonly Mock<IKnockoutFormatStrategy> _strategy = new();
    private readonly Mock<ILogger<EspnKnockoutSyncService>> _logger = new();
    private readonly List<LiguillaNode> _nodes = new();
    private readonly List<LiguillaNode> _added = new();
    private readonly League _league = new() { Id = LeagueId, Code = "nhl", KnockoutFormatCode = NhlFormat };

    public EspnKnockoutSyncServiceTests()
    {
        _strategy.Setup(s => s.FormatCode).Returns(NhlFormat);
        _strategy.Setup(s => s.Rounds).Returns(new[] { new KnockoutRound("R1", 1, FirstRoundNodeCount, 10, FirstRoundNodeCount) });
        _strategy.Setup(s => s.AdvanceWinners(It.IsAny<IReadOnlyList<LiguillaNode>>())).Returns(Array.Empty<LiguillaNode>());
        _strategy.Setup(s => s.ResolveFromEspn(It.IsAny<LiguillaNode>(), It.IsAny<EspnKnockoutTie>()))
            .Returns((LiguillaNode n, EspnKnockoutTie _) => new NodeOutcome(n.NodeNumber, false, null));
        _resolver.Setup(r => r.For(_league)).Returns(_strategy.Object);

        _uow.Setup(u => u.Leagues.GetAsync(LeagueId)).ReturnsAsync(_league);
        _uow.Setup(u => u.Seasons.GetCurrentSeasonAsync(LeagueId))
            .ReturnsAsync(new Season { Id = SeasonId, LeagueId = LeagueId, Year = 2026, IsCurrent = true });
        _uow.Setup(u => u.Liguilla.GetNodesBySeasonAsync(SeasonId, It.IsAny<CancellationToken>())).ReturnsAsync(_nodes);
        _uow.Setup(u => u.Liguilla.AddNodeAsync(It.IsAny<LiguillaNode>(), It.IsAny<CancellationToken>()))
            .Callback<LiguillaNode, CancellationToken>((n, _) => _added.Add(n))
            .Returns(Task.CompletedTask);
        _uow.Setup(u => u.Liguilla.UpdateNodeAsync(It.IsAny<LiguillaNode>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _uow.Setup(u => u.Save(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Espn", name + ".json"));

    private void SetTeams(params (int Id, string EspnId)[] teams) =>
        _uow.Setup(u => u.Teams.GetByLeagueIdAsync(LeagueId))
            .ReturnsAsync(teams.Select(t => new Team { Id = t.Id, EspnTeamId = t.EspnId, LeagueId = LeagueId }).ToList());

    private EspnKnockoutSyncService CreateService(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("EspnClient")).Returns(() => new HttpClient(new StubHandler(respond)));
        return new EspnKnockoutSyncService(_uow.Object, _resolver.Object, new EspnKnockoutParser(), factory.Object, _logger.Object);
    }

    private static HttpResponseMessage Json(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json) };

    /// <summary>NHL: standings level=3 reales para la ruta de standings y el scoreboard indicado para el resto.</summary>
    private static Func<HttpRequestMessage, HttpResponseMessage> NhlResponse(string scoreboardFixture) =>
        request => Json(request.RequestUri!.AbsolutePath.Contains("standings")
            ? Fixture("nhl-standings-2026-level3")
            : Fixture(scoreboardFixture));

    [Fact]
    public async Task SyncLeagueAsync_LeagueWithoutStrategy_ReturnsZeroAndDoesNotSave()
    {
        _resolver.Setup(r => r.For(_league)).Returns((IKnockoutFormatStrategy?)null);
        var service = CreateService(_ => Json("{}"));

        var changed = await service.SyncLeagueAsync(LeagueId);

        changed.Should().Be(0);
        _uow.Verify(u => u.Save(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SyncLeagueAsync_EspnFails_ReturnsZeroAndLogsWarning()
    {
        SetTeams((1, "15"), (2, "16"));
        var service = CreateService(_ => throw new HttpRequestException("sin red"));

        var changed = await service.SyncLeagueAsync(LeagueId);

        changed.Should().Be(0);
        _uow.Verify(u => u.Save(It.IsAny<CancellationToken>()), Times.Never);
        VerifyWarning(Times.AtLeastOnce());
    }

    [Fact]
    public async Task SyncLeagueAsync_PublishedSeries_SeedsShapeAndFillsEastNode()
    {
        SetTeams((1, "15"), (2, "16"));
        var service = CreateService(NhlResponse("nhl-playoffs-20260420"));

        var changed = await service.SyncLeagueAsync(LeagueId);

        changed.Should().BeGreaterThan(0);
        _added.Should().HaveCount(FirstRoundNodeCount);
        var filled = _added.Single(n => n.EspnEventGroupKey == "post-season:15-16");
        filled.Round.Should().Be("R1");
        filled.Conference.Should().Be("east");
        new[] { filled.TeamAId, filled.TeamBId }.Should().BeEquivalentTo(new int?[] { 1, 2 });
        _uow.Verify(u => u.Save(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SyncLeagueAsync_UnknownTeamInEspn_SkipsTieAndLogsWarningWithoutCreatingTeam()
    {
        SetTeams((1, "15"));
        var service = CreateService(NhlResponse("nhl-playoffs-20260420"));

        await service.SyncLeagueAsync(LeagueId);

        _added.Should().HaveCount(FirstRoundNodeCount);
        _added.Should().OnlyContain(n => n.EspnEventGroupKey == null && n.TeamAId == null);
        VerifyWarning(Times.AtLeastOnce());
        _uow.Verify(u => u.Teams.InsertAsync(It.IsAny<Team>()), Times.Never);
    }

    [Fact]
    public async Task SyncLeagueAsync_CompletedSeries_ResolvesNodeWithStrategyWinner()
    {
        const int carolinaId = 11;
        const int ottawaId = 12;
        SetTeams((carolinaId, "7"), (ottawaId, "14"));
        _strategy.Setup(s => s.ResolveFromEspn(It.IsAny<LiguillaNode>(), It.Is<EspnKnockoutTie>(t => t.WinnerEspnTeamId == "7")))
            .Returns((LiguillaNode n, EspnKnockoutTie _) => new NodeOutcome(n.NodeNumber, true, carolinaId));
        var service = CreateService(NhlResponse("nhl-series-completed-20260425"));

        await service.SyncLeagueAsync(LeagueId);

        var resolved = _added.Single(n => n.IsResolved);
        resolved.WinnerTeamId.Should().Be(carolinaId);
        resolved.ResolvedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task SyncLeagueAsync_CompletedSeriesWithRealNhlStrategy_ResolvesCarolinaOverOttawa()
    {
        const int carolinaId = 11;
        const int ottawaId = 12;
        _resolver.Setup(r => r.For(_league)).Returns(new NhlBestOfSevenStrategy());
        SetTeams((carolinaId, "7"), (ottawaId, "14"));
        var service = CreateService(NhlResponse("nhl-series-completed-20260425"));

        await service.SyncLeagueAsync(LeagueId);

        var resolved = _added.Single(n => n.IsResolved);
        resolved.Round.Should().Be("R1");
        new[] { resolved.TeamAId, resolved.TeamBId }.Should().BeEquivalentTo(new int?[] { carolinaId, ottawaId });
        resolved.WinnerTeamId.Should().Be(carolinaId);
        resolved.ResolvedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task SyncLeagueAsync_NodeAlreadyResolvedByAdmin_IsNotOverwritten()
    {
        const int adminWinnerId = 99;
        SetTeams((11, "7"), (12, "14"));
        var manual = new LiguillaNode
        {
            SeasonId = SeasonId, Round = "R1", NodeNumber = 1, Conference = "east", IsResolved = true,
            WinnerTeamId = adminWinnerId, TeamAId = 12, TeamBId = 11, EspnEventGroupKey = "post-season:14-7"
        };
        _nodes.Add(manual);
        var service = CreateService(NhlResponse("nhl-series-completed-20260425"));

        await service.SyncLeagueAsync(LeagueId);

        manual.WinnerTeamId.Should().Be(adminWinnerId);
        _strategy.Verify(s => s.ResolveFromEspn(manual, It.IsAny<EspnKnockoutTie>()), Times.Never);
    }

    [Fact]
    public async Task SyncLeagueAsync_NflStandings_PreSeedsTopSeedInDivisionalPerConference()
    {
        const int denverId = 21;
        const int seattleId = 22;
        const int topSeed = 1;
        var nfl = new NflReseededStrategy();
        _strategy.Setup(s => s.FormatCode).Returns(nfl.FormatCode);
        _strategy.Setup(s => s.Rounds).Returns(nfl.Rounds);
        SetTeams((denverId, "7"), (seattleId, "26"));
        var service = CreateService(request => Json(request.RequestUri!.AbsolutePath.Contains("standings")
            ? Fixture("nfl-standings-2025")
            : Fixture("nfl-postseason-20260111")));

        await service.SyncLeagueAsync(LeagueId);

        var afc = _added.Where(n => n.Round == "DIV" && n.Conference == "afc").OrderBy(n => n.NodeNumber).First();
        var nfc = _added.Where(n => n.Round == "DIV" && n.Conference == "nfc").OrderBy(n => n.NodeNumber).First();
        afc.NodeNumber.Should().Be(DivAfcFirstNode);
        afc.TeamAId.Should().Be(denverId);
        afc.SeedA.Should().Be(topSeed);
        nfc.NodeNumber.Should().Be(DivNfcFirstNode);
        nfc.TeamAId.Should().Be(seattleId);
        nfc.SeedA.Should().Be(topSeed);
    }

    [Fact]
    public async Task SyncLeagueAsync_NflWithoutStandingsSeeds_DoesNothingAndLogsWarning()
    {
        // Arrange
        var nfl = new NflReseededStrategy();
        _strategy.Setup(s => s.FormatCode).Returns(nfl.FormatCode);
        _strategy.Setup(s => s.Rounds).Returns(nfl.Rounds);
        SetTeams((21, "7"), (22, "26"));
        var service = CreateService(request => request.RequestUri!.AbsolutePath.Contains("standings")
            ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
            : Json(Fixture("nfl-postseason-20260111")));

        // Act
        var changed = await service.SyncLeagueAsync(LeagueId);

        // Assert
        changed.Should().Be(0);
        _added.Should().BeEmpty();
        _uow.Verify(u => u.Save(It.IsAny<CancellationToken>()), Times.Never);
        VerifyWarning(Times.AtLeastOnce());
    }

    private void ArrangeNflStrategyWithSeason(Season season)
    {
        var nfl = new NflReseededStrategy();
        _strategy.Setup(s => s.FormatCode).Returns(nfl.FormatCode);
        _strategy.Setup(s => s.Rounds).Returns(nfl.Rounds);
        _uow.Setup(u => u.Seasons.GetCurrentSeasonAsync(LeagueId)).ReturnsAsync(season);
        SetTeams((21, "7"), (22, "26"));
    }

    private static HttpResponseMessage NflResponse(HttpRequestMessage request) =>
        Json(request.RequestUri!.AbsolutePath.Contains("standings")
            ? Fixture("nfl-standings-2025")
            : Fixture("nfl-postseason-20260111"));

    [Fact]
    public async Task SyncLeagueAsync_PlayoffsOfPreviousSeason_AreDiscardedAndNothingIsSeeded()
    {
        // Arrange: la temporada vigente arranca en agosto de 2026; los playoffs de enero de 2026 son de la anterior
        ArrangeNflStrategyWithSeason(new Season
        {
            Id = SeasonId, LeagueId = LeagueId, Year = 2026, IsCurrent = true,
            StartDate = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2027, 2, 15, 0, 0, 0, DateTimeKind.Utc)
        });
        var service = CreateService(NflResponse);

        // Act
        var changed = await service.SyncLeagueAsync(LeagueId);

        // Assert
        changed.Should().Be(0);
        _added.Should().BeEmpty();
        _nodes.Should().NotContain(n => n.IsResolved);
        _uow.Verify(u => u.Save(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SyncLeagueAsync_PlayoffsInsideSeasonWindow_AreSeeded()
    {
        // Arrange: temporada 2025-08-01..2026-08-01, los playoffs de enero de 2026 le pertenecen
        ArrangeNflStrategyWithSeason(new Season
        {
            Id = SeasonId, LeagueId = LeagueId, Year = 2025, IsCurrent = true,
            StartDate = new DateTime(2025, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        var service = CreateService(NflResponse);

        // Act
        var changed = await service.SyncLeagueAsync(LeagueId);

        // Assert
        changed.Should().BeGreaterThan(0);
        _added.Should().Contain(n => n.Round == "DIV" && n.TeamAId == 21 && n.SeedA == 1);
    }

    /// <summary>Devuelve el fixture con la ventana <c>leagues[0].season</c> reemplazada (o sin ella si ambas fechas son null).</summary>
    private static string WithEspnSeasonWindow(string fixtureJson, string? start, string? end)
    {
        var root = JsonNode.Parse(fixtureJson)!;
        var season = root["leagues"]![0]!["season"]!.AsObject();
        season.Remove("startDate");
        season.Remove("endDate");
        if (start != null) season["startDate"] = start;
        if (end != null) season["endDate"] = end;
        return root.ToJsonString();
    }

    private static Season NflSeasonWithoutDates() =>
        new() { Id = SeasonId, LeagueId = LeagueId, Year = 2026, IsCurrent = true };

    private HttpResponseMessage NflResponseWithWindow(HttpRequestMessage request, string? start, string? end) =>
        Json(request.RequestUri!.AbsolutePath.Contains("standings")
            ? Fixture("nfl-standings-2025")
            : WithEspnSeasonWindow(Fixture("nfl-postseason-20260111"), start, end));

    [Fact]
    public async Task SyncLeagueAsync_SeasonWithoutDatesAndEspnWindowOfCurrentSeason_DiscardsPreviousSeasonPlayoffs()
    {
        // Arrange: NFL 2026 sin fechas; ESPN anuncia la ventana 2026-08-06..2027-02-16, los cruces del fixture son de enero de 2026
        ArrangeNflStrategyWithSeason(NflSeasonWithoutDates());
        var service = CreateService(r => NflResponseWithWindow(r, "2026-08-06T07:00Z", "2027-02-16T07:59Z"));

        // Act
        var changed = await service.SyncLeagueAsync(LeagueId);

        // Assert
        changed.Should().Be(0);
        _added.Should().BeEmpty();
        _uow.Verify(u => u.Save(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SyncLeagueAsync_SeasonWithoutDatesAndEspnWindowContainingTies_SeedsThem()
    {
        // Arrange: ventana ESPN 2025-07-31..2026-02-12 contiene los playoffs de enero de 2026
        ArrangeNflStrategyWithSeason(NflSeasonWithoutDates());
        var service = CreateService(r => NflResponseWithWindow(r, "2025-07-31T07:00Z", "2026-02-12T07:59Z"));

        // Act
        var changed = await service.SyncLeagueAsync(LeagueId);

        // Assert
        changed.Should().BeGreaterThan(0);
        _added.Should().Contain(n => n.Round == "DIV" && n.TeamAId == 21 && n.SeedA == 1);
    }

    [Fact]
    public async Task SyncLeagueAsync_NoWindowInSeasonNorEspn_DoesNothingAndLogsWarning()
    {
        // Arrange
        ArrangeNflStrategyWithSeason(NflSeasonWithoutDates());
        var service = CreateService(r => NflResponseWithWindow(r, null, null));

        // Act
        var changed = await service.SyncLeagueAsync(LeagueId);

        // Assert
        changed.Should().Be(0);
        _added.Should().BeEmpty();
        _uow.Verify(u => u.Save(It.IsAny<CancellationToken>()), Times.Never);
        VerifyWarning(Times.AtLeastOnce());
    }

    [Fact]
    public async Task SyncLeagueAsync_RequestsEspnBeforeLoadingNodes_SoAdminResolutionsMadeMeanwhileAreNotOverwritten()
    {
        // Arrange
        var calls = new List<string>();
        SetTeams((1, "15"), (2, "16"));
        _uow.Setup(u => u.Liguilla.GetNodesBySeasonAsync(SeasonId, It.IsAny<CancellationToken>()))
            .Callback<int, CancellationToken>((_, _) => calls.Add("nodes"))
            .ReturnsAsync(_nodes);
        var nhl = NhlResponse("nhl-playoffs-20260420");
        var service = CreateService(request =>
        {
            calls.Add("espn");
            return nhl(request);
        });

        // Act
        await service.SyncLeagueAsync(LeagueId);

        // Assert
        calls.Should().Contain("nodes");
        calls.IndexOf("nodes").Should().BeGreaterThan(calls.LastIndexOf("espn"));
    }

    [Fact]
    public async Task SyncLeagueAsync_NhlSecondVsThirdSeriesOfMetropolitan_GoesToNode4AndNotTheFirstFreeNode()
    {
        // Arrange: PIT (seed 7) y PHI (seed 8), ambos Metropolitana y ninguno de seed <= 2: es el "2o vs 3o" de Metropolitana
        const int philadelphiaId = 1;
        const int pittsburghId = 2;
        const int metropolitanSecondVsThirdNode = 4;
        const int pittsburghSeed = 7;
        const int philadelphiaSeed = 8;
        _resolver.Setup(r => r.For(_league)).Returns(new NhlBestOfSevenStrategy());
        SetTeams((philadelphiaId, "15"), (pittsburghId, "16"));
        var service = CreateService(NhlResponse("nhl-playoffs-20260420"));

        // Act
        await service.SyncLeagueAsync(LeagueId);

        // Assert
        var node = _added.Single(n => n.EspnEventGroupKey == "post-season:15-16");
        node.NodeNumber.Should().Be(metropolitanSecondVsThirdNode);
        node.Conference.Should().Be("east");
        node.SeedA.Should().Be(node.TeamAId == pittsburghId ? pittsburghSeed : philadelphiaSeed);
        node.SeedB.Should().Be(node.TeamBId == pittsburghId ? pittsburghSeed : philadelphiaSeed);
        _added.Where(n => n.NodeNumber != metropolitanSecondVsThirdNode && n.Round == "R1").Should().OnlyContain(n => n.TeamAId == null);
    }

    [Fact]
    public async Task SyncLeagueAsync_NhlDivisionWinnerAgainstWildcardOfSameDivision_GoesToWinnerNodeNotSecondVsThird()
    {
        // Arrange: BUF (seed 2, Atlantico) vs BOS (comodin seed 5, tambien Atlantico) llega DESPUES del 2o vs 3o (TB seed 3 vs MTL seed 4)
        const int buffaloId = 1;
        const int bostonId = 2;
        const int tampaId = 3;
        const int montrealId = 4;
        const int atlanticWinnerNode = 1;
        const int atlanticSecondVsThirdNode = 2;
        const int buffaloSeed = 2;
        const int bostonSeed = 5;
        _resolver.Setup(r => r.For(_league)).Returns(new NhlBestOfSevenStrategy());
        SetTeams((buffaloId, "2"), (bostonId, "1"), (tampaId, "20"), (montrealId, "10"));

        var parser = new Mock<IEspnKnockoutParser>();
        parser.Setup(p => p.SupportedFormatCodes).Returns(new[] { NhlFormat });
        parser.Setup(p => p.ParseScoreboard(It.IsAny<string>(), NhlFormat)).Returns(new[]
        {
            NhlTie("post-season:20-10", "20", "10"),
            NhlTie("post-season:2-1", "2", "1")
        });
        parser.Setup(p => p.ParseNhlPlayoffSeeds(It.IsAny<string>())).Returns(new Dictionary<string, NhlPlayoffSeed>
        {
            ["2"] = new("east", "atlantic", buffaloSeed, true),
            ["1"] = new("east", "atlantic", bostonSeed, false),
            ["20"] = new("east", "atlantic", 3, false),
            ["10"] = new("east", "atlantic", 4, false)
        });
        var service = CreateService(parser.Object, _ => Json("{}"));

        // Act
        await service.SyncLeagueAsync(LeagueId);

        // Assert
        var winnerNode = _added.Single(n => n.Round == "R1" && n.NodeNumber == atlanticWinnerNode);
        winnerNode.EspnEventGroupKey.Should().Be("post-season:2-1");
        winnerNode.SeedA.Should().Be(buffaloSeed);
        winnerNode.SeedB.Should().Be(bostonSeed);
        var secondVsThird = _added.Single(n => n.Round == "R1" && n.NodeNumber == atlanticSecondVsThirdNode);
        secondVsThird.EspnEventGroupKey.Should().Be("post-season:20-10");
    }

    [Fact]
    public async Task SyncLeagueAsync_NhlCentralSeedTwoNonWinnerAgainstSeedThree_GoesToSecondVsThirdNode6NotNode5()
    {
        // Arrange: con los standings reales DAL (Central, seed 2) NO es ganador de division; COL (seed 1) si
        const int dallasId = 1;
        const int minnesotaId = 2;
        const int centralSecondVsThirdNode = 6;
        var service = CreateServiceWithRealSeeds(WestTie("post-season:9-30", "9", "30"));
        SetTeams((dallasId, "9"), (minnesotaId, "30"));
        _resolver.Setup(r => r.For(_league)).Returns(new NhlBestOfSevenStrategy());

        // Act
        await service.SyncLeagueAsync(LeagueId);

        // Assert
        _added.Single(n => n.EspnEventGroupKey == "post-season:9-30").NodeNumber.Should().Be(centralSecondVsThirdNode);
    }

    [Fact]
    public async Task SyncLeagueAsync_NhlCentralDivisionWinnerAgainstWildcard_GoesToWinnerNode5()
    {
        // Arrange: COL (ganador de Central, seed 1) vs UTA (comodin de Central, seed 6)
        const int coloradoId = 1;
        const int utahId = 2;
        const int centralWinnerNode = 5;
        var service = CreateServiceWithRealSeeds(WestTie("post-season:17-129764", "17", "129764"));
        SetTeams((coloradoId, "17"), (utahId, "129764"));
        _resolver.Setup(r => r.For(_league)).Returns(new NhlBestOfSevenStrategy());

        // Act
        await service.SyncLeagueAsync(LeagueId);

        // Assert
        _added.Single(n => n.EspnEventGroupKey == "post-season:17-129764").NodeNumber.Should().Be(centralWinnerNode);
    }

    [Fact]
    public async Task SyncLeagueAsync_NhlPacificWinnerSeedFourAgainstWildcard_GoesToPacificWinnerNode7()
    {
        // Arrange: VGK es ganador de Pacifico pese a ser seed 4 de conferencia; su rival es comodin de otra division (UTA)
        const int vegasId = 1;
        const int utahId = 2;
        const int pacificWinnerNode = 7;
        const int vegasSeed = 4;
        var service = CreateServiceWithRealSeeds(WestTie("post-season:37-129764", "37", "129764"));
        SetTeams((vegasId, "37"), (utahId, "129764"));
        _resolver.Setup(r => r.For(_league)).Returns(new NhlBestOfSevenStrategy());

        // Act
        await service.SyncLeagueAsync(LeagueId);

        // Assert
        var node = _added.Single(n => n.EspnEventGroupKey == "post-season:37-129764");
        node.NodeNumber.Should().Be(pacificWinnerNode);
        (node.TeamAId == vegasId ? node.SeedA : node.SeedB).Should().Be(vegasSeed);
    }

    /// <summary>Scoreboard sintetico con ties dados; los seeds salen del parser REAL sobre los standings reales.</summary>
    private EspnKnockoutSyncService CreateServiceWithRealSeeds(params EspnKnockoutTie[] ties)
    {
        var real = new EspnKnockoutParser();
        var parser = new Mock<IEspnKnockoutParser>();
        parser.Setup(p => p.SupportedFormatCodes).Returns(new[] { NhlFormat });
        parser.Setup(p => p.ParseScoreboard(It.IsAny<string>(), NhlFormat)).Returns(ties);
        parser.Setup(p => p.ParseNhlPlayoffSeeds(It.IsAny<string>()))
            .Returns((string json) => real.ParseNhlPlayoffSeeds(json));
        return CreateService(parser.Object, NhlResponse("nhl-playoffs-20260420"));
    }

    private static EspnKnockoutTie WestTie(string groupKey, string homeEspnId, string awayEspnId) =>
        NhlTie(groupKey, homeEspnId, awayEspnId) with { Conference = "west" };

    [Fact]
    public async Task SyncLeagueAsync_NhlWithoutStandingsSeeds_DoesNothingAndLogsWarning()
    {
        // Arrange
        SetTeams((1, "15"), (2, "16"));
        var service = CreateService(request => request.RequestUri!.AbsolutePath.Contains("standings")
            ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
            : Json(Fixture("nhl-playoffs-20260420")));

        // Act
        var changed = await service.SyncLeagueAsync(LeagueId);

        // Assert
        changed.Should().Be(0);
        _added.Should().BeEmpty();
        _uow.Verify(u => u.Save(It.IsAny<CancellationToken>()), Times.Never);
        VerifyWarning(Times.AtLeastOnce());
    }

    [Fact]
    public async Task SyncLeagueAsync_NhlRequestsStandingsWithLevelThreeForTheSeasonYear()
    {
        // Arrange
        var requested = new List<string>();
        SetTeams((1, "15"), (2, "16"));
        var nhl = NhlResponse("nhl-playoffs-20260420");
        var service = CreateService(request =>
        {
            requested.Add(request.RequestUri!.PathAndQuery);
            return nhl(request);
        });

        // Act
        await service.SyncLeagueAsync(LeagueId);

        // Assert
        requested.Should().Contain(url => url.Contains("/standings") && url.Contains("season=2026") && url.Contains("level=3"));
    }

    private EspnKnockoutSyncService CreateService(IEspnKnockoutParser parser, Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        _uow.Setup(u => u.Seasons.GetCurrentSeasonAsync(LeagueId)).ReturnsAsync(new Season
        {
            Id = SeasonId, LeagueId = LeagueId, Year = 2026, IsCurrent = true,
            StartDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("EspnClient")).Returns(() => new HttpClient(new StubHandler(respond)));
        return new EspnKnockoutSyncService(_uow.Object, _resolver.Object, parser, factory.Object, _logger.Object);
    }

    private static EspnKnockoutTie NhlTie(string groupKey, string homeEspnId, string awayEspnId) => new()
    {
        GroupKey = groupKey,
        Headline = "East 1st Round - Game 1",
        HomeEspnTeamId = homeEspnId,
        AwayEspnTeamId = awayEspnId,
        Conference = "east",
        KickoffUtc = new DateTime(2026, 4, 20, 23, 0, 0, DateTimeKind.Utc)
    };

    private void VerifyWarning(Times times) =>
        _logger.Verify(l => l.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), times);

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_respond(request));
    }
}
