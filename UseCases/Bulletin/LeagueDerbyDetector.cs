using Domain.Entities;
using DTO.Bulletin;
using UseCases.Broadcasters;

namespace UseCases.Bulletin;

public class LeagueDerbyDetector
{
    private static readonly (string TeamA, string TeamB, string Name)[] Derbies =
    [
        ("AME", "GDL", "El Clásico Nacional"),
        ("MTY", "UANL", "El Clásico Regio"),
        ("AME", "CAZ", "El Clásico Joven"),
        ("AME", "UNAM", "El Clásico Capitalino"),
        ("GDL", "ATL", "El Clásico Tapatío"),
        ("LEO", "QRO", "El Clásico del Bajío"),
        ("ASL", "QRO", "El Clásico de la 57")
    ];

    public List<LeagueDerbyDto> DetectDerbies(IEnumerable<Match> matches)
    {
        var result = new List<LeagueDerbyDto>();

        foreach (var match in matches)
        {
            var homeAbbr = BroadcastRuleEngine.NormalizeAbbreviation(match.HomeTeam?.Abbreviation);
            var awayAbbr = BroadcastRuleEngine.NormalizeAbbreviation(match.AwayTeam?.Abbreviation);

            if (string.IsNullOrEmpty(homeAbbr) || string.IsNullOrEmpty(awayAbbr))
                continue;

            foreach (var (teamA, teamB, derbyName) in Derbies)
            {
                var matchesDerby = (homeAbbr.Equals(teamA, StringComparison.OrdinalIgnoreCase) && awayAbbr.Equals(teamB, StringComparison.OrdinalIgnoreCase))
                                || (homeAbbr.Equals(teamB, StringComparison.OrdinalIgnoreCase) && awayAbbr.Equals(teamA, StringComparison.OrdinalIgnoreCase));

                if (matchesDerby)
                {
                    var homeName = match.HomeTeam?.Name ?? homeAbbr;
                    var awayName = match.AwayTeam?.Name ?? awayAbbr;
                    var matchLabel = $"{homeName} vs {awayName}";

                    var isFinished = string.Equals(match.StatusState, "post", StringComparison.OrdinalIgnoreCase);
                    var scoreLabel = isFinished
                        ? $"{homeName} {match.HomeScore} - {match.AwayScore} {awayName}"
                        : $"{matchLabel} (Por jugar)";

                    result.Add(new LeagueDerbyDto
                    {
                        DerbyName = derbyName,
                        MatchLabel = matchLabel,
                        ScoreLabel = scoreLabel,
                        WinnerAbbr = isFinished ? match.WinnerAbbr : null
                    });
                    break;
                }
            }
        }

        return result;
    }
}
