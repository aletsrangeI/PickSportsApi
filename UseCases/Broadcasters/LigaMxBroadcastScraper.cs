using System.Net;
using System.Text.RegularExpressions;
using Common;
using DTO.Broadcast;
using Interface.UseCases;

namespace UseCases.Broadcasters;

/// <summary>
/// SPEC-015 · Extractor liviano de señales de TV desde <c>ligamx.net/cancha/partidos</c>.
/// <para>
/// Cada partido del HTML incluye un bloque de script con el contenedor
/// <c>transmision-tv-1-{idPartido}</c> y la variable <c>let canalesDeTV = ' CANAL 5, TUDN, Vix'</c>,
/// además de enlaces <c>cancha/mxm/{idPartido}/…/minuto-a-minuto-{local}-vs-{visita}-jornada-{n}-…</c>
/// de los que se obtienen los equipos y la jornada.
/// </para>
/// Ante cualquier falla (HTTP, timeout, HTML sin bloques) devuelve una lista vacía: nunca lanza.
/// </summary>
public class LigaMxBroadcastScraper : ILigaMxBroadcastScraper
{
    public const string HttpClientName = "LigaMxClient";
    public const string MatchesUrl     = "https://ligamx.net/cancha/partidos";

    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(2);

    private static readonly Regex ContainerRegex = new(
        @"transmision-tv-1-(\d+)", RegexOptions.Compiled, RegexTimeout);

    private static readonly Regex ChannelsRegex = new(
        @"canalesDeTV\s*=\s*(['""])(?<list>.*?)\1", RegexOptions.Compiled, RegexTimeout);

