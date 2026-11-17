using Domain.Common;
using Interface.UseCases.Knockout;

namespace UseCases.Liguilla.Simulation;

/// <summary>
/// Construye los 8 cruces de primera ronda (<c>R1</c>) de la NHL a partir de los standings actuales de ESPN:
/// 3 clasificados por division mas 2 comodines por conferencia.
/// </summary>
public sealed class NhlSimulationBuilder : IKnockoutSimulationBuilder
{
    private const string NhlStandingsPath = "apis/v2/sports/hockey/nhl/standings?level=3";
    private const string FirstRoundCode = "R1";

    private const string EastConference = "east";
    private const string WestConference = "west";
    private const string AtlanticDivision = "atlantic";
    private const string MetropolitanDivision = "metropolitan";
    private const string CentralDivision = "central";
    private const string PacificDivision = "pacific";

    private const int DivisionQualifiersCount = 3;
    private const int DivisionNonWinnersCount = DivisionQualifiersCount - 1;
    private const int WildCardCount = 2;

    private const int EastAtlanticChampNode = 1;
    private const int EastAtlanticRunnerUpNode = 2;
    private const int EastMetropolitanChampNode = 3;
    private const int EastMetropolitanRunnerUpNode = 4;
    private const int WestCentralChampNode = 5;
    private const int WestCentralRunnerUpNode = 6;
    private const int WestPacificChampNode = 7;
    private const int WestPacificRunnerUpNode = 8;

    private readonly IEspnKnockoutParser _parser;

    public NhlSimulationBuilder(IEspnKnockoutParser parser)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    /// <inheritdoc />
    public string FormatCode => KnockoutFormatCodes.NhlBestOf7;

    /// <inheritdoc />
    public string StandingsPath(string espnApiPath) => NhlStandingsPath;

    /// <inheritdoc />
    public IReadOnlyList<SimulatedTie> Build(string standingsJson)
    {
        if (string.IsNullOrWhiteSpace(standingsJson))
        {
            return Array.Empty<SimulatedTie>();
        }

        var seeds = _parser.ParseNhlPlayoffSeeds(standingsJson);
        if (seeds.Count == 0)
        {
            return Array.Empty<SimulatedTie>();
        }

        var eastTies = BuildConferenceTies(
            seeds,
            EastConference,
            AtlanticDivision,
            MetropolitanDivision,
            EastAtlanticChampNode,
            EastAtlanticRunnerUpNode,
            EastMetropolitanChampNode,
            EastMetropolitanRunnerUpNode);

        if (eastTies is null)
        {
            return Array.Empty<SimulatedTie>();
        }

        var westTies = BuildConferenceTies(
            seeds,
            WestConference,
            CentralDivision,
            PacificDivision,
            WestCentralChampNode,
            WestCentralRunnerUpNode,
            WestPacificChampNode,
            WestPacificRunnerUpNode);

        if (westTies is null)
        {
            return Array.Empty<SimulatedTie>();
        }

        var result = new List<SimulatedTie>(eastTies.Count + westTies.Count);
        result.AddRange(eastTies);
        result.AddRange(westTies);
        return result;
    }

    private static List<SimulatedTie>? BuildConferenceTies(
        IReadOnlyDictionary<string, NhlPlayoffSeed> seeds,
        string conference,
        string firstDivisionCode,
        string secondDivisionCode,
        int firstChampNode,
        int firstRunnerUpNode,
        int secondChampNode,
        int secondRunnerUpNode)
    {
        var conferenceTeams = seeds
            .Where(kv => kv.Value.Conference == conference)
            .Select(kv => new SeededTeam(kv.Key, kv.Value))
            .ToList();

        var firstDivision = ExtractDivision(conferenceTeams, firstDivisionCode);
        var secondDivision = ExtractDivision(conferenceTeams, secondDivisionCode);
        if (firstDivision is null || secondDivision is null)
        {
            return null;
        }

        var assignedIds = new HashSet<string>(StringComparer.Ordinal)
        {
            firstDivision.Winner.EspnTeamId,
            firstDivision.Second.EspnTeamId,
            firstDivision.Third.EspnTeamId,
            secondDivision.Winner.EspnTeamId,
            secondDivision.Second.EspnTeamId,
            secondDivision.Third.EspnTeamId
        };

        var wildCards = conferenceTeams
            .Where(t => !assignedIds.Contains(t.EspnTeamId))
            .OrderBy(t => t.Info.Seed)
            .ThenBy(t => t.EspnTeamId, StringComparer.Ordinal)
            .Take(WildCardCount)
            .ToList();

        if (wildCards.Count < WildCardCount)
        {
            return null;
        }

        var bestWildCard = wildCards[0];
        var worstWildCard = wildCards[WildCardCount - 1];

        var firstWinnerIsBest = firstDivision.Winner.Info.Seed <= secondDivision.Winner.Info.Seed;
        var firstWildCard = firstWinnerIsBest ? worstWildCard : bestWildCard;
        var secondWildCard = firstWinnerIsBest ? bestWildCard : worstWildCard;

        return new List<SimulatedTie>
        {
            CreateTie(firstChampNode, conference, firstDivision.Winner, firstWildCard),
            CreateTie(firstRunnerUpNode, conference, firstDivision.Second, firstDivision.Third),
            CreateTie(secondChampNode, conference, secondDivision.Winner, secondWildCard),
            CreateTie(secondRunnerUpNode, conference, secondDivision.Second, secondDivision.Third)
        };
    }

    private static DivisionBracket? ExtractDivision(List<SeededTeam> conferenceTeams, string divisionCode)
    {
        var divisionTeams = conferenceTeams
            .Where(t => t.Info.Division == divisionCode)
            .ToList();

        var winners = divisionTeams.Where(t => t.Info.IsDivisionWinner).ToList();
        if (winners.Count != 1)
        {
            return null;
        }

        var runnersUp = divisionTeams
            .Where(t => !t.Info.IsDivisionWinner)
            .OrderBy(t => t.Info.Seed)
            .ThenBy(t => t.EspnTeamId, StringComparer.Ordinal)
            .Take(DivisionNonWinnersCount)
            .ToList();

        if (runnersUp.Count < DivisionNonWinnersCount)
        {
            return null;
        }

        return new DivisionBracket(winners[0], runnersUp[0], runnersUp[1]);
    }

    private static SimulatedTie CreateTie(int nodeNumber, string conference, SeededTeam teamA, SeededTeam teamB) =>
        new(nodeNumber, FirstRoundCode, conference, teamA.EspnTeamId, teamA.Info.Seed, teamB.EspnTeamId, teamB.Info.Seed);

    private sealed record SeededTeam(string EspnTeamId, NhlPlayoffSeed Info);

    private sealed record DivisionBracket(SeededTeam Winner, SeededTeam Second, SeededTeam Third);
}
