using System.Globalization;
using System.Text.Json;
using Domain.Common;
using Interface.UseCases.Knockout;

namespace UseCases.Liguilla.Espn;

/// <summary>
/// Interpreta el scoreboard de ESPN para las fases finales de NHL, NFL y Champions League
/// y lo normaliza en <see cref="EspnKnockoutTie"/> (un cruce por serie o partido). Nunca lanza por JSON inesperado.
/// </summary>
public class EspnKnockoutParser : IEspnKnockoutParser
{
    private const string PostSeasonSlug = "post-season";
    private static readonly string[] UefaKnockoutSlugs =
    {
        "round-of-16", "quarterfinals", "semifinals", "final", "knockout-round-playoffs"
    };

    private const string PlayoffSeedStatName = "playoffSeed";
    private const string PointsStatName = "points";
    private const int MinPlayoffSeed = 1;
    private const int MaxNflPlayoffSeed = 7;
    private const int MaxNhlPlayoffSeed = 8;

    private const string EasternConferenceName = "Eastern Conference";
    private const string WesternConferenceName = "Western Conference";
    private const string EastConferenceCode = "east";
    private const string WestConferenceCode = "west";

    private const string AtlanticDivisionName = "Atlantic Division";
    private const string MetropolitanDivisionName = "Metropolitan Division";
    private const string CentralDivisionName = "Central Division";
    private const string PacificDivisionName = "Pacific Division";
    private const string AtlanticDivisionCode = "atlantic";
    private const string MetropolitanDivisionCode = "metropolitan";
    private const string CentralDivisionCode = "central";
    private const string PacificDivisionCode = "pacific";

