using System.Globalization;
using System.Text.Json;
using Domain.Common;
using Domain.Entities;
using Interface.Persistence;
using Interface.UseCases.Knockout;
using Microsoft.Extensions.Logging;

namespace UseCases.Liguilla.Espn;

/// <summary>
/// Sincroniza el cuadro de eliminatorias de una liga desde ESPN: siembra los nodos faltantes con la forma de la
/// estrategia, llena los cruces publicados, resuelve los terminados y avanza ganadores.
/// </summary>
public class EspnKnockoutSyncService : IEspnKnockoutSyncService
{
    private const string EspnClientName = "EspnClient";
    private const string ScoreboardBase = "https://site.web.api.espn.com/apis/site/v2/sports";
    private const string StandingsBase = "https://site.web.api.espn.com/apis/v2/sports";
    private const string PrimaryHost = "site.web.api.espn.com";
    private const string SecondaryHost = "site.api.espn.com";
    private const int ForbiddenStatusCode = 403;
    private const int ScoreboardLimit = 1000;

    private const string InformativePlayoffRound = "PO";
    private const string KnockoutPlayoffsSlug = "knockout-round-playoffs";
    private const string DivisionalRound = "DIV";
    private const int TopSeed = 1;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IKnockoutStrategyResolver _resolver;
    private readonly IEspnKnockoutParser _parser;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<EspnKnockoutSyncService> _logger;

    public EspnKnockoutSyncService(
        IUnitOfWork unitOfWork,
        IKnockoutStrategyResolver resolver,
        IEspnKnockoutParser parser,
        IHttpClientFactory httpClientFactory,
        ILogger<EspnKnockoutSyncService> logger)
    {
        _unitOfWork = unitOfWork;
        _resolver = resolver;
        _parser = parser;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<int> SyncLeagueAsync(int leagueId, CancellationToken ct = default)
    {
        var league = await _unitOfWork.Leagues.GetAsync(leagueId);
        if (league == null) return 0;

        var strategy = _resolver.For(league);
        if (strategy == null || !_parser.SupportedFormatCodes.Contains(strategy.FormatCode)) return 0;

        var season = await _unitOfWork.Seasons.GetCurrentSeasonAsync(leagueId);
        if (season == null) return 0;

        IReadOnlyList<EspnKnockoutTie> ties;
        IReadOnlyDictionary<string, NflPlayoffSeed> seeds;
        SeasonWindow? espnWindow;
        try
        {
            (ties, seeds, espnWindow) = await FetchAsync(league, season, strategy.FormatCode, ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "[EspnKnockoutSync] Fallo de red al consultar ESPN para la liga {LeagueId}.", leagueId);
            return 0;
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "[EspnKnockoutSync] Tiempo de espera agotado consultando ESPN para la liga {LeagueId}.", leagueId);
            return 0;
        }

        // ESPN devuelve por año calendario: descarta las eliminatorias de otra temporada (p. ej. los playoffs de enero previos).
        var start = season.StartDate ?? espnWindow?.Start;
        var end = season.EndDate ?? espnWindow?.End;
        if (!start.HasValue || !end.HasValue)
        {
            _logger.LogWarning(
                "[EspnKnockoutSync] Sin ventana de temporada (ni en la temporada {SeasonId} ni en ESPN) para la liga {LeagueId}; no se siembra ni resuelve nada.",
                season.Id, leagueId);
            return 0;
        }

        ties = ties.Where(t => IsInSeasonWindow(t, start.Value, end.Value)).ToList();
        if (ties.Count == 0) return 0;

        // NFL depende de las siembras de standings: sin ellas no se siembra ni resuelve nada (se reintenta en el siguiente ciclo).
        if (strategy.FormatCode == KnockoutFormatCodes.NflReseededSingleGame && seeds.Count == 0)
        {
            _logger.LogWarning("[EspnKnockoutSync] Sin siembras NFL de standings para la liga {LeagueId}; se reintentara en el siguiente ciclo.", leagueId);
            return 0;
        }

        var teams = (await _unitOfWork.Teams.GetByLeagueIdAsync(leagueId)).ToList();
        var nodes = await _unitOfWork.Liguilla.GetNodesBySeasonAsync(season.Id, ct);

        var context = new SyncContext(strategy, season.Id, teams, nodes);
        EnsureShape(context);
        PreSeedTopSeeds(context, seeds);
        foreach (var tie in ties)
            ApplyTie(context, tie, seeds);

        foreach (var node in strategy.AdvanceWinners(context.Nodes))
            context.Touched.Add(node);

        return await PersistAsync(context, ct);
    }

