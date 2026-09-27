using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;
using Persistence.Interceptors;
using Persistence.Repositories;
using Xunit;

namespace PickSportsApi.UnitTests.PersistenceTests;

public class PushNotificationLogRepositoryTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly PushNotificationLogRepository _repository;

    public PushNotificationLogRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var interceptor = new AuditableEntitySaveChangesInterceptor();
        _context = new ApplicationDbContext(options, interceptor);
        _repository = new PushNotificationLogRepository(_context);
    }

    [Fact]
    public async Task HasMatchFinishedBeenSentAsync_WhenLogExists_ReturnsTrue()
    {
        // Arrange
        var matchId = 101;
        var quinielaId = 4;

        _context.PushNotificationLogs.Add(new PushNotificationLog
        {
            NotificationType = $"MATCH_FINISHED_{matchId}",
            WeekId = 10,
            QuinielaId = quinielaId,
            DateLocal = "2026-09-27",
            Title = "Resultado",
            Message = "Ganó AME",
            Active = true
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.HasMatchFinishedBeenSentAsync(matchId, quinielaId);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task HasMatchFinishedBeenSentAsync_WhenDifferentQuiniela_ReturnsFalse()
    {
        // Arrange
        var matchId = 101;

        _context.PushNotificationLogs.Add(new PushNotificationLog
        {
            NotificationType = $"MATCH_FINISHED_{matchId}",
            WeekId = 10,
            QuinielaId = 1,
            DateLocal = "2026-09-27",
            Title = "Resultado",
            Message = "Ganó AME",
            Active = true
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.HasMatchFinishedBeenSentAsync(matchId, 4);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task HasMatchFinishedBeenSentAsync_WhenNoLog_ReturnsFalse()
    {
        // Act
        var result = await _repository.HasMatchFinishedBeenSentAsync(999, 4);

        // Assert
        Assert.False(result);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
