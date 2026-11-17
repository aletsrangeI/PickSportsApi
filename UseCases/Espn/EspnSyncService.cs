using Common;
using Domain.Entities;
using Interface.Persistence;
using Interface.UseCases;
using UseCases.Broadcasters;

namespace UseCases.Espn;

/// <summary>
/// Sincronización ESPN con fallback en cascada:
/// L1 (calendar endpoint) → L2 (scoreboard date-range scan) → L3 (config stubs).
/// </summary>
public class EspnSyncService : IEspnSyncService
{
    private const string PrimaryBase   = "https://site.web.api.espn.com/apis/site/v2/sports";
    private const string SecondaryBase = "https://sports.core.api.espn.com/v2/sports";
    private const int EspnEventLimit   = 1000; // ESPN no pagina el scoreboard: tope duro por respuesta

    private readonly IHttpClientFactory   _httpClientFactory;
    private readonly IUnitOfWork          _unitOfWork;
    private readonly EspnScoreboardParser _parser;
    private readonly IAppLogger<EspnSyncService> _logger;
    private readonly BroadcastRuleEngine  _broadcastRules;

    public EspnSyncService(
        IHttpClientFactory httpClientFactory,
        IUnitOfWork unitOfWork,
        EspnScoreboardParser parser,
        IAppLogger<EspnSyncService> logger,
        BroadcastRuleEngine broadcastRules)
    {
        _httpClientFactory = httpClientFactory;
        _unitOfWork        = unitOfWork;
        _parser            = parser;
        _logger            = logger;
        _broadcastRules    = broadcastRules;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // RESOLVE ESPN PATH
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Resuelve el deporte y el slug de liga de forma segura sin importar si
    /// EspnApiPath en BD viene como "sports/soccer/mex.1", "soccer/mex.1" o "mex.1".
    /// </summary>
    private static (string sport, string leagueCode) ResolveEspnPath(League league)
    {
        var raw = (league.EspnApiPath ?? "").Trim('/');
        var parts = raw.Split('/', StringSplitOptions.RemoveEmptyEntries);

        var leagueCode = parts.Length > 0 ? parts[^1] : (league.Code ?? "mex.1");

        string sport;
        if (parts.Length >= 2 && parts[^2] != "sports")
            sport = parts[^2];
        else if (!string.IsNullOrWhiteSpace(league.SportKey))
            sport = league.SportKey;
        else
            sport = "soccer";

        return (sport, leagueCode);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // SYNC FULL SEASON   L1 → L2 → L3
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task<SyncResult> SyncFullSeasonAsync(int seasonId, CancellationToken ct = default)
    {
        var season = await _unitOfWork.Seasons.GetAsync(seasonId);
        if (season == null)
            return new SyncResult(false, 0, 0, 0, "ERROR", "Temporada no encontrada.");

        var league = await _unitOfWork.Leagues.GetAsync(season.LeagueId);
        if (league == null)
            return new SyncResult(false, 0, 0, 0, "ERROR", "Liga no encontrada.");

        // ── L1: ESPN Calendar endpoint ────────────────────────────────────────
        var (l1Weeks, l1Error) = await TryCalendarEndpointAsync(season, league, ct);
        var minExpected = Math.Max(1, (int)(league.WeeksCount * 0.5));

        if (l1Weeks.Count >= minExpected)
        {
            _logger.LogInformation("[ESPN L1] Calendar exitoso: {0} semanas para '{1}'", l1Weeks.Count, season.Name);
            return await UpsertWeeksAndSyncMatchesAsync(season, league, l1Weeks, "L1_CALENDAR", ct);
        }

        _logger.LogWarning("[ESPN L1] Incompleto ({0} semanas, mínimo {1}). Razón: {2}. Activando L2 (Scoreboard)...",
            l1Weeks.Count, minExpected, l1Error ?? "sin semanas suficientes en calendar");

        // ── L2: Scoreboard date-range scan ────────────────────────────────────
        var (l2Weeks, l2Error) = await TryScoreboardDateRangeAsync(season, league, ct);

        if (l2Weeks.Count > 0)
        {
            _logger.LogInformation("[ESPN L2] Scoreboard exitoso: {0} semanas encontradas. Fallback L2 activo.", l2Weeks.Count);
            return await UpsertWeeksAndSyncMatchesAsync(season, league, l2Weeks, "L2_SCOREBOARD", ct);
        }

        _logger.LogWarning("[ESPN L2] Fallido también. {0}. Activando L3 (Config)...", l2Error ?? "sin datos");

        // ── L3: Generación por configuración de liga ──────────────────────────
        var l3Weeks = GenerateWeeksFromConfig(season, league);
        _logger.LogWarning("[ESPN L3] Generando {0} semanas stub desde config de liga. Sin partidos.", l3Weeks.Count);

        var (weeksSynced, _, _) = await UpsertWeekStubsAsync(season, l3Weeks, ct);

        await LogHealthAsync(league.EspnApiPath, 0, false,
            $"Fallback L3 activo — {weeksSynced} semanas generadas sin partidos. Sincroniza cada jornada manualmente.");

        return new SyncResult(true, weeksSynced, 0, 0, "L3_CONFIG",
            "Fallback L3: semanas generadas por configuración. Sincroniza cada jornada manualmente.");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // SYNC WEEK INDIVIDUAL
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task<SyncResult> SyncWeekAsync(int weekId, CancellationToken ct = default)
    {
        var week = await _unitOfWork.Weeks.GetAsync(weekId);
        if (week == null)
            return new SyncResult(false, 0, 0, 0, "ERROR", "Jornada no encontrada.");

        var season = await _unitOfWork.Seasons.GetAsync(week.SeasonId);
        var league = season != null ? await _unitOfWork.Leagues.GetAsync(season.LeagueId) : null;
        if (season == null || league == null)
            return new SyncResult(false, 0, 0, 0, "ERROR", "Temporada o liga no encontrada.");

        var (sport, leagueCode) = ResolveEspnPath(league);
        var games = await FetchGamesForWeekAsync(week, sport, leagueCode, ct);

        if (games.Count == 0)
        {
            return new SyncResult(false, 0, 0, 0, "ERROR", "No se encontraron partidos para esta jornada en ESPN.");
        }

        var (matches, teams) = await UpsertGamesAsync(games, week, league);
        return new SyncResult(true, 1, matches, teams, "L1_CALENDAR", null);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // IMPORT MANUAL JSON
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task<SyncResult> ImportManualJsonAsync(int weekId, string rawJson)
    {
        var week = await _unitOfWork.Weeks.GetAsync(weekId);
        if (week == null)
            return new SyncResult(false, 0, 0, 0, "ERROR", "Jornada no encontrada.");

        var season = await _unitOfWork.Seasons.GetAsync(week.SeasonId);
        var league = season != null ? await _unitOfWork.Leagues.GetAsync(season.LeagueId) : null;
        if (season == null || league == null)
            return new SyncResult(false, 0, 0, 0, "ERROR", "Temporada o liga no encontrada.");

        try
        {
            var games = _parser.ParseScoreboard(rawJson);
            if (games.Count == 0)
                return new SyncResult(false, 0, 0, 0, "MANUAL", "El JSON no contiene partidos válidos.");

            var (matches, teams) = await UpsertGamesAsync(games, week, league);
            return new SyncResult(true, 1, matches, teams, "MANUAL", null);
        }
        catch (Exception ex)
        {
            _logger.LogError("[ESPN Manual] Error al parsear JSON: {0}", ex.Message);
            return new SyncResult(false, 0, 0, 0, "MANUAL", $"Error al parsear JSON: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // MÉTODOS DE FALLBACK PRIVADOS
    // ═══════════════════════════════════════════════════════════════════════════

    private async Task<(List<ParsedWeek> weeks, string? error)> TryCalendarEndpointAsync(
        Season season, League league, CancellationToken ct)
    {
        try
        {
            var (sport, leagueCode) = ResolveEspnPath(league);

            // ESPN únicamente provee el endpoint /weeks para fútbol americano (NFL/NCAA) y basketball.
            // Para fútbol soccer (Liga MX, Premier League, etc.), ESPN no expone /weeks y organiza los partidos
            // directamente por fechas en el Scoreboard (fallback L2).
            if (sport == "soccer")
            {
                return (new List<ParsedWeek>(), "En fútbol soccer ESPN no expone endpoint /weeks; pasando directamente a Scoreboard L2.");
            }

            var url = $"{SecondaryBase}/{sport}/leagues/{leagueCode}/seasons/{season.Year}/types/{season.SeasonType}/weeks?limit=50";

            var (json, _, httpCode, latencyMs) = await FetchWithFallbackAsync(url, ct);
            if (json == null) return (new List<ParsedWeek>(), $"HTTP {httpCode}");

            await LogHealthAsync(url, httpCode, true, null, latencyMs);
            var weeks = _parser.ParseCalendar(json);

            if (weeks.Count == 0)
            {
                // ESPN devuelve solo $ref por semana: se resuelve cada una para obtener sus fechas reales.
                foreach (var weekRef in _parser.ExtractCalendarRefs(json))
                {
                    var (weekJson, _, _, _) = await FetchWithFallbackAsync(weekRef.Replace("http://", "https://"), ct);
                    var week = weekJson == null ? null : _parser.ParseCalendarWeek(weekJson);
                    if (week == null) continue;
                    week.Label = $"Jornada {week.WeekNumber}";
                    weeks.Add(week);
                }
            }

            return (weeks, null);
        }
        catch (Exception ex)
        {
            return (new List<ParsedWeek>(), ex.Message);
        }
    }

    private async Task<(List<ParsedWeek> weeks, string? error)> TryScoreboardDateRangeAsync(
        Season season, League league, CancellationToken ct)
    {
        try
        {
            var (sport, leagueCode) = ResolveEspnPath(league);
            // dates={year} trae todos los partidos del año (e.g. Clausura + Apertura de Liga MX).
            // Para el resto de ligas de fútbol se acota a la ventana real de la temporada que publica ESPN
            // (p. ej. Champions 2026-07 → 2027-07); con dates={year} se mezclaban dos temporadas.
            // (ESPN responde 400 a rangos "yyyyMMdd-yyyyMMdd", por eso se piden los años y se filtra en memoria.)
            var years = new List<int> { season.Year };
            (DateTime start, DateTime end)? window = null;
            var seasonNameForWindow = season.Name.ToLowerInvariant();
            var usesWindow = (sport == "soccer" && !seasonNameForWindow.Contains("clausura") && !seasonNameForWindow.Contains("apertura"))
                          || sport == "hockey";
            if (usesWindow)
            {
                window = await ResolveSeasonWindowAsync(season, sport, leagueCode, ct);
                if (window.HasValue)
                    years = new[] { window.Value.start.Year, window.Value.end.Year }.Distinct().ToList();
            }

            var games = new List<ParsedGame>();
            var lastHttp = 0;
            foreach (var year in years)
            {
                var url = $"{PrimaryBase}/{sport}/{leagueCode}/scoreboard?dates={year}&limit=1000";
                var (json, _, httpCode, latencyMs) = await FetchWithFallbackAsync(url, ct);
                lastHttp = httpCode;
                if (json == null) continue;

                await LogHealthAsync(url, httpCode, true, "Fallback L2 scoreboard date-range", latencyMs);
                var yearGames = _parser.ParseScoreboard(json);

                // ESPN no pagina: el tope duro es EspnEventLimit eventos. Si el año llega al tope, la respuesta está
                // truncada (y mezcla temporadas); se vuelve a pedir ese año mes por mes (dates=yyyyMM no se trunca).
                if (CountEvents(json) >= EspnEventLimit)
                {
                    _logger.LogWarning("[ESPN L2] dates={0} alcanzó el tope de {1} eventos; se pide mes por mes.", year, EspnEventLimit);
                    var monthly = new List<ParsedGame>();
                    for (var month = 1; month <= 12; month++)
                    {
                        var monthUrl = $"{PrimaryBase}/{sport}/{leagueCode}/scoreboard?dates={year}{month:00}&limit=1000";
                        var (monthJson, _, monthHttp, monthLatency) = await FetchWithFallbackAsync(monthUrl, ct);
                        if (monthJson == null) continue;

                        await LogHealthAsync(monthUrl, monthHttp, true, "Fallback L2 scoreboard por mes (año truncado)", monthLatency);
                        monthly.AddRange(_parser.ParseScoreboard(monthJson));
                    }

                    if (monthly.Count > 0)
                        yearGames = monthly.GroupBy(g => g.EspnGameId).Select(g => g.First()).ToList();
                }

                games.AddRange(yearGames);
            }

            if (window.HasValue)
            {
                var (ws, we) = window.Value;
                games = games
                    .Where(g => g.DateUtc >= ws && g.DateUtc < we.AddDays(1))
                    .GroupBy(g => g.EspnGameId).Select(g => g.First())
                    // Las eliminatorias (playoffs, octavos, final…) no son jornadas de temporada regular:
                    // se juegan con el módulo Pick'em, no con la quiniela por jornada.
                    .Where(g => !IsKnockoutPhase(g.SeasonSlug))
                    // La pretemporada tampoco cuenta para la quiniela por jornada.
                    .Where(g => !IsPreseason(g.SeasonSlug))
                    .ToList();
            }

            if (games.Count == 0)
                return (new List<ParsedWeek>(), lastHttp >= 400 || lastHttp == 0 ? $"HTTP {lastHttp}" : "Scoreboard vacío para el rango de fechas");

            // Filtrar por torneo si el nombre de la temporada lo indica (e.g. "Clausura" o "Apertura")
            var seasonNameLower = season.Name.ToLowerInvariant();
            if (seasonNameLower.Contains("clausura"))
            {
                var filtered = games.Where(g => g.SeasonSlug == "torneo-clausura" || (g.DateUtc.Year == season.Year && g.DateUtc.Month < 7)).ToList();
                if (filtered.Count > 0) games = filtered;
            }
            else if (seasonNameLower.Contains("apertura"))
            {
                var filtered = games.Where(g => g.SeasonSlug == "torneo-apertura" || (g.DateUtc.Year == season.Year && g.DateUtc.Month >= 7)).ToList();
                if (filtered.Count > 0) games = filtered;
            }

            var grouped = _parser.GroupByWeek(games);
            var weeks = grouped
                .OrderBy(kv => kv.Key)
                .Select(kv => new ParsedWeek
                {
                    WeekNumber = kv.Key,
                    StartDate  = kv.Value.Min(g => g.DateUtc).Date,
                    EndDate    = kv.Value.Max(g => g.DateUtc).Date.AddDays(1),
                    Label      = $"Jornada {kv.Key}",
                    Games      = kv.Value
                })
                .ToList();

            return (weeks, null);
        }
        catch (Exception ex)
        {
            return (new List<ParsedWeek>(), ex.Message);
        }
    }

    /// <summary>True si el slug de fase de ESPN (season.slug) es de pretemporada ("preseason", "pre-season").</summary>
    private static bool IsPreseason(string? slug) =>
        !string.IsNullOrWhiteSpace(slug) && slug.Replace("-", "").Equals("preseason", StringComparison.OrdinalIgnoreCase);

    /// <summary>Cantidad de eventos del arreglo "events" de un scoreboard (0 si el JSON no lo trae).</summary>
    private static int CountEvents(string json)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("events", out var ev) && ev.ValueKind == System.Text.Json.JsonValueKind.Array
            ? ev.GetArrayLength()
            : 0;
    }

    /// <summary>
    /// True si el slug de fase de ESPN (season.slug) corresponde a una fase eliminatoria o previa
    /// (p. ej. "knockout-round-playoffs", "round-of-16", "quarterfinals", "final"). "league-phase" y
    /// "regular-season" no lo son.
    /// </summary>
    public static bool IsKnockoutPhase(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return false;
        var s = slug.ToLowerInvariant();
        return s.Contains("knockout") || s.Contains("playoff") || s.Contains("play-off")
            || s.Contains("post-season") || s.Contains("postseason")
            || s.Contains("round-of") || s.Contains("quarter") || s.Contains("semi")
            || s.Contains("final") || s.Contains("preliminary") || s.Contains("qualif");
    }

    /// <summary>
    /// Ventana (inicio, fin) de la temporada vigente según ESPN (leagues[0].season del scoreboard sin fechas).
    /// Si la temporada aún no la tiene, la guarda en BD y la renombra cuando el nombre es el automático.
    /// Devuelve null si no se puede determinar (se usa entonces el año calendario).
    /// </summary>
    private async Task<(DateTime start, DateTime end)?> ResolveSeasonWindowAsync(
        Season season, string sport, string leagueCode, CancellationToken ct)
    {
        if (season.StartDate.HasValue && season.EndDate.HasValue)
            return (season.StartDate.Value.Date, season.EndDate.Value.Date);

        // Solo la temporada vigente puede tomar la ventana "actual" de ESPN.
        if (!season.IsCurrent) return null;

        try
        {
            var (json, _, _, _) = await FetchWithFallbackAsync($"{PrimaryBase}/{sport}/{leagueCode}/scoreboard", ct);
            if (json == null) return null;

            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("leagues", out var leagues) || leagues.GetArrayLength() == 0) return null;
            if (!leagues[0].TryGetProperty("season", out var s)) return null;
            if (!s.TryGetProperty("startDate", out var sd) || !s.TryGetProperty("endDate", out var ed)) return null;

            var start = DateTime.Parse(sd.GetString()!, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal);
            var end = DateTime.Parse(ed.GetString()!, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal);
            if (end <= start) return null;

            var autoName = $"Temporada {season.Year}";
            season.StartDate = start;
            season.EndDate = end;
            if (season.Name == autoName && start.Year != end.Year)
                season.Name = $"Temporada {start.Year}-{end.Year % 100:00}";
            await _unitOfWork.Seasons.UpdateAsync(season);

            return (start.Date, end.Date);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[ESPN L2] No se pudo leer la ventana de temporada: {0}", ex.Message);
            return null;
        }
    }

    private static List<ParsedWeek> GenerateWeeksFromConfig(Season season, League league)
    {
        var weeks     = new List<ParsedWeek>();
        var startDate = season.StartDate ?? DateTime.UtcNow;

        for (var i = 1; i <= league.WeeksCount; i++)
        {
            var weekStart = startDate.AddDays((i - 1) * league.WeekDurationDays);
            weeks.Add(new ParsedWeek
            {
                WeekNumber = i,
                StartDate  = weekStart,
                EndDate    = weekStart.AddDays(league.WeekDurationDays),
                Label      = $"Jornada {i}"
            });
        }

        return weeks;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // FETCH MATCHES FOR A SINGLE WEEK
    // ═══════════════════════════════════════════════════════════════════════════

    private async Task<List<ParsedGame>> FetchGamesForWeekAsync(
        Week week, string sport, string leagueCode, CancellationToken ct)
    {
        if (sport == "football")
        {
            var url = $"{PrimaryBase}/football/{leagueCode}/scoreboard?week={week.WeekNumber}&seasontype=2";
            var (json, endpointUsed, httpCode, latencyMs) = await FetchWithFallbackAsync(url, ct);
            if (json != null)
            {
                await LogHealthAsync(endpointUsed, httpCode, true, null, latencyMs);
                return _parser.ParseScoreboard(json);
            }
            await LogHealthAsync(url, httpCode, false, $"Sin datos para semana {week.WeekNumber}");
            return new List<ParsedGame>();
        }

        // Para Soccer: consultar por mes(es) de la jornada (formato YYYYMM)
        var m1 = week.StartDate.ToString("yyyyMM");
        var m2 = week.EndDate.ToString("yyyyMM");
        var months = m1 == m2 ? new[] { m1 } : new[] { m1, m2 };

        var allGames = new List<ParsedGame>();
        foreach (var m in months)
        {
            var url = $"{PrimaryBase}/{sport}/{leagueCode}/scoreboard?dates={m}&limit=100";
            var (json, endpointUsed, httpCode, latencyMs) = await FetchWithFallbackAsync(url, ct);
            if (json != null)
            {
                await LogHealthAsync(endpointUsed, httpCode, true, null, latencyMs);
                allGames.AddRange(_parser.ParseScoreboard(json));
            }
        }

        var minDate = week.StartDate.AddHours(-12);
        var maxDate = week.EndDate.AddHours(24);
        return allGames
            .Where(g => g.DateUtc >= minDate && g.DateUtc <= maxDate)
            .GroupBy(g => g.EspnGameId)
            .Select(g => g.First())
            .ToList();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // UPSERT HELPERS
    // ═══════════════════════════════════════════════════════════════════════════

    private async Task<SyncResult> UpsertWeeksAndSyncMatchesAsync(
        Season season, League league, List<ParsedWeek> parsedWeeks, string fallbackLevel, CancellationToken ct)
    {
        var totalWeeks   = 0;
        var totalMatches = 0;
        var totalTeams   = 0;

        var existingWeeks = (await _unitOfWork.Weeks.GetBySeasonIdAsync(season.Id)).ToList();
        var (sport, leagueCode) = ResolveEspnPath(league);

        // Limpiar semanas sobrantes fuera del rango esperado (ej. semanas creadas previamente por ISO week)
        if (parsedWeeks.Count > 0)
        {
            var maxParsedWeek = parsedWeeks.Max(p => p.WeekNumber);
            var excessWeeks = existingWeeks.Where(w => w.WeekNumber > maxParsedWeek).ToList();
            foreach (var excess in excessWeeks)
            {
                await _unitOfWork.Weeks.DeleteAsync(excess.Id);
            }
        }

        foreach (var pw in parsedWeeks)
        {
            var existing = existingWeeks.FirstOrDefault(w => w.WeekNumber == pw.WeekNumber);
            Week week;

            if (existing == null)
            {
                week = new Week
                {
                    SeasonId    = season.Id,
                    WeekNumber  = pw.WeekNumber,
                    Name        = pw.Label,
                    StartDate   = pw.StartDate,
                    EndDate     = pw.EndDate,
                    Status      = "DRAFT",
                    Active      = true,
                    Created     = DateTime.UtcNow
                };
                await _unitOfWork.Weeks.InsertAsync(week);
                await _unitOfWork.Save(ct);
                totalWeeks++;
            }
            else
            {
                week = existing;
                var changed = false;
                if (existing.StartDate != pw.StartDate || existing.EndDate != pw.EndDate)
                {
                    existing.StartDate = pw.StartDate;
                    existing.EndDate   = pw.EndDate;
                    changed = true;
                }
                if (!string.IsNullOrEmpty(pw.Label) && pw.Label != existing.Name)
                {
                    existing.Name = pw.Label;
                    changed = true;
                }
                if (changed)
                {
                    await _unitOfWork.Weeks.UpdateAsync(existing);
                    await _unitOfWork.Save(ct);
                }
            }

            // Sync partidos de esta semana:
            // Si pw.Games ya tiene los partidos (vienen de L2 en memoria), usarlos directamente
            List<ParsedGame> games;
            if (pw.Games.Count > 0)
            {
                games = pw.Games;
            }
            else
            {
                games = await FetchGamesForWeekAsync(week, sport, leagueCode, ct);
            }

            if (games.Count > 0)
            {
                var (m, t) = await UpsertGamesAsync(games, week, league);
                totalMatches += m;
                totalTeams   += t;
            }
        }

        return new SyncResult(true, totalWeeks, totalMatches, totalTeams, fallbackLevel, null);
    }

    private async Task<(int weeksSynced, int matchesUpserted, int teamsUpserted)> UpsertWeekStubsAsync(
        Season season, List<ParsedWeek> parsedWeeks, CancellationToken ct)
    {
        var total         = 0;
        var existingWeeks = (await _unitOfWork.Weeks.GetBySeasonIdAsync(season.Id)).ToList();

        foreach (var pw in parsedWeeks.Where(pw => !existingWeeks.Any(w => w.WeekNumber == pw.WeekNumber)))
        {
            await _unitOfWork.Weeks.InsertAsync(new Week
            {
                SeasonId   = season.Id,
                WeekNumber = pw.WeekNumber,
                Name       = pw.Label,
                StartDate  = pw.StartDate,
                EndDate    = pw.EndDate,
                Status     = "DRAFT",
                Active     = true,
                Created    = DateTime.UtcNow
            });
            total++;
        }

        await _unitOfWork.Save(ct);
        return (total, 0, 0);
    }

    private async Task<(int matchesUpserted, int teamsUpserted)> UpsertGamesAsync(
        List<ParsedGame> games, Week week, League league)
    {
        var matchesUpserted = 0;
        var teamsUpserted   = 0;
        var isLigaMx        = BroadcastChannelCatalog.IsLigaMx(league);

        foreach (var game in games)
        {
            var homeTeam = await UpsertTeamAsync(game.HomeTeam, league.Id);
            if (homeTeam == null) continue;
            if (homeTeam.Id == 0) teamsUpserted++;

            var awayTeam = await UpsertTeamAsync(game.AwayTeam, league.Id);
            if (awayTeam == null) continue;
            if (awayTeam.Id == 0) teamsUpserted++;

            await _unitOfWork.Save();

            var existing = await _unitOfWork.Matches.GetByEspnGameIdAsync(game.EspnGameId);

            if (existing == null)
            {
                await _unitOfWork.Matches.InsertAsync(new Match
                {
                    WeekId          = week.Id,
                    EspnGameId      = game.EspnGameId,
                    DateUtc         = game.DateUtc,
                    HomeTeamId      = homeTeam.Id,
                    AwayTeamId      = awayTeam.Id,
                    StatusState     = game.StatusState,
                    StatusDesc      = game.StatusDesc,
                    HomeScore       = game.HomeScore,
                    AwayScore       = game.AwayScore,
                    WinnerAbbr      = game.WinnerAbbr,
                    PostponedToDate = game.PostponedToDate,
                    Venue           = game.Venue,
                    City            = game.City,
                    LastSyncUtc     = DateTime.UtcNow,
                    // SPEC-015: canales por localía al crear el partido
                    Broadcasters       = isLigaMx ? DefaultBroadcastersFor(homeTeam) : null,
                    BroadcastersSource = isLigaMx ? BroadcastChannelCatalog.SourceRule : null,
                    Active          = true,
                    Created         = DateTime.UtcNow
                });
                matchesUpserted++;
            }
            else
            {
                existing.WeekId          = week.Id;
                existing.DateUtc         = game.DateUtc;
                existing.StatusState     = game.StatusState;
                existing.StatusDesc      = game.StatusDesc;
                existing.PostponedToDate = game.PostponedToDate;
                existing.Venue           = game.Venue;
                existing.City            = game.City;
                existing.LastSyncUtc     = DateTime.UtcNow;

                // SPEC-015: completar canales faltantes sin pisar señales oficiales ni manuales
                if (isLigaMx && existing.Broadcasters == null)
                {
                    existing.Broadcasters       = DefaultBroadcastersFor(homeTeam);
                    existing.BroadcastersSource = BroadcastChannelCatalog.SourceRule;
                }

                // REGLA CRÍTICA: si está pospuesto no tocar scores ni WinnerAbbr
                if (game.StatusState != "postponed")
                {
                    existing.HomeScore  = game.HomeScore;
                    existing.AwayScore  = game.AwayScore;
                    existing.WinnerAbbr = game.WinnerAbbr;
                }

                await _unitOfWork.Matches.UpdateAsync(existing);
            }
        }

        if (!week.FirstGameUtc.HasValue && games.Count > 0)
        {
            week.FirstGameUtc = games.Min(g => g.DateUtc);
            await _unitOfWork.Weeks.UpdateAsync(week);
        }

        await _unitOfWork.Save();
        return (matchesUpserted, teamsUpserted);
    }

    private string DefaultBroadcastersFor(Team homeTeam) =>
        BroadcastChannelCatalog.Serialize(_broadcastRules.Resolve(homeTeam.Abbreviation));

    private async Task<Team?> UpsertTeamAsync(ParsedTeam pt, int leagueId)
    {
        if (string.IsNullOrEmpty(pt.EspnTeamId)) return null;

        var existing = await _unitOfWork.Teams.GetByEspnIdAsync(leagueId, pt.EspnTeamId);
        if (existing != null)
        {
            if (existing.LogoUrl != pt.LogoUrl || existing.PrimaryColor != pt.PrimaryColor)
            {
                existing.LogoUrl      = pt.LogoUrl;
                existing.PrimaryColor = pt.PrimaryColor;
                await _unitOfWork.Teams.UpdateAsync(existing);
            }
            return existing;
        }

        var newTeam = new Team
        {
            LeagueId     = leagueId,
            EspnTeamId   = pt.EspnTeamId,
            Name         = pt.Name,
            Abbreviation = pt.Abbreviation,
            DisplayName  = pt.DisplayName,
            LogoUrl      = pt.LogoUrl,
            PrimaryColor = pt.PrimaryColor,
            Active       = true,
            Created      = DateTime.UtcNow
        };

        await _unitOfWork.Teams.InsertAsync(newTeam);
        return newTeam;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // HTTP CON FALLBACK  primario → secundario (solo ante 403)
    // ═══════════════════════════════════════════════════════════════════════════

    private async Task<(string? json, string endpoint, int httpCode, int latencyMs)> FetchWithFallbackAsync(
        string primaryUrl, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("EspnClient");
        var sw     = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var response = await client.GetAsync(primaryUrl, ct);
            sw.Stop();

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(ct);
                return (json, primaryUrl, (int)response.StatusCode, (int)sw.ElapsedMilliseconds);
            }

            if ((int)response.StatusCode == 403)
            {
                _logger.LogWarning("[ESPN] Primario 403. Activando secundario para: {0}", primaryUrl);

                var secondaryUrl = primaryUrl
                    .Replace("https://site.web.api.espn.com/apis/site/v2/sports",
                             "https://site.api.espn.com/apis/site/v2/sports");

                sw.Restart();
                var secondaryResponse = await client.GetAsync(secondaryUrl, ct);
                sw.Stop();

                if (secondaryResponse.IsSuccessStatusCode)
                {
                    var json2 = await secondaryResponse.Content.ReadAsStringAsync(ct);
                    return (json2, secondaryUrl, (int)secondaryResponse.StatusCode, (int)sw.ElapsedMilliseconds);
                }

                return (null, secondaryUrl, (int)secondaryResponse.StatusCode, (int)sw.ElapsedMilliseconds);
            }

            return (null, primaryUrl, (int)response.StatusCode, (int)sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError("[ESPN] Error de red: {0}", ex.Message);
            return (null, primaryUrl, 0, (int)sw.ElapsedMilliseconds);
        }
    }

    private async Task LogHealthAsync(
        string endpoint, int httpCode, bool isSuccess, string? errorMessage, int latencyMs = 0)
    {
        var truncated = endpoint.Length > 250 ? endpoint[..250] : endpoint;
        await _unitOfWork.EspnHealthLogs.InsertAsync(new EspnHealthLog
        {
            EndpointTested  = truncated,
            HttpStatusCode  = httpCode,
            IsSuccess       = isSuccess,
            ErrorMessage    = errorMessage,
            LatencyMs       = latencyMs,
            CheckedAt       = DateTime.UtcNow,
            Active          = true
        });
        await _unitOfWork.Save();
    }
}