    /// <summary>
    /// Un cruce solo pertenece a la temporada vigente si arranca dentro de su ventana [StartDate, EndDate].
    /// Las fechas salen de la temporada y, si faltan, de <c>leagues[0].season</c> de ESPN. Sin KickoffUtc no se puede ubicar: se descarta.
    /// </summary>
    private static bool IsInSeasonWindow(EspnKnockoutTie tie, DateTime start, DateTime end) =>
        tie.KickoffUtc.HasValue && tie.KickoffUtc.Value >= start && tie.KickoffUtc.Value <= end;

    /// <summary>Lee solo <c>leagues[0].season.startDate/endDate</c> del scoreboard; null si falta o no parsea.</summary>
    private static SeasonWindow? ParseEspnWindow(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("leagues", out var leagues)
                || leagues.ValueKind != JsonValueKind.Array || leagues.GetArrayLength() == 0
                || !leagues[0].TryGetProperty("season", out var season)
                || season.ValueKind != JsonValueKind.Object)
                return null;

            var start = ReadUtcDate(season, "startDate");
            var end = ReadUtcDate(season, "endDate");
            return start.HasValue && end.HasValue ? new SeasonWindow(start.Value, end.Value) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static DateTime? ReadUtcDate(JsonElement season, string property)
    {
        if (!season.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String) return null;

        return DateTimeOffset.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed.UtcDateTime
            : null;
    }

    private sealed record SeasonWindow(DateTime Start, DateTime End);

    // ── Descarga ──────────────────────────────────────────────────────────────

    private async Task<(IReadOnlyList<EspnKnockoutTie> Ties, IReadOnlyDictionary<string, NflPlayoffSeed> Seeds, SeasonWindow? Window)> FetchAsync(
        League league, Season season, string formatCode, CancellationToken ct)
    {
        var (sport, leagueCode) = ResolveEspnPath(league);
        var ties = new Dictionary<string, EspnKnockoutTie>();
        SeasonWindow? yearWindow = null;

        // Las fases finales caen en el año siguiente al de arranque de la temporada (NFL, Champions).
        var years = new[] { season.Year, season.EndDate?.Year ?? season.Year + 1 }.Distinct();
        foreach (var year in years)
        {
            var json = await FetchJsonAsync($"{ScoreboardBase}/{sport}/{leagueCode}/scoreboard?dates={year}&limit={ScoreboardLimit}", ct);
            if (json == null) continue;

            yearWindow ??= ParseEspnWindow(json);
            foreach (var tie in _parser.ParseScoreboard(json, formatCode))
            {
                if (!ties.TryGetValue(tie.GroupKey, out var known) || tie.SeriesCompleted || !known.SeriesCompleted)
                    ties[tie.GroupKey] = tie;
            }
        }

        IReadOnlyDictionary<string, NflPlayoffSeed> seeds = new Dictionary<string, NflPlayoffSeed>();
        if (formatCode == KnockoutFormatCodes.NflReseededSingleGame)
        {
            var standings = await FetchJsonAsync($"{StandingsBase}/{sport}/{leagueCode}/standings?season={season.Year}", ct);
            if (standings != null)
                seeds = _parser.ParseNflPlayoffSeeds(standings);
        }

        // Solo se pide la ventana vigente (scoreboard sin "dates") cuando la temporada no trae ambas fechas.
        SeasonWindow? window = null;
        if (!season.StartDate.HasValue || !season.EndDate.HasValue)
        {
            var current = await FetchJsonAsync($"{ScoreboardBase}/{sport}/{leagueCode}/scoreboard?limit={ScoreboardLimit}", ct);
            window = ParseEspnWindow(current) ?? yearWindow;
        }

        return (ties.Values.ToList(), seeds, window);
    }

    private async Task<string?> FetchJsonAsync(string url, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient(EspnClientName);
        using var response = await client.GetAsync(url, ct);

        if ((int)response.StatusCode == ForbiddenStatusCode)
        {
            using var fallback = await client.GetAsync(url.Replace(PrimaryHost, SecondaryHost), ct);
            return await ReadBodyAsync(fallback, url, ct);
        }

        return await ReadBodyAsync(response, url, ct);
    }

    private async Task<string?> ReadBodyAsync(HttpResponseMessage response, string url, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return await response.Content.ReadAsStringAsync(ct);

        _logger.LogWarning("[EspnKnockoutSync] ESPN respondió {Status} para {Url}.", (int)response.StatusCode, url);
        return null;
    }

