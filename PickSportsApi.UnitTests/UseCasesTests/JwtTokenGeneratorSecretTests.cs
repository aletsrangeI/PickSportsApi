using Domain.Entities;
using Microsoft.Extensions.Configuration;
using UseCases.Security;
using Xunit;

namespace PickSportsApi.UnitTests.UseCasesTests;

public class JwtTokenGeneratorSecretTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GenerateToken_WithoutSecret_ThrowsInsteadOfSigningWithAKnownDefault(string? secret)
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { { "Config:Secret", secret } })
            .Build();
        var generator = new JwtTokenGenerator(config);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => generator.GenerateToken(new User()));
        Assert.Contains("Config:Secret", exception.Message);
    }
}
