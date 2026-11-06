using Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Persistence.Context;
using Persistence.Interceptors;
using Persistence.Repositories;
using Xunit;

namespace PickSportsApi.UnitTests.PersistenceTests;

public class AchievementRepositoryTests : IDisposable
{
    /// <summary>Simula el rechazo del indice unico (UserId, Code, SeasonId) por una calificacion concurrente.</summary>
    private sealed class FailingSaveInterceptor : SaveChangesInterceptor
    {
        public bool Fail { get; set; } = true;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Fail) throw new DbUpdateException("indice unico (simulado)");
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private readonly FailingSaveInterceptor _failing = new();
    private readonly ApplicationDbContext _context;
    private readonly AchievementRepository _repository;

    public AchievementRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .AddInterceptors(_failing)
            .Options;
        _context = new ApplicationDbContext(options, new AuditableEntitySaveChangesInterceptor());
        _repository = new AchievementRepository(_context);
    }

    private static UserAchievement Sample() => new()
    {
        UserId = 7, Code = "PRIMERA_SANGRE", QuinielaId = 1, SeasonId = 3, UnlockedAt = DateTime.UtcNow, Active = true
    };

    [Fact]
    public async Task InsertAsync_CuandoElGuardadoFalla_RelanzaYDesvinculaLaEntidad()
    {
        // Arrange
        var entity = Sample();

        // Act
        var act = () => _repository.InsertAsync(entity);

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
        _context.Entry(entity).State.Should().Be(EntityState.Detached);
    }

    [Fact]
    public async Task InsertAsync_TrasUnFalloDeCarrera_ElSaveFinalNoReintentaElInsertFallido()
    {
        // Arrange: el insert falla y luego la base vuelve a aceptar escrituras (el Save() final de ScoreWeekAsync)
        var entity = Sample();
        await Assert.ThrowsAsync<DbUpdateException>(() => _repository.InsertAsync(entity));
        _failing.Fail = false;

        // Act
        var saved = await _context.SaveChangesAsync();

        // Assert: sin la desvinculacion, la entidad en estado Added se habria insertado (o relanzado el error)
        saved.Should().Be(0);
        (await _context.UserAchievements.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task InsertAsync_SinFallo_GuardaElTrofeo()
    {
        // Arrange
        _failing.Fail = false;

        // Act
        var ok = await _repository.InsertAsync(Sample());

        // Assert
        ok.Should().BeTrue();
        (await _repository.ExistsAsync(7, "PRIMERA_SANGRE", 3)).Should().BeTrue();
    }

    public void Dispose() => _context.Dispose();
}