    private static (string Sport, string LeagueCode) ResolveEspnPath(League league)
    {
        var parts = (league.EspnApiPath ?? string.Empty).Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var leagueCode = parts.Length > 0 ? parts[^1] : league.Code;

        var sport = parts.Length >= 2 && parts[^2] != "sports"
            ? parts[^2]
            : string.IsNullOrWhiteSpace(league.SportKey) ? "soccer" : league.SportKey;
        return (sport, leagueCode);
    }

    // ── Siembra y aplicación ──────────────────────────────────────────────────

    /// <summary>Crea los nodos que faltan con la forma de la estrategia (Round/NodeNumber/Conference).</summary>
    private static void EnsureShape(SyncContext context)
    {
        foreach (var round in context.Strategy.Rounds)
        {
            var start = context.RoundStart[round.Code];
            for (var offset = 0; offset < round.NodeCount; offset++)
            {
                var number = start + offset;
                if (context.Nodes.Any(n => n.NodeNumber == number)) continue;

                AddNode(context, new LiguillaNode
                {
                    SeasonId = context.SeasonId,
                    Round = round.Code,
                    NodeNumber = number,
                    Conference = ConferenceOfBlock(context.Strategy.FormatCode, round, offset),
                    Active = true
                });
            }
        }
    }

    private static void AddNode(SyncContext context, LiguillaNode node)
    {
        context.Nodes.Add(node);
        context.Created.Add(node);
    }

    private void ApplyTie(SyncContext context, EspnKnockoutTie tie, IReadOnlyDictionary<string, NflPlayoffSeed> seeds)
    {
        var roundCode = RoundCodeOf(context.Strategy.FormatCode, tie);
        if (roundCode == null) return;

        if (roundCode == InformativePlayoffRound)
        {
            ApplyInformativePlayoff(context, tie);
            return;
        }

        if (tie.HomeEspnTeamId == null || tie.AwayEspnTeamId == null) return;
        if (!context.TeamsByEspnId.TryGetValue(tie.HomeEspnTeamId, out var home)
            || !context.TeamsByEspnId.TryGetValue(tie.AwayEspnTeamId, out var away))
        {
            _logger.LogWarning(
                "[EspnKnockoutSync] Cruce {GroupKey} omitido: equipo desconocido en la liga (local {Home}, visita {Away}).",
                tie.GroupKey, tie.HomeEspnTeamId, tie.AwayEspnTeamId);
            return;
        }

        var node = FindNode(context, roundCode, tie, home, away);
        if (node == null)
        {
            _logger.LogWarning("[EspnKnockoutSync] Sin nodo libre en {Round} para el cruce {GroupKey}.", roundCode, tie.GroupKey);
            return;
        }

        // Un nodo resuelto (por ESPN o por el admin) no se vuelve a tocar.
        if (node.IsResolved) return;

        FillNode(context, node, tie, home, away, seeds);
        Resolve(context, node, tie);
    }

    private LiguillaNode? FindNode(SyncContext context, string roundCode, EspnKnockoutTie tie, Team home, Team away)
    {
        var inRound = context.Nodes.Where(n => n.Round == roundCode).ToList();

        var byKey = inRound.FirstOrDefault(n => n.EspnEventGroupKey == tie.GroupKey);
        if (byKey != null) return byKey;

        // Nodo ya sembrado por AdvanceWinners con alguno de los dos equipos del cruce.
        var tieTeamIds = new[] { home.Id, away.Id };
        var seeded = inRound.FirstOrDefault(n =>
            n.EspnEventGroupKey == null
            && (n.TeamAId.HasValue || n.TeamBId.HasValue)
            && new[] { n.TeamAId, n.TeamBId }.Where(id => id.HasValue).All(id => tieTeamIds.Contains(id!.Value)));
        if (seeded != null) return seeded;

        var blockIndex = BlockIndexOf(tie.Conference);
        var candidates = inRound.Where(n => IsFree(n) && (blockIndex == null || n.Conference == tie.Conference));
        return candidates.OrderBy(n => n.NodeNumber).FirstOrDefault()
               ?? inRound.Where(IsFree).OrderBy(n => n.NodeNumber).FirstOrDefault();
    }

    private static bool IsFree(LiguillaNode node) =>
        !node.TeamAId.HasValue && !node.TeamBId.HasValue && node.EspnEventGroupKey == null && !node.IsResolved;

