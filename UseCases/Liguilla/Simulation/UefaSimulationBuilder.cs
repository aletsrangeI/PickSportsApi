using System.Text.Json;
using Domain.Common;
using Interface.UseCases.Knockout;

namespace UseCases.Liguilla.Simulation;

/// <summary>
/// Simulacion QA del campo Champions League: siembra 1..16 de la tabla de HOY de ESPN en octavos (1v16, 2v15, ...).
/// </summary>
public class UefaSimulationBuilder : IKnockoutSimulationBuilder
{
    private const string UefaStandingsPath = "apis/v2/sports/soccer/uefa.champions/standings";
    private const string RoundOfSixteenCode = "R16";
    private const string RankStatName = "rank";
    private const int FirstRank = 1;
    private const int LastRank = 16;
    private const int TieCount = 8;
    private const int RankPairSum = FirstRank + LastRank;

    private static readonly IReadOnlyList<SimulatedTie> NoTies = Array.Empty<SimulatedTie>();

    public string FormatCode => KnockoutFormatCodes.UefaTwoLeg;

    public string StandingsPath(string espnApiPath) => UefaStandingsPath;

    public IReadOnlyList<SimulatedTie> Build(string standingsJson)
    {
        if (string.IsNullOrWhiteSpace(standingsJson)) return NoTies;

        IReadOnlyDictionary<int, string> seedsByRank;
        try
        {
            using var document = JsonDocument.Parse(standingsJson);
            seedsByRank = ReadSeeds(document.RootElement);
        }
        catch (JsonException)
        {
            // JSON mal formado: se contrata como "sin equipos" para no romper la simulacion.
            return NoTies;
        }

        if (seedsByRank.Count != LastRank) return NoTies;

        var ties = new List<SimulatedTie>(TieCount);
        for (var nodeNumber = FirstRank; nodeNumber <= TieCount; nodeNumber++)
        {
            var oppositeRank = RankPairSum - nodeNumber;
            ties.Add(new SimulatedTie(
                nodeNumber, RoundOfSixteenCode, null,
                seedsByRank[nodeNumber], nodeNumber, seedsByRank[oppositeRank], oppositeRank));
        }

        return ties;
    }

    /// <summary>Siembras de <c>children[0].standings.entries[]</c>: <c>team.id</c> (texto o numero) y el stat <c>rank</c> (1..16).</summary>
    private static IReadOnlyDictionary<int, string> ReadSeeds(JsonElement root)
    {
        var seedsByRank = new Dictionary<int, string>();
        var entries = Prop(Prop(FirstChild(root), "standings"), "entries");
        foreach (var entry in Items(entries))
        {
            var teamId = Text(Prop(Prop(entry, "team"), "id"));
            var rank = ReadRank(entry);
            if (teamId == null || !rank.HasValue) continue;
            if (!seedsByRank.TryAdd(rank.Value, teamId)) return new Dictionary<int, string>();
        }

        return seedsByRank;
    }

    private static int? ReadRank(JsonElement entry)
    {
        foreach (var stat in Items(Prop(entry, "stats")))
        {
            if (Text(Prop(stat, "name")) != RankStatName) continue;

            var value = Prop(stat, "value");
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var raw)) return null;

            var rank = (int)raw;
            return rank == raw && rank >= FirstRank && rank <= LastRank ? rank : null;
        }

        return null;
    }

    private static JsonElement FirstChild(JsonElement element)
    {
        var children = Prop(element, "children");
        return children.ValueKind == JsonValueKind.Array && children.GetArrayLength() > 0 ? children[0] : default;
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
}
