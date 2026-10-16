using Domain.Entities;
using DTO.CustomEvents;

namespace UseCases.CustomEvents;

public class CustomEventScoringEngine
{
    /// <summary>
    /// Evalúa las preguntas y opciones de una ronda al resolverse:
    /// Marca option.IsCorrect = (option.Id == winningOptionId),
    /// y para cada EventPick de la pregunta asigna:
    /// pick.IsHit = (pick.EventOptionId == winningOptionId)
    /// pick.PointsEarned = pick.IsHit == true ? question.Points : 0
    /// </summary>
    public void EvaluateRound(
        IEnumerable<EventQuestion> questions,
        IEnumerable<QuestionResolutionItemDto> resolutions,
        IEnumerable<EventPick> picks)
    {
        var resolutionMap = resolutions.ToDictionary(r => r.EventQuestionId, r => r.WinningOptionId);
        var picksByQuestion = picks.GroupBy(p => p.EventQuestionId).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var question in questions)
        {
            if (!resolutionMap.TryGetValue(question.Id, out var winningOptionId))
            {
                continue;
            }

            foreach (var option in question.Options)
            {
                option.IsCorrect = (option.Id == winningOptionId);
            }

            var questionPicks = picksByQuestion.GetValueOrDefault(question.Id) ?? question.Picks?.ToList() ?? new List<EventPick>();
            foreach (var pick in questionPicks)
            {
                pick.IsHit = (pick.EventOptionId == winningOptionId);
                pick.PointsEarned = (pick.IsHit == true) ? question.Points : 0;
            }
        }
    }

    /// <summary>
    /// Calcula las rachas (CurrentStreak, BestStreak) de un miembro sobre sus EventPicks resueltos
    /// ordenados por RoundOrder y QuestionOrder.
    /// </summary>
    public (int currentStreak, int bestStreak) CalculateMemberStreaks(IEnumerable<EventPick> picks)
    {
        var evaluatedPicks = picks
            .Where(p => p.IsHit.HasValue)
            .OrderBy(p => p.EventQuestion?.EventRound?.RoundOrder ?? 0)
            .ThenBy(p => p.EventQuestion?.QuestionOrder ?? 0)
            .ThenBy(p => p.SubmittedAtUtc)
            .ToList();

        if (!evaluatedPicks.Any())
        {
            return (0, 0);
        }

        int bestStreak = 0;
        int runningStreak = 0;

        foreach (var pick in evaluatedPicks)
        {
            if (pick.IsHit == true)
            {
                runningStreak++;
                if (runningStreak > bestStreak)
                {
                    bestStreak = runningStreak;
                }
            }
            else
            {
                runningStreak = 0;
            }
        }

        int currentStreak = 0;
        for (int i = evaluatedPicks.Count - 1; i >= 0; i--)
        {
            if (evaluatedPicks[i].IsHit == true)
            {
                currentStreak++;
            }
            else
            {
                break;
            }
        }

        return (currentStreak, bestStreak);
    }

    /// <summary>
    /// Calcula la tabla de posiciones de una ronda ordenando por:
    /// Points DESC -> Hits DESC -> BestStreak DESC -> Alias ASC.
    /// </summary>
    public List<EventStandingRowDto> CalculateRoundStandings(
        IEnumerable<QuinielaMember> members,
        IEnumerable<EventQuestion> questions,
        IEnumerable<EventPick> roundPicks)
    {
        var memberList = members.ToList();
        var questionList = questions.ToList();
        var pickList = roundPicks.ToList();
        int totalQuestions = questionList.Count;

        var picksByMember = pickList
            .GroupBy(p => p.QuinielaMemberId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var standings = new List<EventStandingRowDto>();

        foreach (var member in memberList)
        {
            var mPicks = picksByMember.GetValueOrDefault(member.Id) ?? new List<EventPick>();

            int hits = mPicks.Count(p => p.IsHit == true);
            int points = mPicks.Where(p => p.IsHit == true).Sum(p => p.PointsEarned);
            var (computedCurrentStreak, computedBestStreak) = CalculateMemberStreaks(mPicks);
            int bestStreak = Math.Max(member.BestStreak, computedBestStreak);
            int currentStreak = mPicks.Any() ? computedCurrentStreak : member.CurrentStreak;

            standings.Add(new EventStandingRowDto
            {
                MemberId = member.Id,
                Alias = member.Alias,
                DisplayName = member.User?.DisplayName ?? member.Alias,
                AvatarUrl = member.User?.AvatarUrl,
                Points = points,
                Hits = hits,
                TotalQuestions = totalQuestions,
                CurrentStreak = currentStreak,
                BestStreak = bestStreak
            });
        }

        var sorted = standings
            .OrderByDescending(s => s.Points)
            .ThenByDescending(s => s.Hits)
            .ThenByDescending(s => s.BestStreak)
            .ThenBy(s => s.Alias, StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (int i = 0; i < sorted.Count; i++)
        {
            sorted[i].Rank = i + 1;
        }

        return sorted;
    }

    /// <summary>
    /// Calcula la tabla general acumulada de la quiniela ordenando por:
    /// Points DESC -> Hits DESC -> BestStreak DESC -> Alias ASC.
    /// </summary>
    public List<EventStandingRowDto> CalculateGeneralStandings(
        IEnumerable<QuinielaMember> members,
        IEnumerable<EventPick> allResolvedPicks,
        int totalResolvedQuestions)
    {
        var memberList = members.ToList();
        var pickList = allResolvedPicks.Where(p => p.IsHit.HasValue).ToList();

        var picksByMember = pickList
            .GroupBy(p => p.QuinielaMemberId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var standings = new List<EventStandingRowDto>();

        foreach (var member in memberList)
        {
            var mPicks = picksByMember.GetValueOrDefault(member.Id) ?? new List<EventPick>();

            int hits = mPicks.Count(p => p.IsHit == true);
            int points = mPicks.Where(p => p.IsHit == true).Sum(p => p.PointsEarned);
            var (computedCurrentStreak, computedBestStreak) = CalculateMemberStreaks(mPicks);
            int bestStreak = Math.Max(member.BestStreak, computedBestStreak);
            int currentStreak = mPicks.Any() ? computedCurrentStreak : member.CurrentStreak;

            standings.Add(new EventStandingRowDto
            {
                MemberId = member.Id,
                Alias = member.Alias,
                DisplayName = member.User?.DisplayName ?? member.Alias,
                AvatarUrl = member.User?.AvatarUrl,
                Points = points,
                Hits = hits,
                TotalQuestions = totalResolvedQuestions,
                CurrentStreak = currentStreak,
                BestStreak = bestStreak
            });
        }

        var sorted = standings
            .OrderByDescending(s => s.Points)
            .ThenByDescending(s => s.Hits)
            .ThenByDescending(s => s.BestStreak)
            .ThenBy(s => s.Alias, StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (int i = 0; i < sorted.Count; i++)
        {
            sorted[i].Rank = i + 1;
        }

        return sorted;
    }
}
