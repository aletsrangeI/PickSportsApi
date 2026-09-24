using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Common.Security;
using Domain.Entities;
using DTO.Migration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Persistence.Context;
using Persistence.Initialization;
using Persistence.Interceptors;
using Persistence.Repositories;
using UseCases.Migration;
using UseCases.Scoring;
using Xunit;

namespace PickSportsApi.UnitTests.UseCasesTests;

public class QuinielaMigrationServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly UnitOfWork _unitOfWork;
    private readonly ClosedXmlParserService _parserService;
    private readonly ScoringEngine _scoringEngine;
    private readonly PasswordHasher _passwordHasher;
    private readonly QuinielaMigrationService _migrationService;
    private readonly string _excelPath = @"c:\OrionSys\Quiniela\Quiniela Clausura Liga MX 2026.xlsx";

    public QuinielaMigrationServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var interceptor = new AuditableEntitySaveChangesInterceptor();
        _context = new ApplicationDbContext(options, interceptor);
        _unitOfWork = new UnitOfWork(_context);
        _parserService = new ClosedXmlParserService();
        _scoringEngine = new ScoringEngine();
        _passwordHasher = new PasswordHasher();

        _migrationService = new QuinielaMigrationService(
            _unitOfWork,
            _parserService,
            _scoringEngine,
            _passwordHasher,
            new NullLogger<QuinielaMigrationService>());
    }

    [Fact]
    [Trait("Category", "Migration")]
    public async Task PreviewMigrationAsync_ReturnsAccurateMetadataWithoutSideEffects()
    {
        // Arrange
        using var stream = File.OpenRead(_excelPath);

        // Act
        var result = await _migrationService.PreviewMigrationAsync(stream);

        // Assert
        Assert.True(result.isSuccess);
        Assert.NotNull(result.Data);
        var preview = result.Data;

        Assert.Equal(9, preview.TotalParticipants);
        Assert.Equal(90, preview.TotalMatches);
        Assert.Equal(756, preview.TotalPicks);
        Assert.Equal(10, preview.CurrentWeekNumber);
        Assert.Equal(9, preview.Participants.Count);
        Assert.Contains(preview.Participants, p => p.Alias == "Alex" && p.ExpectedHits == 31);
        Assert.Contains(preview.Participants, p => p.Alias == "Alejandra" && p.ExpectedHits == 35);
        Assert.Contains(preview.Participants, p => p.Alias == "César" && p.ExpectedHits == 28);

        // Ensure no database writes
        Assert.Empty(_context.Quinielas);
        Assert.Empty(_context.Picks);
    }

    [Fact]
    [Trait("Category", "Migration")]
    public async Task ExecuteMigrationAsync_MigratesCompleteQuinielaWithIntegrityOracle()
    {
        // Arrange - Seed admin user
        var adminUser = new User
        {
            Username = "admin",
            Email = "admin@picksports.local",
            DisplayName = "Alex Administrador",
            Role = "ADMIN",
            PasswordHash = _passwordHasher.Hash("Secret123!"),
            Active = true
        };
        await _context.Users.AddAsync(adminUser);
        await _context.SaveChangesAsync();

        using var stream = File.OpenRead(_excelPath);
        var request = new MigrationExecuteRequestDto
        {
            QuinielaName = "Quiniela Amigos Clausura 2026",
            AdminPlayerAlias = "Alex",
            EntryFee = 200m
        };

        // Act
        var response = await _migrationService.ExecuteMigrationAsync(stream, request, adminUser.Id);

        // Assert
        Assert.True(response.isSuccess, response.Message);
        Assert.NotNull(response.Data);
        Assert.True(response.Data.ValidationPassed);
        Assert.Equal(9, response.Data.TotalMembersMigrated);
        Assert.Equal(90, response.Data.TotalMatchesMigrated);
        Assert.Equal(756, response.Data.TotalPicksMigrated);
        Assert.Equal(10, response.Data.CurrentWeekNumber);

        // 1. Check Quiniela
        var quiniela = await _context.Quinielas
            .Include(q => q.Members)
            .FirstOrDefaultAsync(q => q.Id == response.Data.QuinielaId);
        Assert.NotNull(quiniela);
        Assert.Equal("Quiniela Amigos Clausura 2026", quiniela.Name);
        Assert.Equal(9, quiniela.Members.Count);

        // 2. Check Admin membership
        var adminMember = quiniela.Members.FirstOrDefault(m => m.UserId == adminUser.Id);
        Assert.NotNull(adminMember);
        Assert.Equal("OWNER", adminMember.Role);
        Assert.Equal("Alex", adminMember.Alias);
        Assert.Equal(31, adminMember.TotalHits);

        // 3. Check Other Members
        var alejandra = quiniela.Members.FirstOrDefault(m => m.Alias == "Alejandra");
        Assert.NotNull(alejandra);
        Assert.Equal("MEMBER", alejandra.Role);
        Assert.Equal(35, alejandra.TotalHits);

        var cesar = quiniela.Members.FirstOrDefault(m => m.Alias == "César");
        Assert.NotNull(cesar);
        Assert.Equal("MEMBER", cesar.Role);
        Assert.Equal(28, cesar.TotalHits);

        // 4. Check Picks count
        var totalPicksInDb = await _context.Picks.CountAsync(p => p.QuinielaId == quiniela.Id);
        Assert.Equal(756, totalPicksInDb);

        // 5. Check Week 10 Picks (27 picks captured in advance)
        var week10 = await _context.Weeks.FirstOrDefaultAsync(w => w.WeekNumber == 10);
        Assert.NotNull(week10);
        Assert.Equal("PUBLISHED", week10.Status);

        var week10Matches = await _context.Matches.Where(m => m.WeekId == week10.Id).Select(m => m.Id).ToListAsync();
        var week10Picks = await _context.Picks.Where(p => week10Matches.Contains(p.MatchId)).ToListAsync();
        Assert.Equal(27, week10Picks.Count);

        // 6. Check Awards generated for Weeks 1 to 9
        var awardsCount = await _context.WeeklyAwards.CountAsync(a => a.QuinielaId == quiniela.Id);
        Assert.True(awardsCount > 0, "Awards should be calculated for finished weeks");
    }

    public void Dispose()
    {
        _context.Dispose();
        _unitOfWork.Dispose();
    }
}
