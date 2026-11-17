using System.Net;
using Domain.Common;
using Domain.Entities;
using FluentAssertions;
using Interface.Persistence;
using Interface.UseCases.Knockout;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Persistence.Context;
using Persistence.Interceptors;
using Persistence.Repositories;
using UseCases.Liguilla;
using UseCases.Liguilla.Simulation;
using UseCases.Liguilla.Strategies;
using Xunit;

namespace PickSportsApi.UnitTests.UseCasesTests.Knockout;

/// <summary>Simulacion QA contra el repositorio REAL (EF InMemory): lo sembrado debe poder leerse despues (regla anti-regresion de Active).</summary>
public class KnockoutSimulationServiceTests : IDisposable
{
    private const int QuinielaId = 5;
    private const int LeagueId = 9;
    private const int SeasonId = 90;
    private const int UefaNodeCount = 15;
    private const int NflNodeCount = 13;
    private const int FirstTeamId = 100;
    private const string StandingsBody = "{}";
    private const string RealEspnResolvedMessage = "La temporada ya tiene cruces reales de ESPN o resultados; la simulación solo aplica a campos vacíos o simulados.";
    private const string BracketsSubmittedMessage = "Ya hay pronósticos enviados en esta temporada; la simulación los dejaría inconsistentes.";

    private readonly ApplicationDbContext _context;
    private readonly LiguillaRepository _repository;
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly List<Team> _teams = new();
    private readonly League _league = new() { Id = LeagueId, Code = "test", EspnApiPath = "soccer/uefa.champions" };
    private readonly Quiniela _quiniela;