    private static void FillNode(
        SyncContext context, LiguillaNode node, EspnKnockoutTie tie, Team home, Team away,
        IReadOnlyDictionary<string, NflPlayoffSeed> seeds)
    {
        var changed = false;

        if (node.EspnEventGroupKey != tie.GroupKey)
        {
            node.EspnEventGroupKey = tie.GroupKey;
            changed = true;
        }

        if (!node.TeamAId.HasValue && node.TeamBId != home.Id)
        {
            node.TeamAId = home.Id;
            node.TeamA = home;
            changed = true;
        }
        else if (!node.TeamAId.HasValue)
        {
            node.TeamAId = away.Id;
            node.TeamA = away;
            changed = true;
        }

        if (!node.TeamBId.HasValue)
        {
            var other = node.TeamAId == home.Id ? away : home;
            node.TeamBId = other.Id;
            node.TeamB = other;
            changed = true;
        }

        if (seeds.Count > 0)
        {
            node.SeedA ??= SeedOf(seeds, node.TeamA ?? home);
            node.SeedB ??= SeedOf(seeds, node.TeamB ?? away);
        }

        if (!node.KickoffUtc.HasValue && tie.KickoffUtc.HasValue)
        {
            node.KickoffUtc = tie.KickoffUtc;
            changed = true;
        }

        if (changed) context.Touched.Add(node);
    }

    private static int? SeedOf(IReadOnlyDictionary<string, NflPlayoffSeed> seeds, Team team) =>
        seeds.TryGetValue(team.EspnTeamId, out var seed) ? seed.Seed : null;

    /// <summary>
    /// NFL: el seed 1 de cada conferencia descansa en Wild Card, asi que se pre-siembra como TeamA (SeedA = 1)
    /// del primer nodo DIV de su conferencia. Idempotente: no pisa nodos con equipos, llave ESPN o ya resueltos.
    /// </summary>
    private static void PreSeedTopSeeds(SyncContext context, IReadOnlyDictionary<string, NflPlayoffSeed> seeds)
    {
        if (context.Strategy.FormatCode != KnockoutFormatCodes.NflReseededSingleGame) return;

        foreach (var (espnTeamId, seed) in seeds)
        {
            if (seed.Seed != TopSeed) continue;
            if (!context.TeamsByEspnId.TryGetValue(espnTeamId, out var team)) continue;

            var divisional = context.Nodes
                .Where(n => n.Round == DivisionalRound && n.Conference == seed.Conference)
                .OrderBy(n => n.NodeNumber)
                .ToList();
            if (divisional.Any(n => n.TeamAId == team.Id || n.TeamBId == team.Id)) continue;

            var first = divisional.FirstOrDefault();
            if (first == null || !IsFree(first)) continue;

            first.TeamAId = team.Id;
            first.TeamA = team;
            first.SeedA = TopSeed;
            context.Touched.Add(first);
        }
    }

    private static void Resolve(SyncContext context, LiguillaNode node, EspnKnockoutTie tie)
    {
        var outcome = context.Strategy.ResolveFromEspn(node, tie);
        if (!outcome.IsResolved || !outcome.WinnerTeamId.HasValue) return;

        node.IsResolved = true;
        node.WinnerTeamId = outcome.WinnerTeamId;
        node.WinnerTeam = context.TeamsById.GetValueOrDefault(outcome.WinnerTeamId.Value);
        node.ResolvedAtUtc = DateTime.UtcNow;
        context.Touched.Add(node);
    }

    /// <summary>El nodo "PO" (NodeNumber 0) avisa que los playoffs previos existen y se resuelve al terminarlos todos.</summary>
    private static void ApplyInformativePlayoff(SyncContext context, EspnKnockoutTie tie)
    {
        var node = context.Nodes.FirstOrDefault(n => n.Round == InformativePlayoffRound);
        if (node == null)
        {
            node = new LiguillaNode { SeasonId = context.SeasonId, Round = InformativePlayoffRound, NodeNumber = 0, Active = true };
            AddNode(context, node);
        }

        if (node.IsResolved) return;

        context.PlayoffTies.Add(tie);
        var earliest = context.PlayoffTies.Where(t => t.KickoffUtc.HasValue).Min(t => t.KickoffUtc);
        if (node.KickoffUtc != earliest)
        {
            node.KickoffUtc = earliest;
            context.Touched.Add(node);
        }

        node.EspnEventGroupKey ??= KnockoutPlayoffsSlug;
        if (context.PlayoffTies.All(t => t.SeriesCompleted))
        {
            node.IsResolved = true;
            node.ResolvedAtUtc = DateTime.UtcNow;
        }

        context.Touched.Add(node);
    }