    private static readonly IReadOnlyDictionary<string, string> NhlConferenceMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [EasternConferenceName] = EastConferenceCode,
            [WesternConferenceName] = WestConferenceCode
        };

    private static readonly IReadOnlyDictionary<string, string> NhlDivisionMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [AtlanticDivisionName] = AtlanticDivisionCode,
            [MetropolitanDivisionName] = MetropolitanDivisionCode,
            [CentralDivisionName] = CentralDivisionCode,
            [PacificDivisionName] = PacificDivisionCode
        };

    private static readonly IReadOnlyList<EspnKnockoutTie> NoTies = Array.Empty<EspnKnockoutTie>();

    public IReadOnlyCollection<string> SupportedFormatCodes { get; } = new[]
    {
        KnockoutFormatCodes.NhlBestOf7, KnockoutFormatCodes.NflReseededSingleGame, KnockoutFormatCodes.UefaTwoLeg,
        KnockoutFormatCodes.FmfLiguilla
    };

    private const string RankStatName = "rank";
    private const int MaxFmfPlayoffSeed = 8;
    private static readonly string[] FmfKnockoutSlugSuffixes = { "---quarterfinals", "---semifinals", "---finals" };

    public IReadOnlyDictionary<string, int> ParseFmfPlayoffSeeds(string standingsJson)
    {
        var seeds = new Dictionary<string, int>();
        if (string.IsNullOrWhiteSpace(standingsJson)) return seeds;

        try
        {
            using var document = JsonDocument.Parse(standingsJson);
            var entries = FmfGeneralTableEntries(document.RootElement);
            var ranked = new List<(string TeamId, int Rank)>();
            var position = 0;
            foreach (var entry in entries)
            {
                position++;
                var teamId = Text(Prop(Prop(entry, "team"), "id"));
                if (string.IsNullOrWhiteSpace(teamId)) continue;

                var rankStat = ReadStatNumber(entry, RankStatName);
                var rank = rankStat.HasValue && rankStat.Value == Math.Floor(rankStat.Value) ? (int)rankStat.Value : position;
                ranked.Add((teamId, rank));
            }

            foreach (var item in ranked.OrderBy(r => r.Rank))
            {
                if (item.Rank < MinPlayoffSeed || item.Rank > MaxFmfPlayoffSeed) continue;
                seeds.TryAdd(item.TeamId, item.Rank);
            }
        }
        catch (JsonException)
        {
            // JSON mal formado: sin tabla; el sync lo trata como "campo no listo".
            return new Dictionary<string, int>();
        }

        return seeds;
    }

    /// <summary>Tabla general de la liga: el primer grupo con entradas (ESPN publica un solo grupo para Liga MX) o, si no hay grupos, la tabla raiz.</summary>
    private static List<JsonElement> FmfGeneralTableEntries(JsonElement root)
    {
        foreach (var group in Items(Prop(root, "children")))
        {
            var entries = Items(Prop(Prop(group, "standings"), "entries")).ToList();
            if (entries.Count > 0) return entries;
        }

        return Items(Prop(Prop(root, "standings"), "entries")).ToList();
    }

    public IReadOnlyList<EspnKnockoutTie> ParseScoreboard(string json, string formatCode)
    {
        if (string.IsNullOrWhiteSpace(json) || !SupportedFormatCodes.Contains(formatCode))
            return NoTies;

        try
        {
            using var document = JsonDocument.Parse(json);
            return ParseEvents(document.RootElement, formatCode);
        }
        catch (JsonException)
        {
            // JSON mal formado: se contrata como "sin cruces" para que el sync no falle por una respuesta rota de ESPN.
            return NoTies;
        }
    }

    public IReadOnlyDictionary<string, NflPlayoffSeed> ParseNflPlayoffSeeds(string standingsJson)
    {
        var seeds = new Dictionary<string, NflPlayoffSeed>();
        if (string.IsNullOrWhiteSpace(standingsJson)) return seeds;

        try
        {
            using var document = JsonDocument.Parse(standingsJson);
            foreach (var group in Items(Prop(document.RootElement, "children")))
            {
                var conference = NflConferenceOf(Text(Prop(group, "name")));
                if (conference == null) continue;

                var entries = Prop(Prop(group, "standings"), "entries");
                foreach (var entry in Items(entries))
                {
                    var teamId = Text(Prop(Prop(entry, "team"), "id"));
                    var seed = ReadPlayoffSeed(entry, MaxNflPlayoffSeed);
                    if (teamId != null && seed.HasValue)
                        seeds.TryAdd(teamId, new NflPlayoffSeed(conference, seed.Value));
                }
            }
        }
        catch (JsonException)
        {
            // JSON mal formado: devolvemos lo que se alcanzó a leer (nada) sin interrumpir el sync.
            return new Dictionary<string, NflPlayoffSeed>();
        }

        return seeds;
    }

    public IReadOnlyDictionary<string, NhlPlayoffSeed> ParseNhlPlayoffSeeds(string standingsJson)
    {
        var seeds = new Dictionary<string, NhlPlayoffSeed>();
        if (string.IsNullOrWhiteSpace(standingsJson)) return seeds;

        try
        {
            using var document = JsonDocument.Parse(standingsJson);
            foreach (var conferenceNode in Items(Prop(document.RootElement, "children")))
            {
                var conference = NhlConferenceOf(Text(Prop(conferenceNode, "name")));
                if (conference == null) continue;

                foreach (var divisionNode in Items(Prop(conferenceNode, "children")))
                {
                    var division = NhlDivisionOf(Text(Prop(divisionNode, "name")));
                    if (division == null) continue;

                    var qualifiers = new List<NhlDivisionQualifier>();
                    var entries = Prop(Prop(divisionNode, "standings"), "entries");
                    foreach (var entry in Items(entries))
                    {
                        var teamId = Text(Prop(Prop(entry, "team"), "id"));
                        var seed = ReadPlayoffSeed(entry, MaxNhlPlayoffSeed);
                        if (string.IsNullOrWhiteSpace(teamId) || !seed.HasValue) continue;

                        var points = ReadStatNumber(entry, PointsStatName) ?? double.MinValue;
                        qualifiers.Add(new NhlDivisionQualifier(teamId, seed.Value, points));
                    }

                    if (qualifiers.Count == 0) continue;

                    var winnerTeamId = qualifiers
                        .OrderByDescending(q => q.Points)
                        .ThenBy(q => q.Seed)
                        .First()
                        .TeamId;

                    foreach (var qualifier in qualifiers)
                    {
                        seeds.TryAdd(
                            qualifier.TeamId,
                            new NhlPlayoffSeed(conference, division, qualifier.Seed, qualifier.TeamId == winnerTeamId));
                    }
                }
            }
        }
        catch (JsonException)
        {
            // JSON mal formado: devolvemos diccionario vacío sin interrumpir el sync.
            return new Dictionary<string, NhlPlayoffSeed>();
        }

        return seeds;
    }

    private static string? NflConferenceOf(string? groupName)
    {
        if (groupName == null) return null;
        if (groupName.Contains("American", StringComparison.OrdinalIgnoreCase)) return "afc";
        if (groupName.Contains("National", StringComparison.OrdinalIgnoreCase)) return "nfc";
        return null;
    }

    private static string? NhlConferenceOf(string? groupName) =>
        groupName != null && NhlConferenceMap.TryGetValue(groupName, out var code) ? code : null;

    private static string? NhlDivisionOf(string? groupName) =>
        groupName != null && NhlDivisionMap.TryGetValue(groupName, out var code) ? code : null;

    private static int? ReadPlayoffSeed(JsonElement entry, int maxSeed)
    {
        var raw = ReadStatNumber(entry, PlayoffSeedStatName);
        if (!raw.HasValue) return null;

        var seed = (int)raw.Value;
        return seed == raw.Value && seed >= MinPlayoffSeed && seed <= maxSeed ? seed : null;
    }

    private static double? ReadStatNumber(JsonElement entry, string statName)
    {
        foreach (var stat in Items(Prop(entry, "stats")))
        {
            if (Text(Prop(stat, "name")) != statName) continue;

            var value = Prop(stat, "value");
            return value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var raw) ? raw : null;
        }

        return null;
    }

    private static IReadOnlyList<EspnKnockoutTie> ParseEvents(JsonElement root, string formatCode)
    {
        var parsed = new List<ParsedEvent>();
        foreach (var espnEvent in Items(Prop(root, "events")))
        {
            var item = ParseEvent(espnEvent, formatCode);
            if (item != null) parsed.Add(item);
        }

        return parsed
            .GroupBy(p => p.Tie.GroupKey)
            .Select(MergeGroup)
            .OrderBy(t => t.KickoffUtc ?? DateTime.MaxValue)
            .ThenBy(t => t.GroupKey, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Fusiona los juegos de una serie (o las patas de un cruce) en un solo cruce.</summary>
    private static EspnKnockoutTie MergeGroup(IGrouping<string, ParsedEvent> group)
    {
        var ordered = group.OrderByDescending(p => p.Date ?? DateTime.MinValue).ToList();
        var basis = ordered.FirstOrDefault(p => p.Tie.SeriesCompleted) ?? ordered[0];
        var earliest = group.Where(p => p.Date.HasValue).Select(p => p.Date).Min();
        return basis.Tie with { KickoffUtc = earliest ?? basis.Tie.KickoffUtc };
    }

    private static ParsedEvent? ParseEvent(JsonElement espnEvent, string formatCode)
    {
        var slug = Text(Prop(Prop(espnEvent, "season"), "slug"));
        if (slug == null || !IsKnockoutSlug(formatCode, slug)) return null;

        var competitions = Prop(espnEvent, "competitions");
        if (competitions.ValueKind != JsonValueKind.Array || competitions.GetArrayLength() == 0) return null;
        var competition = competitions[0];

        var competitors = Items(Prop(competition, "competitors")).ToList();
        var homeId = TeamIdOf(competitors, "home");
        var awayId = TeamIdOf(competitors, "away");

        var series = Prop(competition, "series");
        var hasSeries = series.ValueKind == JsonValueKind.Object;
        var seriesCompetitors = hasSeries ? Items(Prop(series, "competitors")).ToList() : new List<JsonElement>();

        var pair = hasSeries && seriesCompetitors.Count >= 2
            ? seriesCompetitors.Select(c => Text(Prop(c, "id"))).Where(id => id != null).Select(id => id!).Take(2).ToList()
            : new[] { homeId, awayId }.Where(id => id != null).Select(id => id!).ToList();
        if (pair.Count < 2) return null;

        var headline = HeadlineOf(competition);
        var date = ParseDate(Text(Prop(espnEvent, "date")));
        var completed = hasSeries
            ? Prop(series, "completed").ValueKind == JsonValueKind.True
            : Prop(Prop(Prop(competition, "status"), "type"), "completed").ValueKind == JsonValueKind.True;

        var winnerId = completed ? WinnerOf(hasSeries ? seriesCompetitors : competitors, hasSeries) : null;
        if (winnerId == null && completed && hasSeries && formatCode == KnockoutFormatCodes.FmfLiguilla)
            winnerId = FmfWinnerWithoutFlag(seriesCompetitors, homeId);

        var tie = new EspnKnockoutTie
        {
            GroupKey = BuildGroupKey(slug, pair[0], pair[1]),
            RoundSlug = slug,
            Headline = headline,
            LegNumber = IntOf(Prop(Prop(competition, "leg"), "value")),
            TotalLegs = hasSeries ? IntOf(Prop(series, "totalCompetitions")) ?? 1 : 1,
            SeriesCompleted = completed,
            WinnerEspnTeamId = winnerId,
            Wins = hasSeries ? NumbersOf(seriesCompetitors, "wins", value => (int)value) : new Dictionary<string, int>(),
            AggregateScore = hasSeries ? NumbersOf(seriesCompetitors, "aggregateScore", value => value) : new Dictionary<string, double>(),
            HomeEspnTeamId = homeId,
            AwayEspnTeamId = awayId,
            KickoffUtc = date,
            Conference = ConferenceOf(formatCode, headline)
        };

        return new ParsedEvent(tie, date);
    }

    private static bool IsKnockoutSlug(string formatCode, string slug) => formatCode switch
    {
        KnockoutFormatCodes.NhlBestOf7 or KnockoutFormatCodes.NflReseededSingleGame => slug == PostSeasonSlug,
        KnockoutFormatCodes.UefaTwoLeg =>UefaKnockoutSlugs.Contains(slug),
        KnockoutFormatCodes.FmfLiguilla => FmfKnockoutSlugSuffixes.Any(s => slug.EndsWith(s, StringComparison.OrdinalIgnoreCase)),
        _ => false
    };

    /// <summary>
    /// Liga MX: ESPN no marca <c>winner</c> cuando el global queda empatado (medido en Clausura 2026, UNAM vs America: ambos
    /// <c>winner=false</c>, global 6-6 y goles de visitante 3-3) y su <c>notes[0].headline</c> es poco fiable ("AME win on away goals"
    /// aunque avanzo UNAM). Criterio: gana el de mayor global; si empatan, el local de la vuelta, que es el mejor sembrado
    /// (en la vuelta de los 6 cruces empatados de Clausura 2026 siempre avanzo el local de la vuelta). Se evalua sobre el evento
    /// de la vuelta, por eso <paramref name="homeId"/> es el local del evento que cierra la serie.
    /// </summary>
    private static string? FmfWinnerWithoutFlag(List<JsonElement> seriesCompetitors, string? homeId)
    {
        var aggregates = seriesCompetitors
            .Select(c => (Id: Text(Prop(c, "id")), Aggregate: Prop(c, "aggregateScore")))
            .Where(x => x.Id != null && x.Aggregate.ValueKind == JsonValueKind.Number)
            .Select(x => (x.Id, Value: x.Aggregate.GetDouble()))
            .OrderByDescending(x => x.Value)
            .ToList();
        if (aggregates.Count < 2) return null;
        if (aggregates[0].Value > aggregates[1].Value) return aggregates[0].Id;

        return homeId != null && aggregates.Any(a => a.Id == homeId) ? homeId : null;
    }

    private static string? ConferenceOf(string formatCode, string headline)
    {
        return formatCode switch
        {
            KnockoutFormatCodes.NhlBestOf7 when headline.StartsWith("East", StringComparison.OrdinalIgnoreCase) => "east",
            KnockoutFormatCodes.NhlBestOf7 when headline.StartsWith("West", StringComparison.OrdinalIgnoreCase) => "west",
            KnockoutFormatCodes.NflReseededSingleGame when headline.StartsWith("AFC", StringComparison.OrdinalIgnoreCase) => "afc",
            KnockoutFormatCodes.NflReseededSingleGame when headline.StartsWith("NFC", StringComparison.OrdinalIgnoreCase) => "nfc",
            _ => null
        };
    }

    private static string BuildGroupKey(string slug, string idA, string idB)
    {
        var (low, high) = string.CompareOrdinal(idA, idB) <= 0 ? (idA, idB) : (idB, idA);
        return $"{slug}:{low}-{high}";
    }

    /// <summary>Ganador firme: el competidor marcado <c>winner</c> o, en series, el unico con mas victorias.</summary>
    private static string? WinnerOf(List<JsonElement> competitors, bool fromSeries)
    {
        foreach (var competitor in competitors)
        {
            if (Prop(competitor, "winner").ValueKind != JsonValueKind.True) continue;
            return fromSeries ? Text(Prop(competitor, "id")) : Text(Prop(Prop(competitor, "team"), "id")) ?? Text(Prop(competitor, "id"));
        }

        if (!fromSeries) return null;

        var wins = competitors
            .Select(c => (Id: Text(Prop(c, "id")), Wins: IntOf(Prop(c, "wins"))))
            .Where(x => x.Id != null && x.Wins.HasValue)
            .OrderByDescending(x => x.Wins)
            .ToList();
        var leaderIsUnique = wins.Count > 0 && (wins.Count == 1 || wins[0].Wins > wins[1].Wins);
        return leaderIsUnique ? wins[0].Id : null;
    }

    private static Dictionary<string, T> NumbersOf<T>(List<JsonElement> competitors, string property, Func<double, T> convert)
    {
        var values = new Dictionary<string, T>();
        foreach (var competitor in competitors)
        {
            var id = Text(Prop(competitor, "id"));
            var number = Prop(competitor, property);
            if (id != null && number.ValueKind == JsonValueKind.Number && number.TryGetDouble(out var raw))
                values[id] = convert(raw);
        }

        return values;
    }

    private static string? TeamIdOf(List<JsonElement> competitors, string homeAway)
    {
        var competitor = competitors.FirstOrDefault(c => Text(Prop(c, "homeAway")) == homeAway);
        return competitor.ValueKind == JsonValueKind.Object
            ? Text(Prop(Prop(competitor, "team"), "id")) ?? Text(Prop(competitor, "id"))
            : null;
    }

    private static string HeadlineOf(JsonElement competition)
    {
        var notes = Prop(competition, "notes");
        return notes.ValueKind == JsonValueKind.Array && notes.GetArrayLength() > 0
            ? Text(Prop(notes[0], "headline")) ?? string.Empty
            : string.Empty;
    }

    private static DateTime? ParseDate(string? raw)
    {
        return DateTime.TryParse(raw, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : null;
    }

    private static JsonElement Prop(JsonElement element, string name)
    {
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value)
            ? value
            : default;
    }

    private static IEnumerable<JsonElement> Items(JsonElement element)
    {
        return element.ValueKind == JsonValueKind.Array ? element.EnumerateArray() : Enumerable.Empty<JsonElement>();
    }

    private static string? Text(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.GetRawText(),
        _ => null
    };

    private static int? IntOf(JsonElement element)
    {
        return element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var value) ? value : null;
    }

    private sealed record ParsedEvent(EspnKnockoutTie Tie, DateTime? Date);

    private sealed record NhlDivisionQualifier(string TeamId, int Seed, double Points);
}
