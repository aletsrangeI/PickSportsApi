using System.Text.Json;

namespace UseCases.Espn;

/// <summary>
/// Parsea respuestas JSON del endpoint scoreboard de ESPN y del endpoint de calendario.
/// Mapea estados ESPN a los estados del dominio: "pre" | "in" | "post" | "postponed".
/// </summary>
public class EspnScoreboardParser
{
    // ─── Scoreboard ───────────────────────────────────────────────────────────

    /// <summary>Parsea el JSON del scoreboard ESPN y retorna lista de ParsedGame.</summary>
    public List<ParsedGame> ParseScoreboard(string json)
    {
        var result = new List<ParsedGame>();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("events", out var events))
            return result;

        foreach (var ev in events.EnumerateArray())
        {
            try
            {
                var game = ParseEvent(ev);
                if (game != null) result.Add(game);
            }
            catch { /* ignorar partidos malformados */ }
        }

        return result;
    }

    /// <summary>
    /// Agrupa una lista de ParsedGame por número de semana/jornada.
    /// Si ESPN provee week.number (e.g. NFL), lo usa directamente.
    /// Si no (e.g. fútbol), agrupa por semana calendario ISO-8601 y mapea secuencialmente a Jornada 1..N.
    /// </summary>
    public Dictionary<int, List<ParsedGame>> GroupByWeek(List<ParsedGame> games)
    {
        var grouped = new Dictionary<int, List<ParsedGame>>();
        var sorted = games.OrderBy(g => g.DateUtc).ToList();

        if (sorted.Any(g => g.EspnWeekNumber > 0))
        {
            foreach (var g in sorted)
            {
                var w = g.EspnWeekNumber > 0 ? g.EspnWeekNumber : 1;
                if (!grouped.ContainsKey(w))
                    grouped[w] = new List<ParsedGame>();
                grouped[w].Add(g);
            }
            return grouped;
        }

        var isoGrouped = new SortedDictionary<int, List<ParsedGame>>();
        foreach (var g in sorted)
        {
            var isoWeek = GetIso8601WeekOfYear(g.DateUtc);
            if (!isoGrouped.ContainsKey(isoWeek))
                isoGrouped[isoWeek] = new List<ParsedGame>();
            isoGrouped[isoWeek].Add(g);
        }

        var weekIndex = 1;
        foreach (var kvp in isoGrouped)
        {
            grouped[weekIndex] = kvp.Value;
            weekIndex++;
        }

        return grouped;
    }

    // ─── Calendar ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Parsea el JSON del endpoint de calendario ESPN
    /// (sports.core.api.espn.com/.../seasons/{year}/types/{n}/weeks).
    /// </summary>
    public List<ParsedWeek> ParseCalendar(string json)
    {
        var result = new List<ParsedWeek>();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // El endpoint puede devolver { "items": [...] } o { "weeks": [...] }
        JsonElement items;
        if (!root.TryGetProperty("items", out items) && !root.TryGetProperty("weeks", out items))
            return result;

        foreach (var item in items.EnumerateArray())
        {
            try
            {
                var weekNum = item.TryGetProperty("number", out var num)   ? num.GetInt32()  :
                              item.TryGetProperty("weekNumber", out var wn) ? wn.GetInt32()  : 0;
                if (weekNum <= 0) continue;

                var start = item.TryGetProperty("startDate", out var sd) ? sd.GetDateTime() : DateTime.UtcNow;
                var end   = item.TryGetProperty("endDate",   out var ed) ? ed.GetDateTime() : start.AddDays(7);
                var label = item.TryGetProperty("text",  out var lbl)  ? lbl.GetString() :
                            item.TryGetProperty("label", out var lbl2) ? lbl2.GetString() : null;

                result.Add(new ParsedWeek
                {
                    WeekNumber = weekNum,
                    StartDate  = start,
                    EndDate    = end,
                    Label      = label ?? $"Jornada {weekNum}"
                });
            }
            catch { }
        }

        return result;
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    private static ParsedGame? ParseEvent(JsonElement ev)
    {
        var espnGameId = ev.TryGetProperty("id", out var gid) ? gid.GetString() ?? "" : "";
        if (string.IsNullOrEmpty(espnGameId)) return null;

        // Semana ESPN (opcional)
        var espnWeekNumber = 0;
        if (ev.TryGetProperty("week", out var weekEl) && weekEl.TryGetProperty("number", out var wnum))
            espnWeekNumber = wnum.GetInt32();

        if (espnWeekNumber == 0)
            espnWeekNumber = ResolveLigaMxJornada(espnGameId);

        // competitions[0]
        if (!ev.TryGetProperty("competitions", out var comps) || comps.GetArrayLength() == 0)
            return null;
        var comp = comps[0];

        var dateUtc = ev.TryGetProperty("date", out var dateEl) ? dateEl.GetDateTime() : DateTime.UtcNow;
        var (statusState, statusDesc, postponedToDate) = ParseStatus(comp);

        // Venue
        string? venue = null, city = null;
        if (comp.TryGetProperty("venue", out var venueEl))
        {
            venue = venueEl.TryGetProperty("fullName", out var vn) ? vn.GetString() : null;
            if (venueEl.TryGetProperty("address", out var addr))
                city = addr.TryGetProperty("city", out var ct) ? ct.GetString() : null;
        }

        // Competitors
        if (!comp.TryGetProperty("competitors", out var competitors) || competitors.GetArrayLength() < 2)
            return null;

        ParsedTeam? homeTeam = null, awayTeam = null;
        int homeScore = 0, awayScore = 0;
        string? winnerAbbr = null;

        foreach (var competitor in competitors.EnumerateArray())
        {
            var homeAway = competitor.TryGetProperty("homeAway", out var ha) ? ha.GetString() : "home";
            var scoreStr = competitor.TryGetProperty("score", out var sc) ? sc.GetString() : "0";
            var score    = int.TryParse(scoreStr, out var s) ? s : 0;
            var winner   = competitor.TryGetProperty("winner", out var w) && w.GetBoolean();
            var team     = ParseTeam(competitor);
            if (team == null) continue;

            if (homeAway == "home")
            {
                homeTeam  = team;
                homeScore = score;
                if (winner) winnerAbbr = team.Abbreviation;
            }
            else
            {
                awayTeam  = team;
                awayScore = score;
                if (winner) winnerAbbr = team.Abbreviation;
            }
        }

        if (homeTeam == null || awayTeam == null) return null;

        // Empate: ambos scores iguales en partido terminado y sin ganador marcado
        if (statusState == "post" && homeScore == awayScore && winnerAbbr == null)
            winnerAbbr = "EMPATE";

        // REGLA CRÍTICA: partido pospuesto NUNCA tiene ganador ni scores definitivos
        if (statusState == "postponed")
            winnerAbbr = null;

        string? seasonSlug = null;
        if (ev.TryGetProperty("season", out var seasonEl) && seasonEl.TryGetProperty("slug", out var slugEl))
        {
            seasonSlug = slugEl.GetString();
        }

        return new ParsedGame
        {
            EspnGameId      = espnGameId,
            EspnWeekNumber  = espnWeekNumber,
            DateUtc         = dateUtc,
            StatusState     = statusState,
            StatusDesc      = statusDesc,
            SeasonSlug      = seasonSlug,
            PostponedToDate = postponedToDate,
            HomeScore       = homeScore,
            AwayScore       = awayScore,
            WinnerAbbr      = winnerAbbr,
            Venue           = venue,
            City            = city,
            HomeTeam        = homeTeam,
            AwayTeam        = awayTeam
        };
    }

    private static (string state, string? desc, DateTime? postponedToDate) ParseStatus(JsonElement comp)
    {
        if (!comp.TryGetProperty("status", out var status))
            return ("pre", null, null);

        var typeName  = "";
        var typeState = "";
        var detail    = "";

        if (status.TryGetProperty("type", out var typeEl))
        {
            typeName  = typeEl.TryGetProperty("name",   out var tn)  ? tn.GetString()  ?? "" : "";
            typeState = typeEl.TryGetProperty("state",  out var ts)  ? ts.GetString()  ?? "" : "";
            detail    = typeEl.TryGetProperty("detail", out var det) ? det.GetString() ?? "" : "";
        }

        // Mapping explícito ESPN → dominio
        var domainState = typeName switch
        {
            "STATUS_SCHEDULED"    => "pre",
            "STATUS_IN_PROGRESS"  => "in",
            "STATUS_HALFTIME"     => "in",
            "STATUS_FINAL"        => "post",
            "STATUS_FULL_TIME"    => "post",
            "STATUS_POSTPONED"    => "postponed",
            "STATUS_CANCELED"     => "postponed",  // cancelado = pospuesto en el dominio
            "STATUS_SUSPENDED"    => "postponed",
            _                     => typeState == "in" ? "in" : typeState == "post" ? "post" : "pre"
        };

        // Intentar extraer fecha de reagendamiento del campo detail
        DateTime? postponedToDate = null;
        if (domainState == "postponed" && !string.IsNullOrEmpty(detail))
        {
            if (DateTime.TryParse(detail, out var parsedDate))
                postponedToDate = parsedDate;
        }

        return (domainState, string.IsNullOrEmpty(detail) ? null : detail, postponedToDate);
    }

    private static ParsedTeam? ParseTeam(JsonElement competitor)
    {
        if (!competitor.TryGetProperty("team", out var teamEl)) return null;

        var espnId = teamEl.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "";
        if (string.IsNullOrEmpty(espnId)) return null;

        // Logo: logos[0].href o campo logo directo
        string? logoUrl = null;
        if (teamEl.TryGetProperty("logos", out var logos) && logos.GetArrayLength() > 0)
            logoUrl = logos[0].TryGetProperty("href", out var lh) ? lh.GetString() : null;
        else if (teamEl.TryGetProperty("logo", out var logo))
            logoUrl = logo.GetString();

        string? primaryColor = null;
        if (teamEl.TryGetProperty("color", out var color))
            primaryColor = "#" + color.GetString();

        return new ParsedTeam
        {
            EspnTeamId  = espnId,
            Name        = teamEl.TryGetProperty("name",         out var name) ? name.GetString() ?? "" : "",
            Abbreviation = teamEl.TryGetProperty("abbreviation", out var abbr) ? abbr.GetString() ?? "" : "",
            DisplayName  = teamEl.TryGetProperty("displayName",  out var dn)   ? dn.GetString()   ?? ""
                         : teamEl.TryGetProperty("shortDisplayName", out var sdn) ? sdn.GetString() ?? "" : "",
            LogoUrl      = logoUrl,
            PrimaryColor = primaryColor
        };
    }

    private static int GetIso8601WeekOfYear(DateTime date)
    {
        var day = (int)date.DayOfWeek;
        if (day == 0) day = 7; // Domingo → 7
        var monday = date.AddDays(1 - day);
        var jan1   = new DateTime(monday.Year, 1, 1);
        return (int)Math.Ceiling((monday - jan1).TotalDays / 7) + 1;
    }

    /// <summary>
    /// Resuelve de forma determinística el número de Jornada oficial de la Liga MX (1..17)
    /// a partir de los IDs de partido de ESPN, garantizando que partidos reprogramados o pospuestos
    /// pertenezcan a su Jornada oficial sin desfasar el resto del calendario.
    /// </summary>
    public static int ResolveLigaMxJornada(string gameId)
    {
        if (int.TryParse(gameId, out var id))
        {
            // Apertura 2026: 153 partidos consecutivos (401876902 a 401877054)
            // Jornada 17 está en el rango superior 401877046..401877054
            if (id >= 401877046 && id <= 401877054) return 17;
            // Jornadas 1 a 16 están en orden inverso en bloques de 9 partidos
            if (id >= 401876902 && id <= 401877045) return (401877045 - id) / 9 + 1;

            // Clausura 2026: 153 partidos consecutivos (401840815 a 401840967)
            if (id >= 401840815 && id <= 401840967) return (id - 401840815) / 9 + 1;
        }
        return 0;
    }
}