    // ── Mapeo de rondas ───────────────────────────────────────────────────────

    private static string? RoundCodeOf(string formatCode, EspnKnockoutTie tie)
    {
        var headline = tie.Headline;
        return formatCode switch
        {
            KnockoutFormatCodes.NhlBestOf7 => Contains(headline, "1st Round") ? "R1"
                : Contains(headline, "2nd Round") ? "R2"
                : Contains(headline, "Conference Final") ? "CF"
                : Contains(headline, "Stanley Cup") ? "FINAL"
                : null,
            KnockoutFormatCodes.NflReseededSingleGame => Contains(headline, "Wild Card") ? "WC"
                : Contains(headline, "Divisional") ? "DIV"
                : Contains(headline, "Conference Championship") ? "CONF"
                : Contains(headline, "Super Bowl") ? "SB"
                : null,
            KnockoutFormatCodes.UefaTwoLeg => tie.RoundSlug switch
            {
                "round-of-16" => "R16",
                "quarterfinals" => "QF",
                "semifinals" => "SF",
                "final" => "FINAL",
                KnockoutPlayoffsSlug => InformativePlayoffRound,
                _ => null
            },
            _ => null
        };
    }

    private static bool Contains(string text, string fragment) =>
        text.Contains(fragment, StringComparison.OrdinalIgnoreCase);

    /// <summary>Rondas de varios nodos de NHL/NFL: la primera mitad es la conferencia "east"/"afc", la segunda "west"/"nfc".</summary>
    private static string? ConferenceOfBlock(string formatCode, KnockoutRound round, int offset)
    {
        if (round.NodeCount < 2) return null;

        var isFirstHalf = offset < round.NodeCount / 2;
        return formatCode switch
        {
            KnockoutFormatCodes.NhlBestOf7 => isFirstHalf ? "east" : "west",
            KnockoutFormatCodes.NflReseededSingleGame => isFirstHalf ? "afc" : "nfc",
            _ => null
        };
    }

    private static int? BlockIndexOf(string? conference) => conference switch
    {
        "east" or "afc" => 0,
        "west" or "nfc" => 1,
        _ => null
    };

    // ── Persistencia ──────────────────────────────────────────────────────────

    private async Task<int> PersistAsync(SyncContext context, CancellationToken ct)
    {
        var changed = context.Created.Concat(context.Touched).Distinct(ReferenceEqualityComparer.Instance).Cast<LiguillaNode>().ToList();
        if (changed.Count == 0) return 0;

        foreach (var node in changed)
        {
            if (context.Created.Contains(node))
                await _unitOfWork.Liguilla.AddNodeAsync(node, ct);
            else
                await _unitOfWork.Liguilla.UpdateNodeAsync(node, ct);
        }

        await _unitOfWork.Save(ct);
        return changed.Count;
    }

    private sealed class SyncContext
    {
        public SyncContext(IKnockoutFormatStrategy strategy, int seasonId, List<Team> teams, List<LiguillaNode> nodes)
        {
            Strategy = strategy;
            SeasonId = seasonId;
            Nodes = nodes;
            TeamsById = teams.GroupBy(t => t.Id).ToDictionary(g => g.Key, g => g.First());
            TeamsByEspnId = teams
                .Where(t => !string.IsNullOrEmpty(t.EspnTeamId))
                .GroupBy(t => t.EspnTeamId)
                .ToDictionary(g => g.Key, g => g.First());

            var next = 1;
            foreach (var round in strategy.Rounds.OrderBy(r => r.Order))
            {
                RoundStart[round.Code] = next;
                next += round.NodeCount;
            }

            foreach (var node in nodes)
            {
                node.TeamA ??= node.TeamAId.HasValue ? TeamsById.GetValueOrDefault(node.TeamAId.Value) : null;
                node.TeamB ??= node.TeamBId.HasValue ? TeamsById.GetValueOrDefault(node.TeamBId.Value) : null;
            }
        }

        public IKnockoutFormatStrategy Strategy { get; }
        public int SeasonId { get; }
        public List<LiguillaNode> Nodes { get; }
        public Dictionary<int, Team> TeamsById { get; }
        public Dictionary<string, Team> TeamsByEspnId { get; }
        public Dictionary<string, int> RoundStart { get; } = new();
        public HashSet<LiguillaNode> Created { get; } = new(ReferenceEqualityComparer.Instance);
        public HashSet<LiguillaNode> Touched { get; } = new(ReferenceEqualityComparer.Instance);
        public List<EspnKnockoutTie> PlayoffTies { get; } = new();
    }
}
