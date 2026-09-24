using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Persistence.Context;
using Persistence.Initialization;
using Persistence.Interceptors;
using Persistence.Repositories;
using Xunit;

namespace PickSportsApi.UnitTests.PersistenceTests;

public class ModelIntegrityTests : IDisposable
{
    private readonly ApplicationDbContext _context;

    public ModelIntegrityTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var interceptor = new AuditableEntitySaveChangesInterceptor();
        _context = new ApplicationDbContext(options, interceptor);
    }

    [Fact]
    public async Task DatabaseInitializer_SeedsSportsAndLeaguesCorrectly()
    {
        // Arrange
        var initializer = new DatabaseInitializer(_context, new NullLogger<DatabaseInitializer>());

        // Act
        await initializer.SeedAsync();

        // Assert
        var sports = await _context.Sports.ToListAsync();
        Assert.Equal(2, sports.Count);
        Assert.Contains(sports, s => s.Code == "SOCCER" && s.HasDraw);
        Assert.Contains(sports, s => s.Code == "NFL" && !s.HasDraw);

        var leagues = await _context.Leagues.ToListAsync();
        Assert.Equal(4, leagues.Count);
        Assert.Contains(leagues, l => l.Code == "mex.1");
        Assert.Contains(leagues, l => l.Code == "nfl");

        var admin = await _context.Users.FirstOrDefaultAsync(u => u.Email == "admin@picksports.local");
        Assert.NotNull(admin);
        Assert.Equal("ADMIN", admin.Role);
    }

    [Fact]
    public async Task PickRepository_UpsertPick_UpdatesExistingOrInsertsNew()
    {
        // Arrange
        var repo = new PickRepository(_context);
        var initialPick = new Pick
        {
            QuinielaId = 1,
            MemberId = 1,
            MatchId = 10,
            PickAbbr = "AME"
        };

        // Act - Insert
        var inserted = await repo.UpsertPickAsync(initialPick);

        // Assert - Inserted
        Assert.True(inserted);
        var stored = await repo.GetPickAsync(1, 1, 10);
        Assert.NotNull(stored);
        Assert.Equal("AME", stored.PickAbbr);

        // Act - Update (Usuario cambia de opinión antes del bloqueo)
        var updatedPick = new Pick
        {
            QuinielaId = 1,
            MemberId = 1,
            MatchId = 10,
            PickAbbr = "EMPATE"
        };
        var updated = await repo.UpsertPickAsync(updatedPick);

        // Assert - Updated
        Assert.True(updated);
        var storedUpdated = await repo.GetPickAsync(1, 1, 10);
        Assert.NotNull(storedUpdated);
        Assert.Equal("EMPATE", storedUpdated.PickAbbr);

        // Solo debe haber 1 pick para esa tupla
        var count = await _context.Picks.CountAsync(p => p.QuinielaId == 1 && p.MemberId == 1 && p.MatchId == 10);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task PickRepository_InsertMissingAutoFilledPicks_NeverOverwritesHumanPicks()
    {
        // Arrange
        var repo = new PickRepository(_context);

        // Pick humano existente para MemberId 1
        await repo.UpsertPickAsync(new Pick
        {
            QuinielaId = 1,
            MemberId = 1,
            MatchId = 10,
            PickAbbr = "AME",
            IsAutoFilled = false
        });

        // Intentar autollenar MemberId 1 y MemberId 2
        var autoPicks = new List<Pick>
        {
            new Pick { QuinielaId = 1, MemberId = 1, MatchId = 10, PickAbbr = "CHI" }, // Debería ser IGNORADO
            new Pick { QuinielaId = 1, MemberId = 2, MatchId = 10, PickAbbr = "EMPATE" } // Debería ser INSERTADO
        };

        // Act
        var insertedCount = await repo.InsertMissingAutoFilledPicksAsync(autoPicks);

        // Assert
        Assert.Equal(1, insertedCount);

        // El pick humano debe conservarse como AME
        var humanPick = await repo.GetPickAsync(1, 1, 10);
        Assert.NotNull(humanPick);
        Assert.Equal("AME", humanPick.PickAbbr);
        Assert.False(humanPick.IsAutoFilled);

        // El pick faltante debe haberse insertado como EMPATE con IsAutoFilled = true
        var botPick = await repo.GetPickAsync(1, 2, 10);
        Assert.NotNull(botPick);
        Assert.Equal("EMPATE", botPick.PickAbbr);
        Assert.True(botPick.IsAutoFilled);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
