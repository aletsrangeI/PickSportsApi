using Domain.Common;
using Interface.UseCases.Knockout;

namespace UseCases.Liguilla.Simulation;

/// <summary>
/// Simulacion QA del campo NFL (siembra 1..7 por conferencia) a partir de los standings de HOY de ESPN.
/// Arma los tres cruces de Wild Card de cada conferencia y el descanso del seed 1 en Divisional.
/// </summary>
public class NflSimulationBuilder : IKnockoutSimulationBuilder
{
    private const string NflStandingsPath = "apis/v2/sports/football/nfl/standings";
    private const string AfcConference = "afc";
    private const string NfcConference = "nfc";
    private const string WildCardRoundCode = "WC";
    private const string DivisionalRoundCode = "DIV";
    private const int FirstSeed = 1;
    private const int LastSeed = 7;
    private const int SeedsPerConference = 7;
    private const int AfcWildCardFirstNode = 1;
    private const int NfcWildCardFirstNode = 4;
    private const int AfcDivisionalNode = 7;
    private const int NfcDivisionalNode = 9;

    private static readonly (int HighSeed, int LowSeed)[] WildCardPairs =
    {
        (2, 7),
        (3, 6),
        (4, 5)
    };

    private static readonly ConferenceBracket[] Brackets =
    {
        new(AfcConference, AfcWildCardFirstNode, AfcDivisionalNode),
        new(NfcConference, NfcWildCardFirstNode, NfcDivisionalNode)
    };

    private static readonly IReadOnlyList<SimulatedTie> NoTies = Array.Empty<SimulatedTie>();

    private readonly IEspnKnockoutParser _parser;

    public NflSimulationBuilder(IEspnKnockoutParser parser)
    {
        _parser = parser;
    }

    public string FormatCode => KnockoutFormatCodes.NflReseededSingleGame;

    public string StandingsPath(string espnApiPath) => NflStandingsPath;

    public IReadOnlyList<SimulatedTie> Build(string standingsJson)
    {
        if (string.IsNullOrWhiteSpace(standingsJson)) return NoTies;

        var seeds = _parser.ParseNflPlayoffSeeds(standingsJson);
        var ties = new List<SimulatedTie>(Brackets.Length * (WildCardPairs.Length + 1));
        var scheduledTeamIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var bracket in Brackets)
        {
            if (!TryReadConferenceSeeds(seeds, bracket.Conference, out var seedsByRank)) return NoTies;

            var nodeNumber = bracket.WildCardFirstNode;
            foreach (var pair in WildCardPairs)
            {
                var highSeedTeamId = seedsByRank[pair.HighSeed];
                var lowSeedTeamId = seedsByRank[pair.LowSeed];
                if (!scheduledTeamIds.Add(highSeedTeamId) || !scheduledTeamIds.Add(lowSeedTeamId)) return NoTies;

                ties.Add(new SimulatedTie(
                    nodeNumber++, WildCardRoundCode, bracket.Conference,
                    highSeedTeamId, pair.HighSeed, lowSeedTeamId, pair.LowSeed));
            }

            var byeTeamId = seedsByRank[FirstSeed];
            if (!scheduledTeamIds.Add(byeTeamId)) return NoTies;

            ties.Add(new SimulatedTie(
                bracket.DivisionalNode, DivisionalRoundCode, bracket.Conference,
                byeTeamId, FirstSeed, null, null));
        }

        return ties.OrderBy(tie => tie.NodeNumber).ToList();
    }

    /// <summary>Siembras 1..7 completas y sin ranks ni equipos repetidos; cualquier hueco invalida toda la conferencia.</summary>
    private static bool TryReadConferenceSeeds(
        IReadOnlyDictionary<string, NflPlayoffSeed> seeds,
        string conference,
        out IReadOnlyDictionary<int, string> seedsByRank)
    {
        var result = new Dictionary<int, string>();
        foreach (var seed in seeds)
        {
            if (!string.Equals(seed.Value.Conference, conference, StringComparison.OrdinalIgnoreCase)) continue;
            if (seed.Value.Seed < FirstSeed || seed.Value.Seed > LastSeed) continue;
            if (!result.TryAdd(seed.Value.Seed, seed.Key))
            {
                seedsByRank = result;
                return false;
            }
        }

        seedsByRank = result;
        return result.Count == SeedsPerConference;
    }

    private sealed record ConferenceBracket(string Conference, int WildCardFirstNode, int DivisionalNode);
}
