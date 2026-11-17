using Domain.Entities;
using FluentAssertions;
using Interface.UseCases.Knockout;
using UseCases.Liguilla;
using UseCases.Liguilla.Strategies;
using Xunit;

namespace PickSportsApi.UnitTests.UseCasesTests.Knockout;

public class KnockoutStrategyResolverTests
{
    private readonly KnockoutStrategyResolver _sut = new(new IKnockoutFormatStrategy[] { new FmfLiguillaStrategy() });

    [Fact]
    public void For_LeagueWithKnownCode_ReturnsStrategy()
    {
        // Arrange
        var league = new League { KnockoutFormatCode = "FMF_LIGUILLA" };

        // Act
        var result = _sut.For(league);

        // Assert
        result.Should().BeOfType<FmfLiguillaStrategy>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("NO_EXISTE")]
    public void For_NullOrUnknownCode_ReturnsNullWithoutThrowing(string? code)
    {
        // Arrange
        var league = new League { KnockoutFormatCode = code };

        // Act
        var act = () => _sut.For(league);

        // Assert
        act.Should().NotThrow();
        _sut.For(league).Should().BeNull();
    }

    [Fact]
    public void ByCode_IsCaseInsensitive()
    {
        // Act
        var result = _sut.ByCode("fmf_liguilla");

        // Assert
        result.Should().NotBeNull();
    }
}
