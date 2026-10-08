using System.Net;
using Common;
using FluentAssertions;
using Moq;
using UseCases.Broadcasters;
using Xunit;

namespace PickSportsApi.UnitTests.UseCasesTests;

public class LigaMxBroadcastScraperTests
{
    /// <summary>Fragmento representativo de ligamx.net/cancha/partidos (estructura real, Jornada 11).</summary>
    private const string RepresentativeHtml = """
        <li>
          <div id="transmision-tv-1-155013"></div>
          <ul class="nacionalMarcador">
            <li><a class="loadershow mxmM" href="cancha/mxm/155013/eyJpZENsdWJsb2NhbCI6IjExNTUwIn0/minuto-a-minuto-puebla-fc-vs-leon-jornada-11-estadio-cuauhtemoc-disney">Minuto a Minuto</a></li>
            <li><a class="loadershow estadioM" href="cancha/estadio/9/estadio-cuauhtemoc">Cuauhtémoc</a></li>
          </ul>
        </li>
        <script>
          LMX.consultarServicio(url, {idPartido: '155013'}, function (data) {
            var transmisionHTML = document.getElementById('transmision-tv-1-155013')
            if (data && data["DatosJSON"]) { } else {
              let numeroDeCanales = '2'
              let canalesDeTV = ' ESPN, Disney+'.split(',')
            }
          });
        </script>
        <li>
          <div id="transmision-tv-1-155014"></div>
          <a href="cancha/informeArbitral/155014/eyJpZENsdWJsb2NhbCI6IjEwIn0/informe-arbitral-universidad-nacional-vs-cruz-azul-jornada-11-estadio-olimpico-universitario-vix">Informe</a>
        </li>
        <script>
          var transmisionHTML = document.getElementById('transmision-tv-1-155014')
          let canalesDeTV = ' CANAL 5, TUDN, Vix'.split(',')
        </script>
        <li>
          <div id="transmision-tv-1-155015"></div>
          <a href="cancha/mxm/155015/eyJ9/minuto-a-minuto-gallos-blancos-de-queretaro-vs-atlante-jornada-11-estadio-la-corregidora-foxone">MxM</a>
        </li>
        <script>
          let canalesDeTV = ' foxone, Caliente 2'.split(',')
        </script>
        """;

    [Fact]
    public void Parse_ConHtmlRepresentativo_ExtraeEquiposJornadaYCanales()
    {
        // Arrange & Act
        var result = LigaMxBroadcastScraper.Parse(RepresentativeHtml);

        // Assert
        result.Should().HaveCount(3);

        var puebla = result.Single(r => r.HomeAbbr == "PUE");
        puebla.AwayAbbr.Should().Be("LEO");
        puebla.WeekNumber.Should().Be(11);
        puebla.Channels.Should().Equal("ESPN", "Disney+");

        var pumas = result.Single(r => r.HomeAbbr == "UNAM");
        pumas.AwayAbbr.Should().Be("CAZ");
        pumas.Channels.Should().Equal("Canal 5", "TUDN", "ViX Premium");

        var queretaro = result.Single(r => r.HomeAbbr == "QRO");
        queretaro.AwayAbbr.Should().Be("ATL");
        queretaro.Channels.Should().Equal("FOX One");
    }

    [Fact]
    public void Parse_PartidoSinCanales_NoTomaLosCanalesDelSiguientePartido()
    {
        // Arrange
        const string html = """
            <div id="transmision-tv-1-1"></div>
            <a href="cancha/mxm/1/abc/minuto-a-minuto-america-vs-monterrey-jornada-5-estadio">x</a>
            <script>document.getElementById('transmision-tv-1-1')</script>
            <div id="transmision-tv-1-2"></div>
            <a href="cancha/mxm/2/abc/minuto-a-minuto-toluca-vs-necaxa-jornada-5-estadio">x</a>
            <script>let canalesDeTV = ' CANAL 5, TUDN'.split(',')</script>
            """;

        // Act
        var result = LigaMxBroadcastScraper.Parse(html);

        // Assert
        result.Should().ContainSingle();
        result[0].HomeAbbr.Should().Be("TOL");
        result[0].AwayAbbr.Should().Be("NCX");
        result[0].WeekNumber.Should().Be(5);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<html><body><p>Mantenimiento</p></body></html>")]
    [InlineData("<div id=\"transmision-tv-1-99\"></div><script>let canalesDeTV = ''</script>")]
    public void Parse_ConHtmlVacioOSinBloquesDeCanales_DevuelveListaVacia(string? html)
    {
        // Arrange & Act
        var result = LigaMxBroadcastScraper.Parse(html);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Parse_ConEquipoNoReconocido_OmiteElPartido()
    {
        // Arrange
        const string html = """
            <div id="transmision-tv-1-7"></div>
            <a href="cancha/mxm/7/abc/minuto-a-minuto-equipo-fantasma-vs-toluca-jornada-3-estadio">x</a>
            <script>let canalesDeTV = ' TUDN'.split(',')</script>
            """;

        // Act
        var result = LigaMxBroadcastScraper.Parse(html);

        // Assert
        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task FetchCurrentWeekAsync_ConRespuestaHttpDeError_DevuelveListaVaciaSinLanzar(HttpStatusCode status)
    {
        // Arrange
        var scraper = CreateScraper(new StubHandler(_ => new HttpResponseMessage(status)));

        // Act
        var result = await scraper.FetchCurrentWeekAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchCurrentWeekAsync_ConFallaDeRed_DevuelveListaVaciaSinLanzar()
    {
        // Arrange
        var scraper = CreateScraper(new StubHandler(_ => throw new HttpRequestException("DNS failure")));

        // Act
        var act = () => scraper.FetchCurrentWeekAsync();

        // Assert
        (await act.Should().NotThrowAsync()).Subject.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchCurrentWeekAsync_ConHtmlValido_DevuelveSenalesParseadas()
    {
        // Arrange
        var scraper = CreateScraper(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(RepresentativeHtml)
        }));

        // Act
        var result = await scraper.FetchCurrentWeekAsync();

        // Assert
        result.Should().HaveCount(3);
    }

    private static LigaMxBroadcastScraper CreateScraper(HttpMessageHandler handler)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(LigaMxBroadcastScraper.HttpClientName))
            .Returns(() => new HttpClient(handler, disposeHandler: false));
        return new LigaMxBroadcastScraper(factory.Object, new Mock<IAppLogger<LigaMxBroadcastScraper>>().Object);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }
}
