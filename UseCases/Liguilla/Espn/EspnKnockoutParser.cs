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
    private const int MinPlayoffSeed = 1;
    private const int MaxPlayoffSeed = 7;

    private static readonly IReadOnlyList<EspnKnockoutTie> NoTies = Array.Empty<EspnKnockoutTie>();

    public IReadOnlyCollection<string> SupportedFormatCodes { get; } = new[]
    {
        KnockoutFormatCodes.NhlBestOf7, KnockoutFormatCodes.NflReseededSingleGame, KnockoutFormatCodes.UefaTwoLeg
    };

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
                    var seed = ReadPlayoffSeed(entry);
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

    private static string? NflConferenceOf(string? groupName)
    {
        if (groupName == null) return null;
        if (groupName.Contains("American", StringComparison.OrdinalIgnoreCase)) return "afc";
        if (groupName.Contains("National", StringComparison.OrdinalIgnoreCase)) return "nfc";
        return null;
    }

    private static int? ReadPlayoffSeed(JsonElement entry)
    {
        foreach (var stat in Items(Prop(entry, "stats")))
        {
            if (Text(Prop(stat, "name")) != PlayoffSeedStatName) continue;

            var value = Prop(stat, "value");
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var raw)) return null;

            var seed = (int)raw;
            return seed == raw && seed is >= MinPlayoffSeed and <= MaxPlayoffSeed ? seed : null;
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

        var tie = new EspnKnockoutTie
        {
            GroupKey = BuildGroupKey(slug, pair[0], pair[1]),
            RoundSlug = slug,
            Headline = headline,
            LegNumber = IntOf(Prop(Prop(competition, "leg"), "value")),
            TotalLegs = hasSeries ? IntOf(Prop(series, "totalCompetitions")) ?? 1 : 1,
            SeriesCompleted = completed,
            WinnerEspnTeamId = completed ? WinnerOf(hasSeries ? seriesCompetitors : competitors, hasSeries) : null,
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
        _ => false
    };

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
}
