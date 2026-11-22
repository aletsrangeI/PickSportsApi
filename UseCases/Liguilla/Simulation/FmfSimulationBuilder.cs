using Domain.Common;
using Interface.UseCases.Knockout;

namespace UseCases.Liguilla.Simulation;

/// <summary>
/// Construye los 4 cruces de Cuartos de Final (<c>QF</c>) de la Liguilla de Liga MX a partir de los primeros 8 puestos de la tabla general en ESPN.
/// </summary>
public sealed class FmfSimulationBuilder : IKnockoutSimulationBuilder
{
    private const string LigaMxStandingsPath = "apis/v2/sports/soccer/mex.1/standings";
    private const string QuarterFinalsRoundCode = "QF";
    private const int FirstSeed = 1;
    private const int LastSeed = 8;
    private const int QualifiedTeamCount = LastSeed - FirstSeed + 1;
    private const int TieCount = QualifiedTeamCount / 2;
    private const int SeedPairSum = FirstSeed + LastSeed;

    private readonly IEspnKnockoutParser _parser;

    public FmfSimulationBuilder(IEspnKnockoutParser parser)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    /// <inheritdoc />
    public string FormatCode => KnockoutFormatCodes.FmfLiguilla;

    /// <inheritdoc />
    public string StandingsPath(string espnApiPath) => LigaMxStandingsPath;

    /// <inheritdoc />
    public IReadOnlyList<SimulatedTie> Build(string standingsJson)
    {
        if (string.IsNullOrWhiteSpace(standingsJson))
        {
            return Array.Empty<SimulatedTie>();
        }

        var seeds = _parser.ParseFmfPlayoffSeeds(standingsJson);
        if (seeds is null || seeds.Count != QualifiedTeamCount)
        {
            return Array.Empty<SimulatedTie>();
        }

        var teamBySeed = new Dictionary<int, string>(QualifiedTeamCount);
        foreach (var (espnTeamId, seed) in seeds)
        {
            if (string.IsNullOrWhiteSpace(espnTeamId) || seed < FirstSeed || seed > LastSeed)
            {
                return Array.Empty<SimulatedTie>();
            }

            if (!teamBySeed.TryAdd(seed, espnTeamId))
            {
                return Array.Empty<SimulatedTie>();
            }
        }

        if (teamBySeed.Count != QualifiedTeamCount)
        {
            return Array.Empty<SimulatedTie>();
        }

        var ties = new List<SimulatedTie>(TieCount);
        for (var node = 1; node <= TieCount; node++)
        {
            var seedA = node;
            var seedB = SeedPairSum - node;
            ties.Add(new SimulatedTie(
                node,
                QuarterFinalsRoundCode,
                null,
                teamBySeed[seedA],
                seedA,
                teamBySeed[seedB],
                seedB));
        }

        return ties;
    }
}