    private static readonly Regex MatchLinkRegex = new(
        @"cancha/[A-Za-z]+/(?<id>\d+)/[^/""'\s]+/(?<slug>[a-z0-9-]+-vs-[a-z0-9-]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase, RegexTimeout);

    private static readonly Regex SlugRegex = new(
        @"^(?<home>[a-z0-9-]+?)-vs-(?<away>[a-z0-9-]+?)-jornada-(?<week>\d+)(?:-|$)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase, RegexTimeout);

    /// <summary>Nombres de club en los slugs de ligamx.net → abreviatura ESPN.</summary>
    private static readonly Dictionary<string, string> TeamSlugAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["america"]                     = "AME",
        ["club-america"]                = "AME",
        ["atlas"]                       = "ATS",
        ["atlante"]                     = "ATL",
        ["atletico-de-san-luis"]        = "ASL",
        ["atletico-san-luis"]           = "ASL",
        ["san-luis"]                    = "ASL",
        ["cruz-azul"]                   = "CAZ",
        ["guadalajara"]                 = "GDL",
        ["chivas"]                      = "GDL",
        ["fc-juarez"]                   = "JUA",
        ["juarez"]                      = "JUA",
        ["leon"]                        = "LEO",
        ["monterrey"]                   = "MTY",
        ["rayados"]                     = "MTY",
        ["necaxa"]                      = "NCX",
        ["pachuca"]                     = "PAC",
        ["puebla"]                      = "PUE",
        ["puebla-fc"]                   = "PUE",
        ["queretaro"]                   = "QRO",
        ["gallos-blancos"]              = "QRO",
        ["gallos-blancos-de-queretaro"] = "QRO",
        ["santos"]                      = "SAN",
        ["santos-laguna"]               = "SAN",
        ["tigres"]                      = "UANL",
        ["uanl"]                        = "UANL",
        ["tigres-de-la-uanl"]           = "UANL",
        ["tijuana"]                     = "TIJ",
        ["xolos"]                       = "TIJ",
        ["toluca"]                      = "TOL",
        ["pumas"]                       = "UNAM",
        ["unam"]                        = "UNAM",
        ["pumas-unam"]                  = "UNAM",
        ["universidad-nacional"]        = "UNAM",
        ["mazatlan"]                    = "MAZ",
        ["mazatlan-fc"]                 = "MAZ",
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAppLogger<LigaMxBroadcastScraper> _logger;

    public LigaMxBroadcastScraper(IHttpClientFactory httpClientFactory, IAppLogger<LigaMxBroadcastScraper> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger            = logger;
    }

    public async Task<IReadOnlyList<ScrapedMatchBroadcast>> FetchCurrentWeekAsync(CancellationToken ct = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.GetAsync(MatchesUrl, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[LigaMx] {Url} respondió HTTP {Status}. Se conservan los canales por defecto.",
                    MatchesUrl, (int)response.StatusCode);
                return [];
            }

            var html   = await response.Content.ReadAsStringAsync(ct);
            var parsed = Parse(html);

            if (parsed.Count == 0)
                _logger.LogWarning("[LigaMx] El HTML no contiene bloques de canales reconocibles.");

            return parsed;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[LigaMx] Error consultando {Url}: {Error}. Se conservan los canales por defecto.",
                MatchesUrl, ex.Message);
            return [];
        }
    }

    /// <summary>Parser puro (sin I/O) del HTML de ligamx.net. Devuelve lista vacía si no reconoce la estructura.</summary>
    public static IReadOnlyList<ScrapedMatchBroadcast> Parse(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return [];

        try
        {
            var channelsById = ExtractChannelsById(html);
            if (channelsById.Count == 0) return [];

            var teamsById = ExtractTeamsById(html);
            var result    = new List<ScrapedMatchBroadcast>();

            foreach (var (id, channels) in channelsById)
            {
                if (!teamsById.TryGetValue(id, out var teams)) continue;
                result.Add(new ScrapedMatchBroadcast(teams.Home, teams.Away, teams.Week, channels));
            }

            return result;
        }
        catch (RegexMatchTimeoutException)
        {
            return [];
        }
    }

    /// <summary>
    /// Recorre los contenedores <c>transmision-tv-1-{id}</c> en orden y busca <c>canalesDeTV</c> solo dentro
    /// del segmento de ese partido, para no tomar por error los canales del siguiente.
    /// </summary>
    private static Dictionary<string, List<string>> ExtractChannelsById(string html)
    {
        var containers = ContainerRegex.Matches(html);
        var result     = new Dictionary<string, List<string>>();

        for (var i = 0; i < containers.Count; i++)
        {
            var id = containers[i].Groups[1].Value;
            if (result.ContainsKey(id)) continue;

            var start = containers[i].Index;
            var end   = html.Length;
            for (var j = i + 1; j < containers.Count; j++)
            {
                if (containers[j].Groups[1].Value != id)
                {
                    end = containers[j].Index;
                    break;
                }
            }

            var channelsMatch = ChannelsRegex.Match(html, start, end - start);
            if (!channelsMatch.Success) continue;

            var raw      = WebUtility.HtmlDecode(channelsMatch.Groups["list"].Value);
            var channels = BroadcastChannelCatalog.Sanitize(raw.Split(','));
            if (channels.Count > 0) result[id] = channels;
        }

        return result;
    }

    private static Dictionary<string, (string Home, string Away, int? Week)> ExtractTeamsById(string html)
    {
        var result = new Dictionary<string, (string Home, string Away, int? Week)>();

        foreach (Match link in MatchLinkRegex.Matches(html))
        {
            var id = link.Groups["id"].Value;
            if (result.ContainsKey(id)) continue;

            var slug = link.Groups["slug"].Value.ToLowerInvariant();
            var vs   = slug.IndexOf("-vs-", StringComparison.Ordinal);
            if (vs < 0) continue;

            // El slug inicia con un prefijo de sección ("minuto-a-minuto-", "informe-arbitral-", …)
            var homeSegment = slug[..vs];
            var rest        = slug[(vs + 4)..];

            var weekMatch = SlugRegex.Match("x-vs-" + rest);
            var awaySegment = weekMatch.Success ? weekMatch.Groups["away"].Value : rest;
            int? week = weekMatch.Success && int.TryParse(weekMatch.Groups["week"].Value, out var w) ? w : null;

            var home = ResolveTeamBySuffix(homeSegment);
            var away = ResolveTeamByPrefix(awaySegment);
            if (home == null || away == null) continue;

            result[id] = (home, away, week);
        }

        return result;
    }

    /// <summary>Busca el sufijo más largo (por tokens) del segmento que corresponda a un club.</summary>
    private static string? ResolveTeamBySuffix(string segment)
    {
        var tokens = segment.Split('-', StringSplitOptions.RemoveEmptyEntries);
        for (var start = 0; start < tokens.Length; start++)
        {
            var candidate = string.Join('-', tokens[start..]);
            if (TeamSlugAliases.TryGetValue(candidate, out var abbr)) return abbr;
        }
        return null;
    }

    /// <summary>Busca el prefijo más largo (por tokens) del segmento que corresponda a un club.</summary>
    private static string? ResolveTeamByPrefix(string segment)
    {
        var tokens = segment.Split('-', StringSplitOptions.RemoveEmptyEntries);
        for (var length = tokens.Length; length > 0; length--)
        {
            var candidate = string.Join('-', tokens[..length]);
            if (TeamSlugAliases.TryGetValue(candidate, out var abbr)) return abbr;
        }
        return null;
    }
}