// ─── Modelos intermedios ──────────────────────────────────────────────────────

public class ParsedGame
{
    public string EspnGameId     { get; set; } = null!;
    public int    EspnWeekNumber { get; set; }
    public DateTime DateUtc      { get; set; }
    public string StatusState    { get; set; } = "pre";
    public string? StatusDesc    { get; set; }
    public string? SeasonSlug    { get; set; }
    public DateTime? PostponedToDate { get; set; }
    public int HomeScore         { get; set; }
    public int AwayScore         { get; set; }
    public string? WinnerAbbr    { get; set; }
    public string? Venue         { get; set; }
    public string? City          { get; set; }
    public ParsedTeam HomeTeam   { get; set; } = null!;
    public ParsedTeam AwayTeam   { get; set; } = null!;
}

public class ParsedTeam
{
    public string EspnTeamId   { get; set; } = null!;
    public string Name         { get; set; } = null!;
    public string Abbreviation { get; set; } = null!;
    public string DisplayName  { get; set; } = null!;
    public string? LogoUrl     { get; set; }
    public string? PrimaryColor { get; set; }
}

public class ParsedWeek
{
    public int WeekNumber   { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate   { get; set; }
    public string Label     { get; set; } = null!;
    public List<ParsedGame> Games { get; set; } = new();
}
