using Domain.Entities;
using Interface.UseCases.Knockout;

namespace UseCases.Liguilla;

/// <summary>
/// Motor de puntuación genérico de eliminatorias (SPEC-027). Reemplaza a <c>FmfLiguillaEngine.EvaluateBracket</c>:
/// los puntos, las rondas y el bono salen de la estrategia del formato.
/// </summary>
public static class KnockoutScoringEngine
{
    public static KnockoutScore Evaluate(
        IKnockoutFormatStrategy strategy,
        IReadOnlyDictionary<string, IReadOnlyList<int>> picksByRound,
        IReadOnlyList<LiguillaNode> nodes)
    {
        var pointsByRound = new Dictionary<string, int>();
        var eliminatedTeams = new HashSet<int>();
        var pickableNodes = new List<LiguillaNode>();
        var chosenTeams = new List<int>();
        bool allPicksHit = true;
        int expectedNodeCount = 0;
        int roundPointsSum = 0;

        foreach (var round in strategy.Rounds)
        {
            IReadOnlyList<int> roundPicks = picksByRound.TryGetValue(round.Code, out var picks) ? picks : Array.Empty<int>();
            var roundNodes = nodes
                .Where(n => string.Equals(n.Round, round.Code, StringComparison.OrdinalIgnoreCase))
                .ToList();

            pickableNodes.AddRange(roundNodes);
            chosenTeams.AddRange(roundPicks);
            expectedNodeCount += round.NodeCount;

            int hits = 0;
            foreach (var node in roundNodes.Where(n => n.IsResolved && n.WinnerTeamId.HasValue))
            {
                int winnerId = node.WinnerTeamId!.Value;
                if (roundPicks.Contains(winnerId))
                    hits++;

                // El perdedor del cruce queda eliminado
                if (node.TeamAId.HasValue && node.TeamAId.Value != winnerId)
                    eliminatedTeams.Add(node.TeamAId.Value);
                if (node.TeamBId.HasValue && node.TeamBId.Value != winnerId)
                    eliminatedTeams.Add(node.TeamBId.Value);
            }

            int roundPoints = hits * round.PointsPerHit;
            pointsByRound[round.Code] = roundPoints;
            roundPointsSum += roundPoints;

            if (hits != round.WinnersToPick)
                allPicksHit = false;
        }

        bool allResolved = pickableNodes.Count >= expectedNodeCount && pickableNodes.All(n => n.IsResolved);
        int bonusPoints = allResolved && allPicksHit ? strategy.PerfectBonusPoints : 0;

        var shattered = chosenTeams
            .Distinct()
            .Where(eliminatedTeams.Contains)
            .ToList();

        return new KnockoutScore(roundPointsSum + bonusPoints, bonusPoints, pointsByRound, shattered);
    }
}
