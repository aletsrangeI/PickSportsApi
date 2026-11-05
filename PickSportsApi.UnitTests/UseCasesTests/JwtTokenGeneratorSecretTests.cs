using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
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

    [Fact]
    public void GenerateToken_WithSecret_SignsWithThatSecretAndNoOtherKeyValidates()
    {
        // Arrange
        const string secret = "test-only-secret-0123456789abcdef0123456789abcdef0123456789abcdef";
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Config:Secret", secret },
                { "Config:Issuer", "TestIssuer" },
                { "Config:Audience", "TestAudience" }
            })
            .Build();
        var user = new User { Id = 7, Username = "u", DisplayName = "U", Email = "u@example.com", Role = "ADMIN" };

        // Act
        var token = new JwtTokenGenerator(config).GenerateToken(user);

        // Assert: valida con el secreto configurado...
        TokenValidationParameters ParametersFor(string key) => new()
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(key)),
            ValidIssuer = "TestIssuer",
            ValidAudience = "TestAudience",
            ValidateLifetime = true
        };

        var handler = new JwtSecurityTokenHandler();
        handler.ValidateToken(token, ParametersFor(secret), out _);

        // ...y con ninguna otra llave (p. ej. un valor conocido por defecto)
        Assert.ThrowsAny<SecurityTokenException>(() => handler.ValidateToken(
            token,
            ParametersFor("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"),
            out _));
    }
}
