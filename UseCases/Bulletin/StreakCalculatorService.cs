using Domain.Entities;
using DTO.Bulletin;

namespace UseCases.Bulletin;

public class StreakCalculatorService
{
    private const int MinStreakThreshold = 3;

    public List<TeamStreakDto> CalculateNotableStreaks(
        IEnumerable<Match> seasonMatches,
        IEnumerable<Match> currentWeekMatches)
    {
        var result = new List<TeamStreakDto>();
        var currentMatchList = currentWeekMatches
            .Where(m => string.Equals(m.StatusState, "post", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!currentMatchList.Any()) return result;

        var allFinished = seasonMatches
            .Where(m => string.Equals(m.StatusState, "post", StringComparison.OrdinalIgnoreCase))
            .OrderBy(m => m.DateUtc)
            .ToList();

        // Obtener IDs de equipos participantes en la jornada actual
        var teamsInCurrentWeek = new HashSet<int>();
        foreach (var m in currentMatchList)
        {
            teamsInCurrentWeek.Add(m.HomeTeamId);
            teamsInCurrentWeek.Add(m.AwayTeamId);
        }

        foreach (var teamId in teamsInCurrentWeek)
        {
            // Partidos del equipo ordenados cronológicamente
            var teamMatches = allFinished
                .Where(m => m.HomeTeamId == teamId || m.AwayTeamId == teamId)
                .OrderBy(m => m.DateUtc)
                .ToList();

            var currentWeekMatch = currentMatchList
                .FirstOrDefault(m => m.HomeTeamId == teamId || m.AwayTeamId == teamId);

            if (currentWeekMatch == null) continue;

            // Partidos previos a la jornada actual
            var previousMatches = teamMatches
                .Where(m => m.Id != currentWeekMatch.Id && m.DateUtc < currentWeekMatch.DateUtc)
                .ToList();

            if (!previousMatches.Any()) continue;

            var teamObj = currentWeekMatch.HomeTeamId == teamId ? currentWeekMatch.HomeTeam : currentWeekMatch.AwayTeam;
            var teamName = teamObj?.Name ?? $"Equipo {teamId}";
            var teamAbbr = teamObj?.Abbreviation ?? teamId.ToString();

            // Calcular racha previa de victorias
            int priorWinStreak = 0;
            for (int i = previousMatches.Count - 1; i >= 0; i--)
            {
                if (DidTeamWin(previousMatches[i], teamId, teamAbbr))
                    priorWinStreak++;
                else
                    break;
            }

            // Calcular racha previa de derrotas
            int priorLossStreak = 0;
            for (int i = previousMatches.Count - 1; i >= 0; i--)
            {
                if (DidTeamLose(previousMatches[i], teamId, teamAbbr))
                    priorLossStreak++;
                else
                    break;
            }

            bool wonCurrent = DidTeamWin(currentWeekMatch, teamId, teamAbbr);
            bool lostCurrent = DidTeamLose(currentWeekMatch, teamId, teamAbbr);

            if (priorWinStreak >= MinStreakThreshold)
            {
                if (!wonCurrent)
                {
                    result.Add(new TeamStreakDto
                    {
                        TeamName = teamName,
                        TeamAbbr = teamAbbr,
                        StreakType = "WIN_STREAK",
                        StreakCount = priorWinStreak,
                        IsBroken = true,
                        Description = $"{teamName} llegaba con una racha de {priorWinStreak} victorias consecutivas y fue ROTA esta jornada."
                    });
                }
                else
                {
                    result.Add(new TeamStreakDto
                    {
                        TeamName = teamName,
                        TeamAbbr = teamAbbr,
                        StreakType = "WIN_STREAK",
                        StreakCount = priorWinStreak + 1,
                        IsBroken = false,
                        Description = $"{teamName} extendió su racha ganadora a {priorWinStreak + 1} victorias consecutivas."
                    });
                }
            }
            else if (priorLossStreak >= MinStreakThreshold)
            {
                if (!lostCurrent)
                {
                    result.Add(new TeamStreakDto
                    {
                        TeamName = teamName,
                        TeamAbbr = teamAbbr,
                        StreakType = "LOSS_STREAK",
                        StreakCount = priorLossStreak,
                        IsBroken = true,
                        Description = $"{teamName} acumulaba {priorLossStreak} derrotas al hilo y rompió la mala racha."
                    });
                }
                else
                {
                    result.Add(new TeamStreakDto
                    {
                        TeamName = teamName,
                        TeamAbbr = teamAbbr,
                        StreakType = "LOSS_STREAK",
                        StreakCount = priorLossStreak + 1,
                        IsBroken = false,
                        Description = $"{teamName} cayó nuevamente y sumó su {priorLossStreak + 1}ª derrota consecutiva."
                    });
                }
            }
        }

        return result;
    }

    private static bool DidTeamWin(Match match, int teamId, string teamAbbr)
    {
        if (string.Equals(match.WinnerAbbr, "EMPATE", StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.IsNullOrEmpty(match.WinnerAbbr))
        {
            return string.Equals(match.WinnerAbbr, teamAbbr, StringComparison.OrdinalIgnoreCase);
        }
        if (match.HomeTeamId == teamId) return match.HomeScore > match.AwayScore;
        if (match.AwayTeamId == teamId) return match.AwayScore > match.HomeScore;
        return false;
    }

    private static bool DidTeamLose(Match match, int teamId, string teamAbbr)
    {
        if (string.Equals(match.WinnerAbbr, "EMPATE", StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.IsNullOrEmpty(match.WinnerAbbr))
        {
            return !string.Equals(match.WinnerAbbr, teamAbbr, StringComparison.OrdinalIgnoreCase);
        }
        if (match.HomeTeamId == teamId) return match.HomeScore < match.AwayScore;
        if (match.AwayTeamId == teamId) return match.AwayScore < match.HomeScore;
        return false;
    }
}
