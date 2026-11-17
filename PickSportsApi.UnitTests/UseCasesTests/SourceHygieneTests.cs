using System.Text;
using System.Text.RegularExpressions;
using Domain.Common;
using FluentAssertions;
using UseCases.Liguilla.Strategies;
using Xunit;

namespace PickSportsApi.UnitTests.UseCasesTests;

/// <summary>
/// Barreras contra errores que se repitieron al generar codigo con distintos modelos y que el compilador no ve.
/// </summary>
public class SourceHygieneTests
{
    // Texto UTF-8 leido como Latin-1/Windows-1252 y vuelto a guardar: "Futbol" queda como "FÃºtbol".
    private static readonly Regex DoubleEncodedUtf8 = new("[ÃÂ][\u0080-¿]|â€|�", RegexOptions.Compiled);

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "PickSportsApi.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("No se encontro PickSportsApi.sln subiendo desde el directorio de pruebas.");
    }

    [Fact]
    public void SourceFiles_DoNotContainDoubleEncodedUtf8()
    {
        // Arrange
        var root = FindRepoRoot();
        var separator = Path.DirectorySeparatorChar;
        var files = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{separator}obj{separator}") && !f.Contains($"{separator}bin{separator}"))
            .Where(f => !f.EndsWith(nameof(SourceHygieneTests) + ".cs", StringComparison.Ordinal))
            .ToList();

        // Act
        var offenders = files
            .Where(f => DoubleEncodedUtf8.IsMatch(File.ReadAllText(f, Encoding.UTF8)))
            .Select(f => Path.GetRelativePath(root, f))
            .ToList();

        // Assert
        files.Should().NotBeEmpty();
        offenders.Should().BeEmpty("un archivo con texto doble codificado (p. ej. 'FÃºtbol') corrompe los datos sembrados; reescribelo con Edit/Write y no con Set-Content");
    }

    [Fact]
    public void EveryStrategy_ExposesItsCodeFromKnockoutFormatCodes()
    {
        // Arrange
        var known = new[]
        {
            KnockoutFormatCodes.FmfLiguilla,
            KnockoutFormatCodes.NhlBestOf7,
            KnockoutFormatCodes.NflReseededSingleGame,
            KnockoutFormatCodes.UefaTwoLeg
        };

        // Act
        var strategyCodes = new[]
        {
            new FmfLiguillaStrategy().FormatCode,
            new NhlBestOfSevenStrategy().FormatCode,
            new NflReseededStrategy().FormatCode,
            new UefaTwoLegStrategy().FormatCode
        };

        // Assert
        strategyCodes.Should().BeEquivalentTo(known);
        strategyCodes.Should().OnlyHaveUniqueItems();
    }
}
