using Domain.Entities;
using DTO.Scoring;

namespace UseCases.Scoring;

public class ScoringEngine
{
    /// <summary>
    /// Evalúa todos los partidos finalizados y sus picks correspondientes para una quiniela.
    /// Actualiza en memoria IsUpset en Match, e IsHit, IsUpsetHit, IsHumillacion, IsSomnifero, IsEmpateFallido en Pick.
    /// </summary>
    public void EvaluatePicksAndMatches(
        IEnumerable<Match> matches,
        IEnumerable<Pick> picks,
        int totalQuinielaMembers,
        bool isFootball = false)
    {
        var finishedMatches = matches
            .Where(m => string.Equals(m.StatusState, "post", StringComparison.OrdinalIgnoreCase) 
                        && !string.IsNullOrWhiteSpace(m.WinnerAbbr))
            .ToList();

        var picksList = picks.ToList();
        var picksByMatch = picksList.GroupBy(p => p.MatchId).ToDictionary(g => g.Key, g => g.ToList());

        // El umbral de Upset es cuando atinó el 25% o menos de los miembros registrados (mínimo 1 acierto)
        int upsetMaxCount = (int)Math.Ceiling(totalQuinielaMembers * 0.25);

        foreach (var match in finishedMatches)
        {
            var matchPicks = picksByMatch.GetValueOrDefault(match.Id) ?? new List<Pick>();
            var winner = match.WinnerAbbr!.Trim().ToUpperInvariant();
            var homeScore = match.HomeScore;
            var awayScore = match.AwayScore;
            var goalDiff = Math.Abs(homeScore - awayScore);

            int correctPicks = matchPicks.Count(p => string.Equals(p.PickAbbr?.Trim(), winner, StringComparison.OrdinalIgnoreCase));
            bool isUpset = correctPicks > 0 && correctPicks <= upsetMaxCount;
            match.IsUpset = isUpset;

            int humillacionThreshold = isFootball ? 20 : 3;

            foreach (var pick in matchPicks)
            {
                var pickClean = (pick.PickAbbr ?? string.Empty).Trim().ToUpperInvariant();

                if (string.Equals(pickClean, winner, StringComparison.OrdinalIgnoreCase))
                {
                    pick.IsHit = true;
                    pick.IsUpsetHit = isUpset;
                    pick.IsHumillacion = false;
                    pick.IsSomnifero = false;
                    pick.IsEmpateFallido = false;
                }
                else
                {
                    pick.IsHit = false;
                    pick.IsUpsetHit = false;

                    // A. El Humillado: Derrota por 3+ goles (o 20+ pts NFL) apostando por un perdedor (no empate)
                    pick.IsHumillacion = goalDiff >= humillacionThreshold && pickClean != "EMPATE";

                    // B. Víctima del Somnífero: Apostó por ganador, pero terminó 0-0
                    pick.IsSomnifero = homeScore == 0 && awayScore == 0 && pickClean != "EMPATE";

                    // C. Rey del Empate Fallido: Apostó EMPATE, pero hubo un ganador
                    pick.IsEmpateFallido = pickClean == "EMPATE" && homeScore != awayScore;
                }
            }
        }
    }

