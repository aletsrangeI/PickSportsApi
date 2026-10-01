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
        int consecutiveWeeksThreshold = 3,
        int? seasonId = null)
    {
        var currentPicksList = currentWeekPicks.ToList();
        if (!currentPicksList.Any()) return;

        // Blindaje multi-temporada: el WeekNumber se repite entre temporadas, así que se descartan
        // picks cuya jornada pertenece a otra temporada. Picks sin Week cargada se conservan.
        if (seasonId.HasValue)
        {
            allHistoricalPicks = allHistoricalPicks.Where(p => p.Match?.Week == null || p.Match.Week.SeasonId == seasonId.Value);
        }

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
    /// Calcula la Tabla General acumulada de una temporada hasta la jornada indicada (inclusive).
    /// A partir de la jornada 2 asigna PreviousRank y RankDelta respecto a la jornada anterior.
    /// Solo considera picks calificados y galardones MVP de la temporada indicada (ADR-005/ADR-006).
    /// Desempate en cascada: Aciertos DESC -> Sorpresas DESC -> Humillaciones ASC -> Jornadas como MVP DESC
    /// -> Mejor jornada más reciente (aciertos de la última jornada, luego la anterior...) -> Alias (último recurso técnico).
    /// </summary>
    public List<MemberStandingDto> CalculateGeneralStandings(
        IEnumerable<QuinielaMember> members,
        IEnumerable<Pick> allPicks,
        int seasonId,
        int uptoWeekNumber,
        IEnumerable<WeeklyAward>? awards = null)
    {
        var memberList = members.ToList();
        var seasonPicks = allPicks
            .Where(p => p.IsHit.HasValue && p.Match?.Week != null && p.Match.Week.SeasonId == seasonId)
            .ToList();
        var seasonMvps = (awards ?? Enumerable.Empty<WeeklyAward>())
            .Where(a => a.AwardType == "MVP" && a.Week != null && a.Week.SeasonId == seasonId)
            .ToList();

        var sortedGeneral = BuildGeneralTable(memberList, seasonPicks, seasonMvps, uptoWeekNumber);

        if (uptoWeekNumber > 1)
        {
            var previousRanks = BuildGeneralTable(memberList, seasonPicks, seasonMvps, uptoWeekNumber - 1)
                .ToDictionary(s => s.MemberId, s => s.Rank);
            foreach (var standing in sortedGeneral)
            {
                if (previousRanks.TryGetValue(standing.MemberId, out var previousRank))
                {
                    standing.PreviousRank = previousRank;
                    standing.RankDelta = previousRank - standing.Rank;
                }
            }
        }

        return sortedGeneral;
    }

    private List<MemberStandingDto> BuildGeneralTable(
        List<QuinielaMember> members,
        List<Pick> seasonPicks,
        List<WeeklyAward> seasonMvps,
        int uptoWeekNumber)
    {
        var picksByMember = seasonPicks
            .Where(p => p.Match.Week.WeekNumber <= uptoWeekNumber)
            .GroupBy(p => p.MemberId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var mvpCountByMember = seasonMvps
            .Where(a => a.Week.WeekNumber <= uptoWeekNumber)
            .GroupBy(a => a.MemberId)
            .ToDictionary(g => g.Key, g => g.Count());

        var rows = new List<(MemberStandingDto Standing, int MvpCount, int[] HitsByRecentWeek)>();
        foreach (var m in members)
        {
            var mHistorical = picksByMember.GetValueOrDefault(m.Id) ?? new List<Pick>();
            int totalPicks = mHistorical.Count;
            int totalHits = mHistorical.Count(p => p.IsHit == true);
            decimal acc = totalPicks > 0 ? Math.Round((decimal)totalHits / totalPicks * 100m, 1) : 0m;

            var (currentStreak, bestStreak) = CalculateMemberStreaks(mHistorical);

            // Índice 0 = jornada más reciente; se compara hacia atrás hasta la jornada 1
            var hitsByWeek = mHistorical
                .Where(p => p.IsHit == true)
                .GroupBy(p => p.Match.Week.WeekNumber)
                .ToDictionary(g => g.Key, g => g.Count());
            var hitsByRecentWeek = Enumerable.Range(0, Math.Max(uptoWeekNumber, 0))
                .Select(i => hitsByWeek.GetValueOrDefault(uptoWeekNumber - i))
                .ToArray();

            rows.Add((new MemberStandingDto
            {
                MemberId = m.Id,
                UserId = m.UserId,
                Alias = m.Alias,
                DisplayName = m.User?.DisplayName ?? m.Alias,
                AvatarUrl = m.User?.AvatarUrl,
                Hits = totalHits,
                TotalPicks = totalPicks,
                AccuracyPct = acc,
                UpsetHits = mHistorical.Count(p => p.IsUpsetHit),
                Humillaciones = mHistorical.Count(p => p.IsHumillacion),
                Somniferos = mHistorical.Count(p => p.IsSomnifero),
                EmpatesFallidos = mHistorical.Count(p => p.IsEmpateFallido),
                CurrentStreak = currentStreak,
                BestStreak = bestStreak
            }, mvpCountByMember.GetValueOrDefault(m.Id), hitsByRecentWeek));
        }

        var sorted = rows
            .OrderByDescending(r => r.Standing.Hits)
            .ThenByDescending(r => r.Standing.UpsetHits)
            .ThenBy(r => r.Standing.Humillaciones)
            .ThenByDescending(r => r.MvpCount)
            .ThenBy(r => r.HitsByRecentWeek, RecentWeeksComparer.Instance)
            // Último recurso técnico: solo aplica si empatan en todas las jornadas y en MVPs
            .ThenBy(r => r.Standing.Alias)
            .Select(r => r.Standing)
            .ToList();

        for (int i = 0; i < sorted.Count; i++)
        {
            sorted[i].Rank = i + 1;
        }

        return sorted;
    }

    /// <summary>
    /// Ordena primero a quien tuvo más aciertos en la jornada más reciente; si empatan, compara la anterior, y así sucesivamente.
    /// </summary>
    private sealed class RecentWeeksComparer : IComparer<int[]>
    {
        public static readonly RecentWeeksComparer Instance = new();

        public int Compare(int[]? x, int[]? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x == null) return 1;
            if (y == null) return -1;

            int length = Math.Min(x.Length, y.Length);
            for (int i = 0; i < length; i++)
            {
                if (x[i] != y[i]) return y[i].CompareTo(x[i]);
            }
            return 0;
        }
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

        var picksList = weekPicks.ToList();
        var matchesById = weekMatches.ToDictionary(m => m.Id);

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
        void AddMaxAward(
            string awardType,
            Func<MemberStandingDto, int> selector,
            string value2,
            string noteTemplate,
            Func<Pick, bool> triggeringPick)
        {
            int maxVal = sortedWeeklyStandings.Max(selector);
            if (maxVal > 0)
            {
                var winners = sortedWeeklyStandings.Where(s => selector(s) == maxVal).ToList();
                foreach (var winner in winners)
                {
                    var details = GetAwardTriggerDetails(awardType, winner.MemberId, weekMatches, weekPicks, sortedWeeklyStandings.Count);
                    string notes = FormatEnrichedNotes(awardType, winner.Alias, maxVal, details, noteTemplate);

                    awards.Add(new WeeklyAward
                    {
                        QuinielaId = quinielaId,
                        WeekId = weekId,
                        MemberId = winner.MemberId,
                        AwardType = awardType,
                        AwardValue1 = maxVal.ToString(),
                        AwardValue2 = value2,
                        Notes = notes
                    });
                }
            }
        }

        // 2. Rey de las Sorpresas
        AddMaxAward("REY_SORPRESAS", s => s.UpsetHits, "Sorpresas", "{0} acertó {1} sorpresas en la jornada", p => p.IsUpsetHit);

        // 3. El Humillado
        AddMaxAward("HUMILLADO", s => s.Humillaciones, "Humillaciones", "{0} sufrió {1} derrotas por goleada", p => p.IsHumillacion);

        // 4. Víctima del Somnífero
        AddMaxAward("SOMNIFERO", s => s.Somniferos, "0-0", "{0} apostó a ganador en {1} partidos que terminaron 0-0", p => p.IsSomnifero);

        // 5. Rey del Empate Fallido
        AddMaxAward("EMPATE_FALLIDO", s => s.EmpatesFallidos, "Empates rotos", "{0} apostó empate en {1} partidos con ganador", p => p.IsEmpateFallido);

        // 6. Partido Más Difícil (Rompe-Quinielas)
        var finishedMatches = weekMatches
            .Where(m => string.Equals(m.StatusState, "post", StringComparison.OrdinalIgnoreCase) 
                        && !string.IsNullOrWhiteSpace(m.WinnerAbbr))
            .ToList();

        if (finishedMatches.Any())
        {
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

    public static string GetBaseNoteTemplate(string awardType) => awardType switch
    {
        "REY_SORPRESAS" => "{0} acertó {1} sorpresas en la jornada",
        "HUMILLADO" => "{0} sufrió {1} derrotas por goleada",
        "SOMNIFERO" => "{0} apostó a ganador en {1} partidos que terminaron 0-0",
        "EMPATE_FALLIDO" => "{0} apostó empate en {1} partidos con ganador",
        _ => "{0}"
    };

    public static List<AwardMatchDetailDto> GetAwardTriggerDetails(
        string awardType,
        int memberId,
        IEnumerable<Match> weekMatches,
        IEnumerable<Pick> weekPicks,
        int totalMembers = 0)
    {
        var details = new List<AwardMatchDetailDto>();
        var matchesList = weekMatches.ToList();
        var picksList = weekPicks.ToList();
        var matchesById = matchesList.ToDictionary(m => m.Id);

        var memberPicks = picksList.Where(p => p.MemberId == memberId).ToList();

        switch (awardType)
        {
            case "REY_SORPRESAS":
            {
                var upsetPicks = memberPicks
                    .Where(p => p.IsUpsetHit || (p.IsHit == true && matchesById.TryGetValue(p.MatchId, out var m) && m.IsUpset))
                    .OrderBy(p => p.MatchId)
                    .ToList();

                foreach (var pick in upsetPicks)
                {
                    if (!matchesById.TryGetValue(pick.MatchId, out var match)) continue;
                    var homeAbbr = match.HomeTeam?.Abbreviation ?? "LOC";
                    var awayAbbr = match.AwayTeam?.Abbreviation ?? "VIS";
                    var homeName = match.HomeTeam?.Name ?? homeAbbr;
                    var awayName = match.AwayTeam?.Name ?? awayAbbr;
                    var score = $"{match.HomeScore}-{match.AwayScore}";

                    var mPicks = picksList.Where(p => p.MatchId == match.Id).ToList();
                    int correct = mPicks.Count(p => string.Equals(p.PickAbbr?.Trim(), match.WinnerAbbr?.Trim(), StringComparison.OrdinalIgnoreCase));
                    decimal pct = mPicks.Count > 0 ? Math.Round((decimal)correct / mPicks.Count * 100m, 1) : 0m;

                    details.Add(new AwardMatchDetailDto
                    {
                        MatchId = match.Id,
                        MatchTitle = $"{homeName} vs {awayName}",
                        TeamsAbbr = $"{homeAbbr} vs {awayAbbr}",
                        Score = score,
                        PickAbbr = pick.PickAbbr ?? "",
                        DetailText = $"{homeAbbr} {score} {awayAbbr} (pronóstico {pick.PickAbbr})",
                        ContextText = mPicks.Count > 0 
                            ? $"Acierto sorpresa: solo {correct} de {mPicks.Count} acertaron ({pct}%)"
                            : "Acierto sorpresa contra el pronóstico mayoritario"
                    });
                }
                break;
            }
            case "EMPATE_FALLIDO":
            {
                var failedDrawPicks = memberPicks
                    .Where(p => p.IsEmpateFallido || (string.Equals(p.PickAbbr?.Trim(), "EMPATE", StringComparison.OrdinalIgnoreCase) && matchesById.TryGetValue(p.MatchId, out var m) && m.HomeScore != m.AwayScore))
                    .OrderBy(p => p.MatchId)
                    .ToList();

                foreach (var pick in failedDrawPicks)
                {
                    if (!matchesById.TryGetValue(pick.MatchId, out var match)) continue;
                    var homeAbbr = match.HomeTeam?.Abbreviation ?? "LOC";
                    var awayAbbr = match.AwayTeam?.Abbreviation ?? "VIS";
                    var homeName = match.HomeTeam?.Name ?? homeAbbr;
                    var awayName = match.AwayTeam?.Name ?? awayAbbr;
                    var score = $"{match.HomeScore}-{match.AwayScore}";

                    details.Add(new AwardMatchDetailDto
                    {
                        MatchId = match.Id,
                        MatchTitle = $"{homeName} vs {awayName}",
                        TeamsAbbr = $"{homeAbbr} vs {awayAbbr}",
                        Score = score,
                        PickAbbr = "EMPATE",
                        DetailText = $"{homeAbbr} {score} {awayAbbr} (pronóstico EMPATE)",
                        ContextText = $"Pronosticó empate, pero ganó {match.WinnerAbbr}"
                    });
                }
                break;
            }
            case "HUMILLADO":
            {
                var humillacionPicks = memberPicks
                    .Where(p => p.IsHumillacion)
                    .OrderBy(p => p.MatchId)
                    .ToList();

                foreach (var pick in humillacionPicks)
                {
                    if (!matchesById.TryGetValue(pick.MatchId, out var match)) continue;
                    var homeAbbr = match.HomeTeam?.Abbreviation ?? "LOC";
                    var awayAbbr = match.AwayTeam?.Abbreviation ?? "VIS";
                    var homeName = match.HomeTeam?.Name ?? homeAbbr;
                    var awayName = match.AwayTeam?.Name ?? awayAbbr;
                    var score = $"{match.HomeScore}-{match.AwayScore}";
                    int diff = Math.Abs(match.HomeScore - match.AwayScore);

                    details.Add(new AwardMatchDetailDto
                    {
                        MatchId = match.Id,
                        MatchTitle = $"{homeName} vs {awayName}",
                        TeamsAbbr = $"{homeAbbr} vs {awayAbbr}",
                        Score = score,
                        PickAbbr = pick.PickAbbr ?? "",
                        DetailText = $"{homeAbbr} {score} {awayAbbr} (pronóstico {pick.PickAbbr})",
                        ContextText = $"Goleada: diferencia de {diff} goles contra su pronóstico"
                    });
                }
                break;
            }
            case "SOMNIFERO":
            {
                var somniferoPicks = memberPicks
                    .Where(p => p.IsSomnifero)
                    .OrderBy(p => p.MatchId)
                    .ToList();

                foreach (var pick in somniferoPicks)
                {
                    if (!matchesById.TryGetValue(pick.MatchId, out var match)) continue;
                    var homeAbbr = match.HomeTeam?.Abbreviation ?? "LOC";
                    var awayAbbr = match.AwayTeam?.Abbreviation ?? "VIS";
                    var homeName = match.HomeTeam?.Name ?? homeAbbr;
                    var awayName = match.AwayTeam?.Name ?? awayAbbr;

                    details.Add(new AwardMatchDetailDto
                    {
                        MatchId = match.Id,
                        MatchTitle = $"{homeName} vs {awayName}",
                        TeamsAbbr = $"{homeAbbr} vs {awayAbbr}",
                        Score = "0-0",
                        PickAbbr = pick.PickAbbr ?? "",
                        DetailText = $"{homeAbbr} 0-0 {awayAbbr} (pronóstico {pick.PickAbbr})",
                        ContextText = "Apostó a ganador en partido que terminó 0-0"
                    });
                }
                break;
            }
        }

        return details;
    }

    public static string FormatEnrichedNotes(
        string awardType,
        string memberAlias,
        int awardValue,
        List<AwardMatchDetailDto> details,
        string baseNoteTemplate)
    {
        string header = string.Format(baseNoteTemplate, memberAlias, awardValue);
        if (details.Count == 0) return header;

        string matchDetails = string.Join("; ", details.Select(d => d.DetailText));
        string full = $"{header}. Partidos: {matchDetails}";
        return full.Length <= 500 ? full : $"{full[..497]}...";
    }
}
