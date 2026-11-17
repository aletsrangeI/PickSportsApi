using Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;
using Persistence.Interceptors;
using Persistence.Repositories;
using Xunit;

namespace PickSportsApi.UnitTests.UseCasesTests.Knockout;

public class LiguillaRepositoryPicksTests : IDisposable
{
    private const int BracketId = 10;
    private const int OtherBracketId = 11;

    private readonly ApplicationDbContext _context;
    private readonly LiguillaRepository _repository;

    public LiguillaRepositoryPicksTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options, new AuditableEntitySaveChangesInterceptor());
        _repository = new LiguillaRepository(_context);
    }

    private static LiguillaBracketPick Pick(int bracketId, string round, int slot, int teamId) =>
        new() { BracketId = bracketId, RoundCode = round, Slot = slot, TeamId = teamId };

    [Fact]
    public async Task GetPicksByBracketAsync_PicksDesordenados_DevuelveOrdenadosPorRondaYSlot()
    {
        // Arrange
        _context.LiguillaBracketPicks.AddRange(
            Pick(BracketId, "SF", 2, 7),
            Pick(BracketId, "QF", 3, 3),
            Pick(BracketId, "SF", 1, 6),
            Pick(BracketId, "QF", 1, 1));
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetPicksByBracketAsync(BracketId);

        // Assert
        result.Select(p => (p.RoundCode, p.Slot)).Should()
            .Equal(("QF", 1), ("QF", 3), ("SF", 1), ("SF", 2));
    }

    [Fact]
    public async Task GetPicksByBracketAsync_ExistenPicksDeOtroBracket_SoloDevuelveLosDelBracketPedido()
    {
        // Arrange
        _context.LiguillaBracketPicks.AddRange(
            Pick(BracketId, "QF", 1, 1),
            Pick(OtherBracketId, "QF", 1, 2));
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetPicksByBracketAsync(BracketId);

        // Assert
        result.Should().ContainSingle().Which.TeamId.Should().Be(1);
    }

    [Fact]
    public async Task ReplacePicksAsync_BracketConPicksPrevios_BorraLosAnterioresYGuardaLosNuevos()
    {
        // Arrange
        _context.LiguillaBracketPicks.AddRange(
            Pick(BracketId, "QF", 1, 1),
            Pick(BracketId, "QF", 2, 2),
            Pick(OtherBracketId, "QF", 1, 9));
        await _context.SaveChangesAsync();
        var nuevos = new List<LiguillaBracketPick> { Pick(BracketId, "QF", 1, 5), Pick(BracketId, "SF", 1, 5) };

        // Act
        await _repository.ReplacePicksAsync(BracketId, nuevos);
        await _context.SaveChangesAsync();

        // Assert
        var propios = await _repository.GetPicksByBracketAsync(BracketId);
        propios.Select(p => (p.RoundCode, p.Slot, p.TeamId)).Should().Equal(("QF", 1, 5), ("SF", 1, 5));
        var ajenos = await _repository.GetPicksByBracketAsync(OtherBracketId);
        ajenos.Should().ContainSingle().Which.TeamId.Should().Be(9);
    }

    [Fact]
    public async Task ReplacePicksAsync_SinSaveChanges_NoPersisteNada()
    {
        // Arrange
        _context.LiguillaBracketPicks.Add(Pick(BracketId, "QF", 1, 1));
        await _context.SaveChangesAsync();

        // Act
        await _repository.ReplacePicksAsync(BracketId, new List<LiguillaBracketPick> { Pick(BracketId, "QF", 1, 8) });

        // Assert
        _context.ChangeTracker.HasChanges().Should().BeTrue();
        var persistidos = await _context.LiguillaBracketPicks.AsNoTracking().ToListAsync();
        persistidos.Should().ContainSingle().Which.TeamId.Should().Be(1);
    }

    [Fact]
    public async Task ReplacePicksAsync_ListaVacia_BorraTodosLosPicksDelBracket()
    {
        // Arrange
        _context.LiguillaBracketPicks.AddRange(Pick(BracketId, "QF", 1, 1), Pick(BracketId, "QF", 2, 2));
        await _context.SaveChangesAsync();

        // Act
        await _repository.ReplacePicksAsync(BracketId, new List<LiguillaBracketPick>());
        await _context.SaveChangesAsync();

        // Assert
        (await _repository.GetPicksByBracketAsync(BracketId)).Should().BeEmpty();
    }

    public void Dispose() => _context.Dispose();
}