    public KnockoutSimulationServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options, new AuditableEntitySaveChangesInterceptor());
        _repository = new LiguillaRepository(_context);

        _quiniela = NewQuiniela(SeasonId);
        _quiniela.League = _league;

        _uow.Setup(u => u.Quinielas.GetAsync(QuinielaId)).ReturnsAsync(_quiniela);
        _uow.Setup(u => u.Leagues.GetAsync(LeagueId)).ReturnsAsync(_league);
        // El repositorio real devuelve equipos desconectados (AsNoTracking): asignarlos como navegacion de un nodo hace que EF los INSERTE de nuevo.
        _uow.Setup(u => u.Teams.GetByLeagueIdAsync(LeagueId))
            .ReturnsAsync(() => _context.Teams.AsNoTracking().Where(t => t.LeagueId == LeagueId).ToList());
        _uow.Setup(u => u.Liguilla).Returns(_repository);
        _uow.Setup(u => u.Save(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken ct) => _context.SaveChangesAsync(ct));
    }

    public void Dispose() => _context.Dispose();

    private static Quiniela NewQuiniela(int? seasonId) =>
        new() { Id = QuinielaId, LeagueId = LeagueId, SeasonId = seasonId, Name = "Quiniela QA", InviteCode = "QA123" };

    private KnockoutSimulationService BuildService(
        string formatCode, IKnockoutFormatStrategy strategy, IReadOnlyList<SimulatedTie> ties, HttpStatusCode status = HttpStatusCode.OK)
    {
        _league.KnockoutFormatCode = formatCode;

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("EspnClient"))
            .Returns(() => new HttpClient(new StubHandler(status, StandingsBody)));

        var resolver = new KnockoutStrategyResolver(new[] { strategy });
        return new KnockoutSimulationService(
            _uow.Object, resolver, new IKnockoutSimulationBuilder[] { new FakeBuilder(formatCode, ties) },
            factory.Object, NullLogger<KnockoutSimulationService>.Instance);
    }

    private void AddTeams(int count)
    {
        for (var i = 1; i <= count; i++)
        {
            _teams.Add(new Team
            {
                Id = FirstTeamId + i,
                EspnTeamId = i.ToString(),
                LeagueId = LeagueId,
                Name = "Equipo " + i,
                DisplayName = "Equipo " + i,
                Abbreviation = "E" + i
            });
        }

        // Los equipos viven en la misma base que los nodos, como en produccion (el repositorio los devuelve rastreados).
        _context.Teams.AddRange(_teams);
        _context.SaveChanges();
    }

    private static List<SimulatedTie> UefaTies()
    {
        var ties = new List<SimulatedTie>();
        for (var node = 1; node <= 8; node++)
            ties.Add(new SimulatedTie(node, "R16", null, node.ToString(), node, (17 - node).ToString(), 17 - node));
        return ties;
    }

    [Fact]
    public async Task SeedAsync_EmptySeason_CreatesWholeShapeReadableThroughRealRepositoryAndActive()
    {
        // Arrange
        AddTeams(16);
        var service = BuildService(KnockoutFormatCodes.UefaTwoLeg, new UefaTwoLegStrategy(), UefaTies());

        // Act
        var count = await service.SeedAsync(QuinielaId);

        // Assert
        count.Should().Be(UefaNodeCount);
        var nodes = await _repository.GetNodesBySeasonAsync(SeasonId);
        nodes.Should().HaveCount(UefaNodeCount);
        nodes.Should().OnlyContain(n => n.Active);
        nodes.Select(n => n.NodeNumber).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task SeedAsync_UefaTies_SeedsFirstRoundWithTeamsSeedsAndFutureKickoff()
    {
        // Arrange
        AddTeams(16);
        var service = BuildService(KnockoutFormatCodes.UefaTwoLeg, new UefaTwoLegStrategy(), UefaTies());

        // Act
        await service.SeedAsync(QuinielaId);

        // Assert
        var nodes = await _repository.GetNodesBySeasonAsync(SeasonId);
        var first = nodes.Single(n => n.NodeNumber == 1);
        first.Round.Should().Be("R16");
        first.TeamAId.Should().Be(FirstTeamId + 1);
        first.TeamBId.Should().Be(FirstTeamId + 16);
        first.SeedA.Should().Be(1);
        first.SeedB.Should().Be(16);
        first.KickoffUtc.Should().BeAfter(DateTime.UtcNow.AddDays(6));
        first.IsResolved.Should().BeFalse();
        first.WinnerTeamId.Should().BeNull();

        var laterRounds = nodes.Where(n => n.Round != "R16").ToList();
        laterRounds.Should().HaveCount(UefaNodeCount - 8);
        laterRounds.Should().OnlyContain(n => n.TeamAId == null && n.TeamBId == null && !n.IsResolved);
        nodes.Single(n => n.Round == "FINAL").KickoffUtc.Should().BeAfter(first.KickoffUtc!.Value);
    }

    [Fact]
    public async Task SeedAsync_CalledTwice_ReplacesInsteadOfDuplicating()
    {
        // Arrange
        AddTeams(16);
        var service = BuildService(KnockoutFormatCodes.UefaTwoLeg, new UefaTwoLegStrategy(), UefaTies());
        await service.SeedAsync(QuinielaId);

        // Act
        await service.SeedAsync(QuinielaId);

        // Assert
        var nodes = await _repository.GetNodesBySeasonAsync(SeasonId);
        nodes.Should().HaveCount(UefaNodeCount);
        nodes.Select(n => n.NodeNumber).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task SeedAsync_PreviousSimulatedField_IsReseededWithTheNewField()
    {
        // Arrange: campo simulado previo = sin clave de ESPN y sin resolver
        AddTeams(16);
        await _repository.AddNodeAsync(new LiguillaNode
        {
            SeasonId = SeasonId, Round = "R16", NodeNumber = 1, TeamAId = FirstTeamId + 3, TeamBId = FirstTeamId + 4,
            SeedA = 3, SeedB = 4, IsResolved = false, Active = true
        });
        await _context.SaveChangesAsync();
        var service = BuildService(KnockoutFormatCodes.UefaTwoLeg, new UefaTwoLegStrategy(), UefaTies());

        // Act
        await service.SeedAsync(QuinielaId);

        // Assert
        var nodes = await _repository.GetNodesBySeasonAsync(SeasonId);
        nodes.Should().HaveCount(UefaNodeCount);
        var first = nodes.Single(n => n.NodeNumber == 1);
        first.TeamAId.Should().Be(FirstTeamId + 1);
        first.TeamBId.Should().Be(FirstTeamId + 16);
        first.IsResolved.Should().BeFalse();
    }

    [Fact]
    public async Task SeedAsync_RealSeriesInProgress_ThrowsAndLeavesItUntouched()
    {
        // Arrange: serie real de ESPN en curso (clave presente, sin resolver)
        AddTeams(16);
        await _repository.AddNodeAsync(new LiguillaNode
        {
            SeasonId = SeasonId, Round = "R16", NodeNumber = 1, TeamAId = FirstTeamId + 3, TeamBId = FirstTeamId + 4,
            EspnEventGroupKey = "round-of-16:3-4", IsResolved = false, Active = true
        });
        await _context.SaveChangesAsync();
        var service = BuildService(KnockoutFormatCodes.UefaTwoLeg, new UefaTwoLegStrategy(), UefaTies());

        // Act
        var act = () => service.SeedAsync(QuinielaId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage(RealEspnResolvedMessage);
        var nodes = await _repository.GetNodesBySeasonAsync(SeasonId);
        nodes.Should().ContainSingle();
        nodes[0].EspnEventGroupKey.Should().Be("round-of-16:3-4");
        nodes[0].TeamAId.Should().Be(FirstTeamId + 3);
    }

    [Fact]
    public async Task SeedAsync_NodeResolvedByHandWithoutEspnKey_ThrowsAndLeavesItUntouched()
    {
        // Arrange: nodo resuelto a mano por el admin (resuelto, sin clave de ESPN)
        AddTeams(16);
        await _repository.AddNodeAsync(new LiguillaNode
        {
            SeasonId = SeasonId, Round = "R16", NodeNumber = 1, TeamAId = FirstTeamId + 3, TeamBId = FirstTeamId + 4,
            WinnerTeamId = FirstTeamId + 3, IsResolved = true, ResolvedAtUtc = DateTime.UtcNow, Active = true
        });
        await _context.SaveChangesAsync();
        var service = BuildService(KnockoutFormatCodes.UefaTwoLeg, new UefaTwoLegStrategy(), UefaTies());

        // Act
        var act = () => service.SeedAsync(QuinielaId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage(RealEspnResolvedMessage);
        var nodes = await _repository.GetNodesBySeasonAsync(SeasonId);
        nodes.Should().ContainSingle();
        nodes[0].IsResolved.Should().BeTrue();
        nodes[0].WinnerTeamId.Should().Be(FirstTeamId + 3);
    }

    [Fact]
    public async Task SeedAsync_SeasonWithSubmittedBrackets_ThrowsAndKeepsTheField()
    {
        // Arrange: campo simulado previo + un bracket ya enviado en la temporada
        AddTeams(16);
        var service = BuildService(KnockoutFormatCodes.UefaTwoLeg, new UefaTwoLegStrategy(), UefaTies());
        await service.SeedAsync(QuinielaId);
        _context.Quinielas.Add(NewQuiniela(SeasonId));
        _context.LiguillaBrackets.Add(new LiguillaBracket { QuinielaId = QuinielaId, UserId = 1, Active = true });
        await _context.SaveChangesAsync();
        var before = (await _repository.GetNodesBySeasonAsync(SeasonId)).Select(n => (n.NodeNumber, n.TeamAId)).ToList();

        // Act
        var act = () => service.SeedAsync(QuinielaId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage(BracketsSubmittedMessage);
        var after = (await _repository.GetNodesBySeasonAsync(SeasonId)).Select(n => (n.NodeNumber, n.TeamAId)).ToList();
        after.Should().Equal(before);
    }

    [Fact]
    public async Task AnyBracketsForSeasonAsync_QuinielaWithFixedSeason_FindsOnlyThatSeason()
    {
        // Arrange
        _context.Quinielas.Add(NewQuiniela(SeasonId));
        _context.LiguillaBrackets.Add(new LiguillaBracket { QuinielaId = QuinielaId, UserId = 1, Active = true });
        await _context.SaveChangesAsync();

        // Act
        var same = await _repository.AnyBracketsForSeasonAsync(SeasonId);
        var other = await _repository.AnyBracketsForSeasonAsync(SeasonId + 1);

        // Assert
        same.Should().BeTrue();
        other.Should().BeFalse();
    }

    [Fact]
    public async Task AnyBracketsForSeasonAsync_QuinielaWithoutSeason_CountsOnlyForTheCurrentSeasonOfItsLeague()
    {
        // Arrange
        _context.Seasons.AddRange(
            new Season { Id = SeasonId, LeagueId = LeagueId, Name = "Actual", IsCurrent = true, Active = true },
            new Season { Id = SeasonId + 1, LeagueId = LeagueId, Name = "Pasada", IsCurrent = false, Active = true });
        _context.Quinielas.Add(NewQuiniela(null));
        _context.LiguillaBrackets.Add(new LiguillaBracket { QuinielaId = QuinielaId, UserId = 1, Active = true });
        await _context.SaveChangesAsync();

        // Act
        var current = await _repository.AnyBracketsForSeasonAsync(SeasonId);
        var past = await _repository.AnyBracketsForSeasonAsync(SeasonId + 1);

        // Assert
        current.Should().BeTrue();
        past.Should().BeFalse();
    }

    [Fact]
    public async Task AnyBracketsForSeasonAsync_NoBrackets_ReturnsFalse()
    {
        // Arrange
        _context.Quinielas.Add(NewQuiniela(SeasonId));
        await _context.SaveChangesAsync();

        // Act
        var any = await _repository.AnyBracketsForSeasonAsync(SeasonId);

        // Assert
        any.Should().BeFalse();
    }

    [Fact]
    public async Task SeedAsync_SeasonWithResolvedEspnNodes_ThrowsAndLeavesThemUntouched()
    {
        // Arrange
        AddTeams(16);
        await _repository.AddNodeAsync(new LiguillaNode
        {
            SeasonId = SeasonId, Round = "R16", NodeNumber = 1, TeamAId = FirstTeamId + 3, TeamBId = FirstTeamId + 4,
            EspnEventGroupKey = "round-of-16:3-4", WinnerTeamId = FirstTeamId + 3, IsResolved = true, Active = true
        });
        await _context.SaveChangesAsync();
        var service = BuildService(KnockoutFormatCodes.UefaTwoLeg, new UefaTwoLegStrategy(), UefaTies());

        // Act
        var act = () => service.SeedAsync(QuinielaId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage(RealEspnResolvedMessage);
        var nodes = await _repository.GetNodesBySeasonAsync(SeasonId);
        nodes.Should().ContainSingle();
        nodes[0].IsResolved.Should().BeTrue();
    }

    [Fact]
    public async Task SeedAsync_NflRestingTopSeed_SeedsTeamAWithoutTeamBAndCoherentConference()
    {
        // Arrange
        AddTeams(14);
        var ties = new List<SimulatedTie>
        {
            new(1, "WC", "afc", "2", 2, "7", 7),
            new(4, "WC", "nfc", "9", 2, "14", 7),
            new(7, "DIV", "afc", "1", 1, null, null),
            new(9, "DIV", "nfc", "8", 1, null, null)
        };
        var service = BuildService(KnockoutFormatCodes.NflReseededSingleGame, new NflReseededStrategy(), ties);

        // Act
        var count = await service.SeedAsync(QuinielaId);

        // Assert
        count.Should().Be(NflNodeCount);
        var nodes = await _repository.GetNodesBySeasonAsync(SeasonId);
        var afcBye = nodes.Single(n => n.NodeNumber == 7);
        afcBye.Round.Should().Be("DIV");
        afcBye.Conference.Should().Be("afc");
        afcBye.TeamAId.Should().Be(FirstTeamId + 1);
        afcBye.SeedA.Should().Be(1);
        afcBye.TeamBId.Should().BeNull();
        nodes.Single(n => n.NodeNumber == 9).Conference.Should().Be("nfc");
        nodes.Single(n => n.NodeNumber == 4).Conference.Should().Be("nfc");
        nodes.Single(n => n.NodeNumber == 13).Conference.Should().BeNull();
    }

    [Fact]
    public async Task SeedAsync_TieWithTeamMissingInLeague_SkipsOnlyThatTie()
    {
        // Arrange
        AddTeams(15);
        var service = BuildService(KnockoutFormatCodes.UefaTwoLeg, new UefaTwoLegStrategy(), UefaTies());

        // Act
        await service.SeedAsync(QuinielaId);

        // Assert
        var nodes = await _repository.GetNodesBySeasonAsync(SeasonId);
        nodes.Single(n => n.NodeNumber == 1).TeamAId.Should().BeNull();
        nodes.Single(n => n.NodeNumber == 2).TeamAId.Should().Be(FirstTeamId + 2);
    }

    [Fact]
    public async Task SeedAsync_LeagueWithoutKnockoutFormat_Throws()
    {
        // Arrange
        AddTeams(16);
        var service = BuildService(KnockoutFormatCodes.UefaTwoLeg, new UefaTwoLegStrategy(), UefaTies());
        _league.KnockoutFormatCode = null;

        // Act
        var act = () => service.SeedAsync(QuinielaId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*formato de eliminatoria configurado*");
    }

    [Fact]
    public async Task SeedAsync_BuilderWithoutTies_ThrowsAndCreatesNothing()
    {
        // Arrange
        AddTeams(16);
        var service = BuildService(KnockoutFormatCodes.UefaTwoLeg, new UefaTwoLegStrategy(), Array.Empty<SimulatedTie>());

        // Act
        var act = () => service.SeedAsync(QuinielaId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*equipos suficientes*");
        (await _repository.GetNodesBySeasonAsync(SeasonId)).Should().BeEmpty();
    }

    [Fact]
    public async Task SeedAsync_EspnFails_ThrowsAndCreatesNothing()
    {
        // Arrange
        AddTeams(16);
        var service = BuildService(KnockoutFormatCodes.UefaTwoLeg, new UefaTwoLegStrategy(), UefaTies(), HttpStatusCode.InternalServerError);

        // Act
        var act = () => service.SeedAsync(QuinielaId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*no devolvió la tabla de posiciones*");
        (await _repository.GetNodesBySeasonAsync(SeasonId)).Should().BeEmpty();
    }

    private sealed class FakeBuilder : IKnockoutSimulationBuilder
    {
        private readonly IReadOnlyList<SimulatedTie> _ties;

        public FakeBuilder(string formatCode, IReadOnlyList<SimulatedTie> ties)
        {
            FormatCode = formatCode;
            _ties = ties;
        }

        public string FormatCode { get; }

        public string StandingsPath(string espnApiPath) => "apis/v2/sports/test/standings";

        public IReadOnlyList<SimulatedTie> Build(string standingsJson) => _ties;
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;

        public StubHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(_status) { Content = new StringContent(_body) });
    }
}