    /// <summary>
    /// Aplica la regla anti-abandono por reincidencia de autollenado:
    /// Si un participante acumula 3 o más semanas consecutivas jugando exclusivamente con autollenado,
    /// sus aciertos en la jornada actual quedan topados a: Max(0, MinManualHits - 1),
    /// donde MinManualHits es el puntaje mínimo de los participantes que llenaron manualmente esa semana.
    /// Sus picks pierden además cualquier condición de Upset (Sorpresa).
    /// </summary>
    public void ApplyRecidivistAutofillPenalty(
        IEnumerable<QuinielaMember> members,
        IEnumerable<Pick> currentWeekPicks,
        IEnumerable<Pick> allHistoricalPicks,
        int currentWeekNumber,
        IDictionary<int, int>? matchWeekNumbers = null,
        int consecutiveWeeksThreshold = 3)
    {
        var currentPicksList = currentWeekPicks.ToList();
        if (!currentPicksList.Any()) return;

        int? GetWeekNum(Pick p)
        {
            if (matchWeekNumbers != null && matchWeekNumbers.TryGetValue(p.MatchId, out int w))
                return w;
            return p.Match?.Week?.WeekNumber;
        }

        var currentPicksByMember = currentPicksList
            .GroupBy(p => p.MemberId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var allPicksList = allHistoricalPicks.ToList();
        var historicalByMemberAndWeek = allPicksList
            .Select(p => new { Pick = p, WeekNum = GetWeekNum(p) })
            .Where(x => x.WeekNum.HasValue)
            .GroupBy(x => (x.Pick.MemberId, x.WeekNum!.Value))
            .ToDictionary(g => g.Key, g => g.Select(x => x.Pick).ToList());

        var recidivistMemberIds = new HashSet<int>();

        foreach (var member in members)
        {
            var mCurrent = currentPicksByMember.GetValueOrDefault(member.Id);
            if (mCurrent == null || !mCurrent.Any()) continue;

            // Para ser candidato a reincidente, TODOS los picks de la jornada actual deben ser autollenados
            if (!mCurrent.All(p => p.IsAutoFilled))
            {
                continue;
            }

            int streak = 1; // La jornada actual ya es 100% autollenada
            for (int w = currentWeekNumber - 1; w >= 1; w--)
            {
                if (historicalByMemberAndWeek.TryGetValue((member.Id, w), out var prevPicks) && prevPicks.Any())
                {
                    if (prevPicks.All(p => p.IsAutoFilled))
                    {
                        streak++;
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    break;
                }
            }

            if (streak >= consecutiveWeeksThreshold)
            {
                recidivistMemberIds.Add(member.Id);
            }
        }

        if (!recidivistMemberIds.Any()) return;

        // Calcular puntaje mínimo de los participantes manuales en la jornada actual
        var manualMembersHits = members
            .Where(m => !recidivistMemberIds.Contains(m.Id)
                        && currentPicksByMember.TryGetValue(m.Id, out var picks)
                        && !picks.All(p => p.IsAutoFilled))
            .Select(m => currentPicksByMember[m.Id].Count(p => p.IsHit == true))
            .ToList();

        int minManualHits = manualMembersHits.Any() ? manualMembersHits.Min() : 0;
        int cap = Math.Max(0, minManualHits - 1);

        foreach (var memberId in recidivistMemberIds)
        {
            if (!currentPicksByMember.TryGetValue(memberId, out var mPicks)) continue;

            // 1. Descalificar sorpresas
            foreach (var p in mPicks)
            {
                p.IsUpsetHit = false;
            }

            // 2. Aplicar tope a los aciertos
            var hitPicks = mPicks.Where(p => p.IsHit == true).OrderBy(p => p.MatchId).ToList();
            if (hitPicks.Count > cap)
            {
                for (int i = cap; i < hitPicks.Count; i++)
                {
                    hitPicks[i].IsHit = false;
                }
            }
        }
    }

    /// <summary>
    /// Calcula la tabla de posiciones semanal aplicando el ordenamiento en cascada estricto:
    /// 1° Hits DESC -> 2° UpsetHits DESC -> 3° Humillaciones ASC -> 4° Manual antes que Auto -> 5° Alias ASC.
    /// </summary>
    public List<MemberStandingDto> CalculateWeeklyStandings(
        IEnumerable<QuinielaMember> members,
        IEnumerable<Pick> weekPicks,
        IEnumerable<Match> weekMatches)
    {
        var finishedMatchIds = weekMatches
            .Where(m => string.Equals(m.StatusState, "post", StringComparison.OrdinalIgnoreCase) 
                        && !string.IsNullOrWhiteSpace(m.WinnerAbbr))
            .Select(m => m.Id)
            .ToHashSet();

        var weekPicksList = weekPicks.ToList();
        var picksByMember = weekPicksList
            .Where(p => finishedMatchIds.Contains(p.MatchId))
            .GroupBy(p => p.MemberId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var isAllAutoByMember = weekPicksList
            .GroupBy(p => p.MemberId)
            .ToDictionary(g => g.Key, g => g.Any() && g.All(p => p.IsAutoFilled));

        var standings = new List<MemberStandingDto>();

        foreach (var member in members)
        {
            var mPicks = picksByMember.GetValueOrDefault(member.Id) ?? new List<Pick>();

            int hits = mPicks.Count(p => p.IsHit == true);
            int upsetHits = mPicks.Count(p => p.IsUpsetHit);
            int humillaciones = mPicks.Count(p => p.IsHumillacion);
            int somniferos = mPicks.Count(p => p.IsSomnifero);
            int empatesFallidos = mPicks.Count(p => p.IsEmpateFallido);
            int totalPicks = mPicks.Count;
            decimal accuracy = totalPicks > 0 ? Math.Round((decimal)hits / totalPicks * 100m, 1) : 0m;

            standings.Add(new MemberStandingDto
            {
                MemberId = member.Id,
                UserId = member.UserId,
                Alias = member.Alias,
                DisplayName = member.User?.DisplayName ?? member.Alias,
                AvatarUrl = member.User?.AvatarUrl,
                Hits = hits,
                TotalPicks = totalPicks,
                AccuracyPct = accuracy,
                UpsetHits = upsetHits,
                Humillaciones = humillaciones,
                Somniferos = somniferos,
                EmpatesFallidos = empatesFallidos,
                CurrentStreak = member.CurrentStreak,
                BestStreak = member.BestStreak
            });
        }

        // Ordenamiento en cascada estricto (favorece manual sobre autollenado en empate)
        var sorted = standings
            .OrderByDescending(s => s.Hits)
            .ThenByDescending(s => s.UpsetHits)
            .ThenBy(s => s.Humillaciones)
            .ThenBy(s => isAllAutoByMember.GetValueOrDefault(s.MemberId, false))
            .ThenBy(s => s.Alias)
            .ToList();

        for (int i = 0; i < sorted.Count; i++)
        {
            sorted[i].Rank = i + 1;
        }

        return sorted;
    }

    /// <summary>
    /// Genera la lista de galardones para la jornada: MVP, Rey de Sorpresas, Humillado, Somnífero, Empate Fallido y Partido Más Difícil.
    /// </summary>
    public List<WeeklyAward> CalculateAwards(
        int quinielaId,
        int weekId,
        List<MemberStandingDto> sortedWeeklyStandings,
        IEnumerable<Match> weekMatches,
        IEnumerable<Pick> weekPicks)
    {
        var awards = new List<WeeklyAward>();
        if (!sortedWeeklyStandings.Any()) return awards;

        // 1. MVP: El 1er lugar de la jornada (siempre que tenga al menos 1 acierto)
        var top = sortedWeeklyStandings.First();
        if (top.Hits > 0)
        {
            awards.Add(new WeeklyAward
            {
                QuinielaId = quinielaId,
                WeekId = weekId,
                MemberId = top.MemberId,
                AwardType = "MVP",
                AwardValue1 = top.Hits.ToString(),
                AwardValue2 = $"{top.AccuracyPct}%",
                Notes = $"Líder semanal con {top.Hits} aciertos de {top.TotalPicks} pronósticos ({top.AccuracyPct}%)"
            });
        }

        // Helper para métricas grupales donde puede haber múltiples ganadores o ninguno si max es 0
        void AddMaxAward(string awardType, Func<MemberStandingDto, int> selector, string value2, string noteTemplate)
        {
            int maxVal = sortedWeeklyStandings.Max(selector);
            if (maxVal > 0)
            {
                var winners = sortedWeeklyStandings.Where(s => selector(s) == maxVal).ToList();
                foreach (var winner in winners)
                {
                    awards.Add(new WeeklyAward
                    {
                        QuinielaId = quinielaId,
                        WeekId = weekId,
                        MemberId = winner.MemberId,
                        AwardType = awardType,
                        AwardValue1 = maxVal.ToString(),
                        AwardValue2 = value2,
                        Notes = string.Format(noteTemplate, winner.Alias, maxVal)
                    });
                }
            }
        }

        // 2. Rey de las Sorpresas
        AddMaxAward("REY_SORPRESAS", s => s.UpsetHits, "Sorpresas", "{0} acertó {1} sorpresas en la jornada");

        // 3. El Humillado
        AddMaxAward("HUMILLADO", s => s.Humillaciones, "Humillaciones", "{0} sufrió {1} derrotas por goleada");

        // 4. Víctima del Somnífero
        AddMaxAward("SOMNIFERO", s => s.Somniferos, "0-0", "{0} apostó a ganador en {1} partidos que terminaron 0-0");

        // 5. Rey del Empate Fallido
        AddMaxAward("EMPATE_FALLIDO", s => s.EmpatesFallidos, "Empates rotos", "{0} apostó empate en {1} partidos con ganador");

        // 6. Partido Más Difícil (Rompe-Quinielas)
        var finishedMatches = weekMatches
            .Where(m => string.Equals(m.StatusState, "post", StringComparison.OrdinalIgnoreCase) 
                        && !string.IsNullOrWhiteSpace(m.WinnerAbbr))
            .ToList();

        if (finishedMatches.Any())
        {
            var picksList = weekPicks.ToList();
            Match? hardestMatch = null;
            decimal lowestAccuracy = 101m;
            int totalPicksInHardest = 0;
            int correctPicksInHardest = 0;

            foreach (var match in finishedMatches)
            {
                var mPicks = picksList.Where(p => p.MatchId == match.Id).ToList();
                if (mPicks.Count == 0) continue;

                var winner = match.WinnerAbbr!.Trim().ToUpperInvariant();
                int correct = mPicks.Count(p => string.Equals(p.PickAbbr?.Trim(), winner, StringComparison.OrdinalIgnoreCase));
                decimal acc = (decimal)correct / mPicks.Count * 100m;

                if (acc < lowestAccuracy)
                {
                    lowestAccuracy = acc;
                    hardestMatch = match;
                    totalPicksInHardest = mPicks.Count;
                    correctPicksInHardest = correct;
                }
            }

            if (hardestMatch != null)
            {
                // Asignamos MemberId = top.MemberId como referencia de la quiniela
                awards.Add(new WeeklyAward
                {
                    QuinielaId = quinielaId,
                    WeekId = weekId,
                    MemberId = top.MemberId,
                    AwardType = "PARTIDO_DIFICIL",
                    AwardValue1 = $"{hardestMatch.HomeTeam?.Abbreviation ?? "LOC"} vs {hardestMatch.AwayTeam?.Abbreviation ?? "VIS"}",
                    AwardValue2 = $"{Math.Round(lowestAccuracy, 1)}%",
                    Notes = $"Ganador: {hardestMatch.WinnerAbbr}. Solo {correctPicksInHardest} de {totalPicksInHardest} participantes acertaron ({Math.Round(lowestAccuracy, 1)}%)"
                });
            }
        }

        return awards;
    }

    /// <summary>
    /// Calcula las rachas actuales y mejores rachas históricas de un miembro basado en la secuencia cronológica de partidos.
    /// </summary>
    public (int currentStreak, int bestStreak) CalculateMemberStreaks(IEnumerable<Pick> historicalPicksOrdered)
    {
        var evaluatedPicks = historicalPicksOrdered
            .Where(p => p.IsHit.HasValue)
            .ToList();

        if (!evaluatedPicks.Any()) return (0, 0);

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

        // Current streak: contar aciertos consecutivos desde el último partido hacia atrás
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
}
