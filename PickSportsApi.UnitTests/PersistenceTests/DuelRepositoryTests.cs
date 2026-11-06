using Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;
using Persistence.Interceptors;
using Persistence.Repositories;
using Xunit;

namespace PickSportsApi.UnitTests.PersistenceTests;

public class DuelRepositoryTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly DuelRepository _repository;

    public DuelRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options, new AuditableEntitySaveChangesInterceptor());
        _repository = new DuelRepository(_context);

        _context.Users.AddRange(
            new User { Id = 7, Username = "a", Email = "a@x", PasswordHash = "x", DisplayName = "Alex", Active = true },
            new User { Id = 8, Username = "b", Email = "b@x", PasswordHash = "x", DisplayName = "Omar", Active = true });
        _context.QuinielaMembers.AddRange(
            new QuinielaMember { Id = 1, QuinielaId = 4, UserId = 7, Alias = "Alex", Active = true },
            new QuinielaMember { Id = 2, QuinielaId = 4, UserId = 8, Alias = "Omar", Active = true });
        _context.SaveChanges();
    }

    /// <summary>Como los genera RoundRobinScheduler: sin Active (BaseEntity.Active vale false por defecto).</summary>
    private static List<DuelFixture> SchedulerLikeFixtures() => new()
    {
        new DuelFixture { QuinielaId = 4, WeekNumber = 1, Round = 1, Leg = 1, HomeMemberId = 1, AwayMemberId = 2 },
        new DuelFixture { QuinielaId = 4, WeekNumber = 3, Round = 2, Leg = 1, HomeMemberId = 2, AwayMemberId = 1 },
    };

    [Fact]
    public async Task InsertFixturesAsync_FixturesDelScheduler_QuedanActivosYVisiblesEnLasLecturas()
    {
        // Arrange
        var fixtures = SchedulerLikeFixtures();
        fixtures.Should().OnlyContain(f => f.Active == false); // precondicion: asi salen del scheduler

        // Act
        await _repository.InsertFixturesAsync(fixtures);

        // Assert
        (await _repository.GetFixturesAsync(4)).Should().HaveCount(2);
        (await _repository.GetFixturesByWeekNumberAsync(4, 3)).Should().HaveCount(1);
        (await _repository.GetFixtureAsync(fixtures[0].Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task AnyResolvedAsync_ConUnDueloResuelto_EsTrue_YElByeNoCuenta()
    {
        // Arrange
        var fixtures = SchedulerLikeFixtures();
        fixtures.Add(new DuelFixture { QuinielaId = 4, WeekNumber = 5, Round = 3, Leg = 1, HomeMemberId = 1, AwayMemberId = null, Result = "BYE" });
        await _repository.InsertFixturesAsync(fixtures);

        // Act + Assert: solo hay un Bye "resuelto" -> no cuenta
        (await _repository.AnyResolvedAsync(4)).Should().BeFalse();

        var first = (await _repository.GetFixturesByWeekNumberAsync(4, 1)).Single();
        first.Result = "HOME";
        await _repository.UpdateFixtureAsync(first);
        (await _repository.AnyResolvedAsync(4)).Should().BeTrue();
    }

    [Fact]
    public async Task UpsertSeasonInfoAsync_NuevaFila_QuedaActivaYSeLeeConGetSeasonInfo()
    {
        // Act
        await _repository.UpsertSeasonInfoAsync(new QuinielaDuelSeason { QuinielaId = 4, Seed = 99, GeneratedAt = DateTime.UtcNow });

        // Assert
        var info = await _repository.GetSeasonInfoAsync(4);
        info.Should().NotBeNull();
        info!.Seed.Should().Be(99);
    }

    [Fact]
    public async Task DeleteFixturesAsync_BorraSoloLosDeLaQuiniela()
    {
        // Arrange
        await _repository.InsertFixturesAsync(SchedulerLikeFixtures());
        await _repository.InsertFixturesAsync(new[]
        {
            new DuelFixture { QuinielaId = 9, WeekNumber = 1, Round = 1, Leg = 1, HomeMemberId = 1, AwayMemberId = 2 }
        });

        // Act
        await _repository.DeleteFixturesAsync(4);

        // Assert
        (await _repository.GetFixturesAsync(4)).Should().BeEmpty();
        (await _repository.GetFixturesAsync(9)).Should().HaveCount(1);
    }

    public void Dispose() => _context.Dispose();
}
