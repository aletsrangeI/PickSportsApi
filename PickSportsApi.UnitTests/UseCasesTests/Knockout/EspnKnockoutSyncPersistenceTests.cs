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
using UseCases.Liguilla.Espn;
using UseCases.Liguilla.Strategies;
using Xunit;

namespace PickSportsApi.UnitTests.UseCasesTests.Knockout;

/// <summary>Integra el sync con el repositorio REAL sobre EF InMemory: lo que se siembra debe poder leerse despues.</summary>
public class EspnKnockoutSyncPersistenceTests : IDisposable
{
    private const int LeagueId = 7;
    private const int SeasonId = 70;
    private const int NhlNodeCount = 15;

    private readonly ApplicationDbContext _context;
    private readonly LiguillaRepository _repository;
    private readonly EspnKnockoutSyncService _service;

    public EspnKnockoutSyncPersistenceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options, new AuditableEntitySaveChangesInterceptor());
        _repository = new LiguillaRepository(_context);

        var league = new League { Id = LeagueId, Code = "nhl", KnockoutFormatCode = KnockoutFormatCodes.NhlBestOf7 };
        var teams = new List<Team>
        {
            NewTeam(1, "15", "Bruins"),
            NewTeam(2, "16", "Sabres")
        };

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.Leagues.GetAsync(LeagueId)).ReturnsAsync(league);
        uow.Setup(u => u.Seasons.GetCurrentSeasonAsync(LeagueId))
            .ReturnsAsync(new Season { Id = SeasonId, LeagueId = LeagueId, Year = 2026, IsCurrent = true });
        uow.Setup(u => u.Teams.GetByLeagueIdAsync(LeagueId)).ReturnsAsync(teams);
        uow.Setup(u => u.Liguilla).Returns(_repository);
        uow.Setup(u => u.Save(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken ct) => _context.SaveChangesAsync(ct));

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("EspnClient"))
            .Returns(() => new HttpClient(new StubHandler(Fixture("nhl-playoffs-20260420"))));

        var resolver = new KnockoutStrategyResolver(new IKnockoutFormatStrategy[] { new NhlBestOfSevenStrategy() });
        _service = new EspnKnockoutSyncService(
            uow.Object, resolver, new EspnKnockoutParser(), factory.Object, NullLogger<EspnKnockoutSyncService>.Instance);
    }

    public void Dispose() => _context.Dispose();

    private static Team NewTeam(int id, string espnId, string name) => new()
    {
        Id = id,
        EspnTeamId = espnId,
        LeagueId = LeagueId,
        Name = name,
        DisplayName = name,
        Abbreviation = name[..3].ToUpperInvariant()
    };

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Espn", name + ".json"));

    [Fact]
    public async Task SyncLeagueAsync_FirstSync_SeededNodesAreReadableThroughTheRealRepository()
    {
        // Act
        await _service.SyncLeagueAsync(LeagueId);

        // Assert
        var nodes = await _repository.GetNodesBySeasonAsync(SeasonId);
        nodes.Should().HaveCount(NhlNodeCount);
        nodes.Should().OnlyContain(n => n.Active);
    }

    [Fact]
    public async Task SyncLeagueAsync_SecondSync_DoesNotThrowNorDuplicateNodes()
    {
        // Arrange
        await _service.SyncLeagueAsync(LeagueId);

        // Act
        var act = () => _service.SyncLeagueAsync(LeagueId);

        // Assert
        await act.Should().NotThrowAsync();
        var nodes = await _repository.GetNodesBySeasonAsync(SeasonId);
        nodes.Should().HaveCount(NhlNodeCount);
        nodes.Select(n => n.NodeNumber).Should().OnlyHaveUniqueItems();
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _body;

        public StubHandler(string body) => _body = body;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(_body) });
    }
}
