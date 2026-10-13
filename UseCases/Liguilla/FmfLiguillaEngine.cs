using Domain.Entities;
using DTO.Liguilla;

namespace UseCases.Liguilla;

public record TeamSeedInfo(int TeamId, int Seed);
public record SemiPairing(TeamSeedInfo TeamA, TeamSeedInfo TeamB);

public static class FmfLiguillaEngine
{
    public const int QF_POINTS_PER_HIT = 10;
    public const int SF_POINTS_PER_HIT = 25;
    public const int CHAMPION_POINTS = 50;
    public const int PERFECT_BONUS = 20;

    /// <summary>
    /// Aplica el Artículo 10 del Reglamento Oficial de la FMF:
    /// Reordena a los 4 ganadores de Cuartos de Final según su siembra en la Jornada 17.
    /// Semifinal 1: El mejor clasificado (1°) vs el peor clasificado (4°).
    /// Semifinal 2: El segundo mejor (2°) vs el tercer mejor (3°).
    /// </summary>
    public static (SemiPairing Semi1, SemiPairing Semi2) CalculateFmfReSeed(IEnumerable<TeamSeedInfo> quarterfinalWinners)
    {
        var sorted = quarterfinalWinners.OrderBy(t => t.Seed).ToList();
        if (sorted.Count != 4)
        {
            throw new ArgumentException("Deben proporcionarse exactamente 4 ganadores de Cuartos de Final para el reacomodo FMF.");
        }

        var semi1 = new SemiPairing(sorted[0], sorted[3]);
        var semi2 = new SemiPairing(sorted[1], sorted[2]);

        return (semi1, semi2);
    }

    /// <summary>
    /// Calcula los puntos y desglose para un LiguillaBracket dado el estado de los nodos oficiales.
    /// </summary>
    public static (int qfPts, int sfPts, int champPts, int bonusPts, int totalPts, List<int> shatteredTeamIds) EvaluateBracket(
        LiguillaBracket bracket,
        IReadOnlyList<LiguillaNode> nodes)
    {
        int qfHits = 0;
        int sfHits = 0;
        bool champHit = false;

        var userQfPicks = new HashSet<int>
        {
            bracket.QfWinner1TeamId,
            bracket.QfWinner2TeamId,
            bracket.QfWinner3TeamId,
            bracket.QfWinner4TeamId
        };

        var userSfPicks = new HashSet<int>
        {
            bracket.SfWinner1TeamId,
            bracket.SfWinner2TeamId
        };

        var eliminatedTeams = new HashSet<int>();

        // 1. Evaluar Cuartos de Final (Nodos 1 al 4)
        var qfNodes = nodes.Where(n => n.NodeNumber >= 1 && n.NodeNumber <= 4).ToList();
        foreach (var node in qfNodes)
        {
            if (node.IsResolved && node.WinnerTeamId.HasValue)
            {
                if (userQfPicks.Contains(node.WinnerTeamId.Value))
                    qfHits++;

                // El perdedor de la serie queda eliminado de la Liguilla
                if (node.TeamAId.HasValue && node.WinnerTeamId != node.TeamAId.Value)
                    eliminatedTeams.Add(node.TeamAId.Value);
                if (node.TeamBId.HasValue && node.WinnerTeamId != node.TeamBId.Value)
                    eliminatedTeams.Add(node.TeamBId.Value);
            }
        }

        // 2. Evaluar Semifinales (Nodos 5 y 6)
        var sfNodes = nodes.Where(n => n.NodeNumber == 5 || n.NodeNumber == 6).ToList();
        foreach (var node in sfNodes)
        {
            if (node.IsResolved && node.WinnerTeamId.HasValue)
            {
                if (userSfPicks.Contains(node.WinnerTeamId.Value))
                    sfHits++;

                if (node.TeamAId.HasValue && node.WinnerTeamId != node.TeamAId.Value)
                    eliminatedTeams.Add(node.TeamAId.Value);
                if (node.TeamBId.HasValue && node.WinnerTeamId != node.TeamBId.Value)
                    eliminatedTeams.Add(node.TeamBId.Value);
            }
        }

        // 3. Evaluar Final (Nodo 7)
        var finalNode = nodes.FirstOrDefault(n => n.NodeNumber == 7);
        if (finalNode != null && finalNode.IsResolved && finalNode.WinnerTeamId.HasValue)
        {
            if (bracket.ChampionTeamId == finalNode.WinnerTeamId.Value)
                champHit = true;

            if (finalNode.TeamAId.HasValue && finalNode.WinnerTeamId != finalNode.TeamAId.Value)
                eliminatedTeams.Add(finalNode.TeamAId.Value);
            if (finalNode.TeamBId.HasValue && finalNode.WinnerTeamId != finalNode.TeamBId.Value)
                eliminatedTeams.Add(finalNode.TeamBId.Value);
        }

        int qfPts = qfHits * QF_POINTS_PER_HIT;
        int sfPts = sfHits * SF_POINTS_PER_HIT;
        int champPts = champHit ? CHAMPION_POINTS : 0;

        // Bono perfecto (7 de 7) si todos los nodos están resueltos
        bool allResolved = nodes.Count >= 7 && nodes.All(n => n.IsResolved);
        int bonusPts = (allResolved && qfHits == 4 && sfHits == 2 && champHit) ? PERFECT_BONUS : 0;
        int totalPts = qfPts + sfPts + champPts + bonusPts;

        // Identificar equipos elegidos por el usuario que ya fueron eliminados
        var userAllChosen = new HashSet<int>(userQfPicks);
        userAllChosen.UnionWith(userSfPicks);
        userAllChosen.Add(bracket.ChampionTeamId);

        var shattered = userAllChosen.Where(teamId => eliminatedTeams.Contains(teamId)).ToList();

        return (qfPts, sfPts, champPts, bonusPts, totalPts, shattered);
    }
}
