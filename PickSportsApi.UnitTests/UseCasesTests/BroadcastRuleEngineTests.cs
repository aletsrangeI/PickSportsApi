using FluentAssertions;
using UseCases.Broadcasters;
using Xunit;

namespace PickSportsApi.UnitTests.UseCasesTests;

public class BroadcastRuleEngineTests
{
    private readonly BroadcastRuleEngine _engine = new();

    [Theory]
    [InlineData("AME",  "Canal 5|TUDN|ViX Premium")]
    [InlineData("ATS",  "Canal 5|TUDN|ViX Premium")]
    [InlineData("ATL",  "Azteca 7|Azteca Deportes")]
    [InlineData("ASL",  "ESPN|Disney+|ViX Premium")]
    [InlineData("CAZ",  "Canal 5|TUDN|ViX Premium")]
    [InlineData("GDL",  "Amazon Prime Video")]
    [InlineData("JUA",  "Azteca 7|FOX|Azteca Deportes|FOX One|ViX Premium")]
    [InlineData("LEO",  "FOX|FOX One|ViX Premium")]
    [InlineData("MTY",  "Canal 5|TUDN|ViX Premium")]
    [InlineData("NCX",  "FOX|FOX One|ViX Premium")]
    [InlineData("PAC",  "FOX|FOX One|Claro Sports")]
    [InlineData("PUE",  "Azteca 7|FOX|Azteca Deportes|FOX One")]
    [InlineData("QRO",  "FOX|FOX One")]
    [InlineData("SAN",  "ESPN|TUDN|Disney+|ViX Premium")]
    [InlineData("UANL", "Azteca 7|FOX|Azteca Deportes|FOX One")]
    [InlineData("TIJ",  "FOX|FOX One")]
    [InlineData("TOL",  "Canal 5|TUDN|ViX Premium")]
    [InlineData("UNAM", "Las Estrellas|Canal 5|TUDN|ViX Premium")]
    public void Resolve_ConClubLocalDeLigaMx_DevuelveCanalesDeLaMatriz(string homeAbbr, string expected)
    {
        // Arrange
        var expectedChannels = expected.Split('|');

        // Act
        var channels = _engine.Resolve(homeAbbr);

        // Assert
        channels.Should().Equal(expectedChannels);
    }

    [Fact]
    public void Resolve_NingunClub_AsignaFoxSportsDescontinuado()
    {
        // Arrange & Act
        var all = BroadcastRuleEngine.CoveredTeams.SelectMany(t => _engine.Resolve(t));

        // Assert
        all.Should().NotContain("FOX Sports");
    }

    [Fact]
    public void CoveredTeams_CubreLos18ClubesDeLigaMx()
    {
        // Arrange & Act
        var covered = BroadcastRuleEngine.CoveredTeams;

        // Assert
        covered.Should().HaveCount(18);
    }

    [Theory]
    [InlineData("NEC", "NCX")]
    [InlineData("PUM", "UNAM")]
    [InlineData("TIG", "UANL")]
    [InlineData("CRU", "CAZ")]
    [InlineData("gdl", "GDL")]
    public void Resolve_ConAliasDeAbreviatura_DevuelveLosCanalesDelClubCanonico(string alias, string canonical)
    {
        // Arrange
        var expected = _engine.Resolve(canonical);

        // Act
        var channels = _engine.Resolve(alias);

        // Assert
        channels.Should().Equal(expected);
        channels.Should().NotContain(BroadcastChannelCatalog.PorConfirmar);
    }

    [Theory]
    [InlineData("MAZ")]
    [InlineData("XYZ")]
    [InlineData("")]
    [InlineData(null)]
    public void Resolve_ConEquipoFueraDelCatalogo_DevuelvePorConfirmar(string? homeAbbr)
    {
        // Arrange & Act
        var channels = _engine.Resolve(homeAbbr);

        // Assert
        channels.Should().ContainSingle().Which.Should().Be(BroadcastChannelCatalog.PorConfirmar);
    }

    [Fact]
    public void Resolve_ChivasLocal_AsignaSoloAmazonPrimeVideo()
    {
        // Arrange & Act
        var channels = _engine.Resolve("GDL");

        // Assert
        channels.Should().ContainSingle().Which.Should().Be("Amazon Prime Video");
    }
}

public class BroadcastChannelCatalogTests
{
    [Theory]
    [InlineData("foxone", "FOX One")]
    [InlineData(" CANAL 5", "Canal 5")]
    [InlineData("Vix", "ViX Premium")]
    [InlineData("AZTECA 7", "Azteca 7")]
    [InlineData("prime video", "Amazon Prime Video")]
    [InlineData("Caliente 2", "FOX One")]
    [InlineData("FOX Sports", "FOX")]
    [InlineData("fox plus", "FOX+")]
    [InlineData("CALIENTE TV", "FOX One")]
    public void Normalize_ConNombreDeLigaMx_DevuelveNombreCanonico(string raw, string expected)
    {
        // Arrange & Act
        var normalized = BroadcastChannelCatalog.Normalize(raw);

        // Assert
        normalized.Should().Be(expected);
    }

    [Fact]
    public void Sanitize_ConDuplicadosYVacios_DevuelveListaLimpia()
    {
        // Arrange
        var raw = new[] { " TUDN", "", "tudn", "Vix", "   ", "ViX Premium" };

        // Act
        var result = BroadcastChannelCatalog.Sanitize(raw);

        // Assert
        result.Should().Equal("TUDN", "ViX Premium");
    }

    [Fact]
    public void Sanitize_CalienteYFoxOneJuntos_DejaUnSoloFoxOne()
    {
        // Arrange
        var raw = new[] { "foxone", "Caliente 2" };

        // Act
        var result = BroadcastChannelCatalog.Sanitize(raw);

        // Assert
        result.Should().Equal("FOX One");
    }

    [Fact]
    public void SerializeYDeserialize_ConservanLaLista()
    {
        // Arrange
        var channels = new List<string> { "Canal 5", "TUDN", "ViX Premium" };

        // Act
        var roundTrip = BroadcastChannelCatalog.Deserialize(BroadcastChannelCatalog.Serialize(channels));

        // Assert
        roundTrip.Should().Equal(channels);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("[not json")]
    public void Deserialize_ConValorVacioOInvalido_DevuelveListaVacia(string? stored)
    {
        // Arrange & Act
        var result = BroadcastChannelCatalog.Deserialize(stored);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Deserialize_ConCanalesDescontinuadosGuardados_LosDevuelveNormalizados()
    {
        // Arrange
        var stored = "[\"FOX Sports\",\"FOX\",\"Caliente 2\",\"FOX One\"]";

        // Act
        var result = BroadcastChannelCatalog.Deserialize(stored);

        // Assert
        result.Should().Equal("FOX", "FOX One");
    }

    [Fact]
    public void Deserialize_ConListaSeparadaPorComas_DevuelveCanales()
    {
        // Arrange & Act
        var result = BroadcastChannelCatalog.Deserialize("Canal 5, TUDN");

        // Assert
        result.Should().Equal("Canal 5", "TUDN");
    }
}
