using Common;
using DTO.Bulletin;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using UseCases.Bulletin;
using Xunit;

namespace PickSportsApi.UnitTests.UseCasesTests;

public class GeminiBulletinNarrativeServiceTests
{
    private readonly Mock<IConfiguration> _mockConfig = new();
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory = new();
    private readonly Mock<IAppLogger<GeminiBulletinNarrativeService>> _mockLogger = new();

    [Fact]
    public async Task GenerateChronicleAsync_WhenApiKeyMissing_ShouldReturnProceduralFallback()
    {
        // Arrange
        _mockConfig.Setup(c => c["Gemini:ApiKey"]).Returns(string.Empty);
        _mockConfig.Setup(c => c["Gemini:Model"]).Returns("gemini-3.8-flash");

        var sut = new GeminiBulletinNarrativeService(
            _mockConfig.Object,
            _mockHttpClientFactory.Object,
            _mockLogger.Object);

        var ctx = new BulletinNarrativeContextDto
        {
            WeekNumber = 8,
            SeasonName = "Apertura 2026",
            CommunityAccuracyPct = 42.5m,
            Podium = new List<PodiumMemberDto>
            {
                new() { Position = 1, Alias = "Dani", Hits = 7, AccuracyPct = 77.8m }
            },
            Derbies = new List<LeagueDerbyDto>
            {
                new() { DerbyName = "El Clásico Nacional", ScoreLabel = "América 2 - 1 Chivas", WinnerAbbr = "AME" }
            }
        };

        // Act
        var result = await sut.GenerateChronicleAsync(ctx);

        // Assert
        result.Should().NotBeNull();
        result.GeneratedByAi.Should().BeFalse();
        result.Headline.Should().Contain("Dani");
        result.Headline.Should().Contain("El Clásico Nacional");
        result.Chronicle.Should().Contain("Jornada 8");
        result.Chronicle.Should().Contain("42.5%");
        result.Chronicle.Should().Contain("El Clásico Nacional");
    }

    [Fact]
    public void GenerateProceduralChronicle_WhenFavoriteLost_ShouldHighlightBetrayalInHeadline()
    {
        // Arrange
        var ctx = new BulletinNarrativeContextDto
        {
            WeekNumber = 9,
            SeasonName = "Apertura 2026",
            CommunityAccuracyPct = 35.0m,
            Podium = new List<PodiumMemberDto>
            {
                new() { Position = 1, Alias = "Fer", Hits = 6 }
            },
            Favorite = new BulletinFavoriteDto
            {
                TeamName = "Cruz Azul",
                PickPct = 85m,
                Won = false
            }
        };

        // Act
        var result = GeminiBulletinNarrativeService.GenerateProceduralChronicle(ctx, "TestModel");

        // Assert
        result.Headline.Should().Contain("Cruz Azul");
        result.Headline.Should().Contain("Fer");
        result.Chronicle.Should().Contain("Cruz Azul");
        result.Chronicle.Should().Contain("85%");
    }
}
