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

public class EspnKnockoutSyncFmfTests
{
    private const int LeagueId = 7;
    private const int SeasonId = 70;
    private const string QfSlug = "clausura---quarterfinals";
    private const string SfSlug = "clausura---semifinals";

    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IKnockoutStrategyResolver> _resolver = new();
    private readonly Mock<IEspnKnockoutParser> _parser = new();
    private readonly Mock<ILogger<EspnKnockoutSyncService>> _logger = new();
    private readonly List<LiguillaNode> _nodes = new();
    private readonly List<LiguillaNode> _added = new();
    private readonly League _league = new()
    {
        Id = LeagueId,
        Code = "mx.1",
        KnockoutFormatCode = KnockoutFormatCodes.FmfLiguilla
    };

    public EspnKnockoutSyncFmfTests()
    {
        // Estrategia FMF REAL: no se mockea ni la re-siembra ni la resolucion.
        _resolver.Setup(r => r.For(_league)).Returns(new FmfLiguillaStrategy());

        _parser.Setup(p => p.SupportedFormatCodes).Returns(new[] { KnockoutFormatCodes.FmfLiguilla });
        _parser.Setup(p => p.ParseScoreboard(It.IsAny<string>(), KnockoutFormatCodes.FmfLiguilla))
            .Returns(Array.Empty<EspnKnockoutTie>());
        SetSeeds(Enumerable.Range(1, 8).ToDictionary(n => "e" + n, n => n));

        _uow.Setup(u => u.Leagues.GetAsync(LeagueId)).ReturnsAsync(_league);
        _uow.Setup(u => u.Seasons.GetCurrentSeasonAsync(LeagueId)).ReturnsAsync(new Season
        {
            Id = SeasonId,
            LeagueId = LeagueId,
            Year = 2026,
            IsCurrent = true,
            StartDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        _uow.Setup(u => u.Liguilla.GetNodesBySeasonAsync(SeasonId, It.IsAny<CancellationToken>())).ReturnsAsync(_nodes);
        _uow.Setup(u => u.Liguilla.AddNodeAsync(It.IsAny<LiguillaNode>(), It.IsAny<CancellationToken>()))
            .Callback<LiguillaNode, CancellationToken>((n, _) => _added.Add(n))
            .Returns(Task.CompletedTask);
        _uow.Setup(u => u.Liguilla.UpdateNodeAsync(It.IsAny<LiguillaNode>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uow.Setup(u => u.Save(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        SetTeams();
    }

    [Fact]
    public async Task SyncLeagueAsync_FourQuarterFinalTies_SeedsNodesOneToFourBySeedOrder()
    {
        // Arrange: 4 cruces publicados con local/visita aleatorio (el mejor sembrado es visita en 2v7 y 4v5).
        SetTies(
            Qf(1, 8, bestIsAway: false),
            Qf(2, 7, bestIsAway: true),
            Qf(3, 6, bestIsAway: false),
            Qf(4, 5, bestIsAway: true));
        var service = CreateService();

        // Act
        await service.SyncLeagueAsync(LeagueId);

        // Assert
        var qf = _added.Where(n => n.Round == "QF").OrderBy(n => n.NodeNumber).ToList();
        qf.Should().HaveCount(4);
        qf.Select(n => n.NodeNumber).Should().BeEquivalentTo(new[] { 1, 2, 3, 4 });

        qf[0].TeamAId.Should().Be(1);
        qf[0].TeamBId.Should().Be(8);
        qf[0].SeedA.Should().Be(1);
        qf[0].SeedB.Should().Be(8);
        qf[0].KickoffUtc.Should().NotBeNull();
        qf[0].EspnEventGroupKey.Should().Be("clausura:qf-1-8");

        qf[1].TeamAId.Should().Be(2);
        qf[1].TeamBId.Should().Be(7);
        qf[1].SeedA.Should().Be(2);
        qf[1].SeedB.Should().Be(7);
        qf[1].KickoffUtc.Should().NotBeNull();
        qf[1].EspnEventGroupKey.Should().Be("clausura:qf-2-7");

        qf[2].TeamAId.Should().Be(3);
        qf[2].TeamBId.Should().Be(6);
        qf[2].SeedA.Should().Be(3);
        qf[2].SeedB.Should().Be(6);
        qf[2].EspnEventGroupKey.Should().Be("clausura:qf-3-6");

        qf[3].TeamAId.Should().Be(4);
        qf[3].TeamBId.Should().Be(5);
        qf[3].SeedA.Should().Be(4);
        qf[3].SeedB.Should().Be(5);
        qf[3].EspnEventGroupKey.Should().Be("clausura:qf-4-5");

        _added.Should().OnlyContain(n => n.Active);
    }

    [Fact]
    public async Task SyncLeagueAsync_QuarterFinalCompletedWithUpset_ResolvesNodeOneWithWinnerTeam()
    {
        // Arrange: el nodo 1 es 1 vs 8 y gana el 8 (visita, peor sembrado).
        SetTies(Qf(1, 8, bestIsAway: false, winner: 8));
        var service = CreateService();

        // Act
        await service.SyncLeagueAsync(LeagueId);

        // Assert
        var node = _added.Single(n => n.Round == "QF" && n.NodeNumber == 1);
        node.IsResolved.Should().BeTrue();
        node.WinnerTeamId.Should().Be(8);
        node.ResolvedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task SyncLeagueAsync_AllQuarterFinalsCompleted_ReseedsSemifinalsWithRealSeeds()
    {
        // Arrange: ganadores 8 (nodo 1), 2 (nodo 2), 3 (nodo 3) y 5 (nodo 4); hay no-favoritos en 1 y 4.
        SetTies(
            Qf(1, 8, bestIsAway: false, winner: 8),
            Qf(2, 7, bestIsAway: true, winner: 2),
            Qf(3, 6, bestIsAway: false, winner: 3),
            Qf(4, 5, bestIsAway: true, winner: 5));
        var service = CreateService();

        // Act
        await service.SyncLeagueAsync(LeagueId);

        // Assert: el nodo 5 se siembra con el mejor (2) vs el peor (8) y el 6 con 3 vs 5.
        var sf = _added.Where(n => n.Round == "SF").OrderBy(n => n.NodeNumber).ToList();
        sf.Should().HaveCount(2);
        sf.Should().OnlyContain(n =>
            n.TeamAId.HasValue && n.TeamBId.HasValue && n.SeedA.HasValue && n.SeedB.HasValue);

        var sfSeeds = sf.SelectMany(n => new[] { n.SeedA.GetValueOrDefault(), n.SeedB.GetValueOrDefault() }).ToList();
        sfSeeds.Should().BeEquivalentTo(new[] { 2, 3, 5, 8 });

        var node5 = sf.Single(n => n.NodeNumber == 5);
        new[] { node5.SeedA.GetValueOrDefault(), node5.SeedB.GetValueOrDefault() }
            .Should().BeEquivalentTo(new[] { 2, 8 });

        // El default (8) solo puede aparecer por el equipo 8, que si gano su serie.
        var node6 = sf.Single(n => n.NodeNumber == 6);
        new[] { node6.SeedA.GetValueOrDefault(), node6.SeedB.GetValueOrDefault() }
            .Should().BeEquivalentTo(new[] { 3, 5 });
    }

    [Fact]
    public async Task SyncLeagueAsync_QuarterFinalResolvedByAdmin_IsNotOverwritten()
    {
        // Arrange: el admin ya resolvio el nodo 1 con un ganador manual (99).
        const int adminWinnerId = 99;
        var manual = new LiguillaNode
        {
            SeasonId = SeasonId,
            Round = "QF",
            NodeNumber = 1,
            TeamAId = 1,
            TeamBId = 8,
            IsResolved = true,
            WinnerTeamId = adminWinnerId,
            EspnEventGroupKey = "clausura:qf-1-8",
            Active = true
        };
        _nodes.Add(manual);
        SetTies(Qf(1, 8, bestIsAway: false, winner: 8));
        var service = CreateService();

        // Act
        await service.SyncLeagueAsync(LeagueId);

        // Assert
        manual.WinnerTeamId.Should().Be(adminWinnerId);
        manual.IsResolved.Should().BeTrue();
    }

    [Fact]
    public async Task SyncLeagueAsync_WithoutFmfSeeds_ReturnsZeroAndWarns()
    {
        // Arrange: standings sin tabla (diccionario vacio).
        SetSeeds(new Dictionary<string, int>());
        SetTies(Qf(1, 8, bestIsAway: false));
        var service = CreateService();

        // Act
        var changed = await service.SyncLeagueAsync(LeagueId);

        // Assert
        changed.Should().Be(0);
        _added.Should().BeEmpty();
        _uow.Verify(u => u.Save(It.IsAny<CancellationToken>()), Times.Never);
        VerifyWarning(Times.AtLeastOnce());
    }

    [Fact]
    public async Task SyncLeagueAsync_AllTiesOutsideSeasonWindow_ReturnsZeroAndWarns()
    {
        // Arrange: los cruces son de enero de 2025, fuera de la ventana 2026-04-01..2026-07-01.
        var outside = new DateTime(2025, 1, 10, 23, 0, 0, DateTimeKind.Utc);
        SetTies(
            Qf(1, 8, bestIsAway: false) with { KickoffUtc = outside },
            Qf(2, 7, bestIsAway: true) with { KickoffUtc = outside },
            Qf(3, 6, bestIsAway: false) with { KickoffUtc = outside },
            Qf(4, 5, bestIsAway: true) with { KickoffUtc = outside });
        var service = CreateService();

        // Act
        var changed = await service.SyncLeagueAsync(LeagueId);

        // Assert
        changed.Should().Be(0);
        _added.Should().BeEmpty();
        _uow.Verify(u => u.Save(It.IsAny<CancellationToken>()), Times.Never);
        VerifyWarningContaining("[EspnKnockoutSync] Todos los cruces quedaron fuera de la ventana");
    }

    [Fact]
    public async Task SyncLeagueAsync_PublishedSemiFinal_FillsKeyAndKickoffWithoutOverwritingSeeds()
    {
        // Arrange: nodo 5 ya sembrado por AdvanceWinners (2 vs 7) y ESPN publica esa semifinal.
        var seeded = new LiguillaNode
        {
            SeasonId = SeasonId,
            Round = "SF",
            NodeNumber = 5,
            TeamAId = 2,
            TeamBId = 7,
            SeedA = 2,
            SeedB = 7,
            Active = true
        };
        _nodes.Add(seeded);
        SetTies(Tie("clausura:sf-2-7", 2, 7, SfSlug));
        var service = CreateService();

        // Act
        await service.SyncLeagueAsync(LeagueId);

        // Assert
        seeded.EspnEventGroupKey.Should().Be("clausura:sf-2-7");
        seeded.KickoffUtc.Should().NotBeNull();
        seeded.SeedA.Should().Be(2);
        seeded.SeedB.Should().Be(7);
        seeded.TeamAId.Should().Be(2);
        seeded.TeamBId.Should().Be(7);
    }

    [Fact]
    public async Task SyncLeagueAsync_QuarterFinalSlugInAnotherCaseAndPrefix_StillMapsToQf()
    {
        // Arrange: "Apertura---QuarterFinals" debe mapear al mismo bloque QF (OrdinalIgnoreCase).
        var slug = "Apertura---QuarterFinals";
        SetTies(
            Tie("apertura:qf-1-8", 1, 8, slug),
            Tie("apertura:qf-2-7", 7, 2, slug),
            Tie("apertura:qf-3-6", 3, 6, slug),
            Tie("apertura:qf-4-5", 5, 4, slug));
        var service = CreateService();

        // Act
        await service.SyncLeagueAsync(LeagueId);

        // Assert
        var qf = _added.Where(n => n.Round == "QF").OrderBy(n => n.NodeNumber).ToList();
        qf.Should().HaveCount(4);
        qf.Should().OnlyContain(n => n.TeamAId.HasValue && n.TeamBId.HasValue);
        qf.Select(n => n.EspnEventGroupKey).Should().BeEquivalentTo(new[]
        {
            "apertura:qf-1-8", "apertura:qf-2-7", "apertura:qf-3-6", "apertura:qf-4-5"
        });
    }

    private void SetTeams() =>
        _uow.Setup(u => u.Teams.GetByLeagueIdAsync(LeagueId))
            .ReturnsAsync(Enumerable.Range(1, 8)
                .Select(n => new Team { Id = n, EspnTeamId = "e" + n, LeagueId = LeagueId })
                .ToList());

    private void SetSeeds(Dictionary<string, int> seeds) =>
        _parser.Setup(p => p.ParseFmfPlayoffSeeds(It.IsAny<string>())).Returns(seeds);

    private void SetTies(params EspnKnockoutTie[] ties) =>
        _parser.Setup(p => p.ParseScoreboard(It.IsAny<string>(), KnockoutFormatCodes.FmfLiguilla)).Returns(ties);

    private EspnKnockoutSyncService CreateService()
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("EspnClient")).Returns(() => new HttpClient(new StubHandler()));
        return new EspnKnockoutSyncService(_uow.Object, _resolver.Object, _parser.Object, factory.Object, _logger.Object);
    }

    private static EspnKnockoutTie Tie(string groupKey, int homeTeam, int awayTeam, string roundSlug, int? winner = null) => new()
    {
        GroupKey = groupKey,
        Headline = "Liguilla - Cruce",
        HomeEspnTeamId = "e" + homeTeam,
        AwayEspnTeamId = "e" + awayTeam,
        RoundSlug = roundSlug,
        KickoffUtc = new DateTime(2026, 5, 10, 23, 0, 0, DateTimeKind.Utc),
        SeriesCompleted = winner.HasValue,
        WinnerEspnTeamId = winner.HasValue ? "e" + winner.Value : null
    };

    /// <summary>Cruce de cuartos 1v8, 2v7, 3v6, 4v5; <paramref name="bestIsAway"/> publica al mejor sembrado como visita.</summary>
    private static EspnKnockoutTie Qf(int bestSeed, int worstSeed, bool bestIsAway, int? winner = null) =>
        Tie(
            $"clausura:qf-{bestSeed}-{worstSeed}",
            bestIsAway ? worstSeed : bestSeed,
            bestIsAway ? bestSeed : worstSeed,
            QfSlug,
            winner);

    private void VerifyWarning(Times times) =>
        _logger.Verify(l => l.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), times);

    private void VerifyWarningContaining(string fragment) =>
        _logger.Verify(l => l.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains(fragment)),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.AtLeastOnce());

    private sealed class StubHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });
    }
}
