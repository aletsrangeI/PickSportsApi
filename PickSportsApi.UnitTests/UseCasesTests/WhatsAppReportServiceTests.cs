using Domain.Entities;
using Interface.Persistence;
using Moq;
using UseCases.Reports;
using UseCases.Scoring;
using Xunit;
using MatchEntity = Domain.Entities.Match;

namespace PickSportsApi.UnitTests.UseCasesTests;

public class WhatsAppReportServiceTests
{
    private readonly Mock<IUnitOfWork> _mockUow = new();
    private readonly ScoringEngine _engine = new();
    private readonly WhatsAppReportService _service;

    public WhatsAppReportServiceTests()
    {
        _service = new WhatsAppReportService(_mockUow.Object, _engine);
    }

    [Fact]
    public async Task GenerateReminderReport_FormateaMensajeConEmojisYFaltantes()
    {
        // GIVEN: Quiniela con jornada y miembros que deben picks
        var quiniela = new Quiniela { Id = 1, Name = "Quiniela Amigos" };
        var week = new Week { Id = 10, WeekNumber = 4 };
        var member1 = new QuinielaMember { Id = 101, Alias = "Alex" };
        var member2 = new QuinielaMember { Id = 102, Alias = "Fer" };
        var match = new MatchEntity { Id = 201, WeekId = 10, DateUtc = DateTime.UtcNow.AddHours(2) };

        _mockUow.Setup(u => u.Quinielas.GetAsync(1)).ReturnsAsync(quiniela);
        _mockUow.Setup(u => u.Weeks.GetAsync(10)).ReturnsAsync(week);
        _mockUow.Setup(u => u.Matches.GetByWeekIdAsync(10)).ReturnsAsync((IEnumerable<MatchEntity>)new List<MatchEntity> { match });
        _mockUow.Setup(u => u.QuinielaMembers.GetMembersAsync(1)).ReturnsAsync((IEnumerable<QuinielaMember>)new List<QuinielaMember> { member1, member2 });
        _mockUow.Setup(u => u.Picks.GetAllPicksForWeekAsync(1, 10)).ReturnsAsync((IEnumerable<Pick>)new List<Pick>()); // Nadie ha llenado

        // WHEN: Se genera el recordatorio
        var result = await _service.GenerateReminderReportAsync(1, 10, "https://quiniela.test");

        // THEN: Contiene alerta, emojis y mención a los miembros faltantes
        Assert.True(result.isSuccess);
        Assert.Contains("🚨 *¡ALERTA QUINIELA - JORNADA 4!* 🚨", result.Data!.Content);
        Assert.Contains("❌ Alex", result.Data.Content);
        Assert.Contains("❌ Fer", result.Data.Content);
        Assert.Contains("👉 *Link para llenar:*", result.Data.Content);
    }

    [Fact]
    public async Task GeneratePrizePoolReport_CalculaPorcentajes70_20_10()
    {
        // GIVEN: Quiniela con 10 miembros y cuota de $100
        var quiniela = new Quiniela
        {
            Id = 1,
            EntryFee = 100,
            FirstPlacePct = 70,
            SecondPlacePct = 20,
            ThirdPlacePct = 10
        };
        var members = Enumerable.Range(1, 10).Select(i => new QuinielaMember { Id = i, Alias = $"User{i}" }).ToList();

        _mockUow.Setup(u => u.Quinielas.GetAsync(1)).ReturnsAsync(quiniela);
        _mockUow.Setup(u => u.QuinielaMembers.GetMembersAsync(1)).ReturnsAsync((IEnumerable<QuinielaMember>)members);

        // WHEN
        var result = await _service.GeneratePrizePoolReportAsync(1);

        // THEN: Bolsa = 1000, 1° = 700, 2° = 200, 3° = 100
        Assert.True(result.isSuccess);
        Assert.Contains("BOLSA TOTAL: $1,000 MXN", result.Data!.Content);
        Assert.Contains("1er Lugar (70%): *$700 MXN*", result.Data.Content);
        Assert.Contains("2do Lugar (20%): *$200 MXN*", result.Data.Content);
        Assert.Contains("3er Lugar (10%): *$100 MXN*", result.Data.Content);
    }
}
