using System.Net;
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
        var service = CreateService(_ => Json(Fixture("nhl-playoffs-20260420")));

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
        var service = CreateService(_ => Json(Fixture("nhl-playoffs-20260420")));

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
        var service = CreateService(_ => Json(Fixture("nhl-series-completed-20260425")));

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
        var service = CreateService(_ => Json(Fixture("nhl-series-completed-20260425")));

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
        var service = CreateService(_ => Json(Fixture("nhl-series-completed-20260425")));

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
