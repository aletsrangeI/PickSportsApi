using Domain.Entities;
using Domain.Gamification;
using UseCases.Scoring;

namespace UseCases.Gamification;

/// <summary>
/// SPEC-013: motor puro que decide que trofeos del catalogo cumple cada miembro con los picks de UNA temporada.
/// No toca repositorios. COLECCIONISTA_DE_BRONCE no se evalua aqui (depende de lo ya otorgado): lo resuelve la aplicacion.
/// </summary>
public class AchievementEvaluator
{
    private readonly ScoringEngine _scoringEngine;

    public AchievementEvaluator(ScoringEngine? scoringEngine = null)
    {
        _scoringEngine = scoringEngine ?? new ScoringEngine();
    }

    /// <param name="members">Miembros activos de la quiniela.</param>
    /// <param name="seasonPicks">Picks de la quiniela de UNA sola temporada (con Match cargado).</param>
    /// <param name="seasonAwards">Galardones de la quiniela (se filtran por las jornadas de la temporada).</param>
    /// <returns>Codigos cumplidos por MemberId (solo miembros que cumplen al menos uno).</returns>
    public Dictionary<int, HashSet<string>> Evaluate(
        IReadOnlyList<QuinielaMember> members,
        IReadOnlyList<Pick> seasonPicks,
        IReadOnlyList<WeeklyAward> seasonAwards,
        IReadOnlyDictionary<int, HashSet<int>>? realMatchIdsByWeek = null)
    {
        var result = new Dictionary<int, HashSet<string>>();

        // Los picks huerfanos (sin Match/Week) no pertenecen a ninguna jornada: no cuentan para nada.
        seasonPicks = seasonPicks.Where(p => WeekKey(p) != 0).ToList();

        // Jornadas con al menos un pick calificado = jornadas calificadas de la temporada.
        var picksByWeek = seasonPicks.GroupBy(WeekKey).ToDictionary(g => g.Key, g => g.ToList());
        var qualifiedWeeks = picksByWeek
            .Where(kv => kv.Value.Any(p => p.IsHit.HasValue))
            .ToDictionary(kv => kv.Key, kv => kv.Value);
        // Partidos REALES de cada jornada (los que carga la aplicacion). Sin ese dato se usan los partidos que tienen picks.
        HashSet<int> RealMatchIds(int weekKey) =>
            realMatchIdsByWeek != null && realMatchIdsByWeek.TryGetValue(weekKey, out var ids)
                ? ids
                : picksByWeek[weekKey].Select(p => p.MatchId).ToHashSet();

        void Add(int memberId, string code)
        {
            if (!result.TryGetValue(memberId, out var set))
            {
                set = new HashSet<string>();
                result[memberId] = set;
            }
            set.Add(code);
        }

        var picksByMember = seasonPicks.GroupBy(p => p.MemberId).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var member in members)
        {
            if (!picksByMember.TryGetValue(member.Id, out var mine)) continue;

            var graded = mine.Where(p => p.IsHit.HasValue).ToList();
            if (graded.Count == 0) continue;

            if (graded.Any(p => p.IsHit == true)) Add(member.Id, AchievementCatalog.PrimeraSangre);
            if (graded.Any(p => p.IsUpsetHit)) Add(member.Id, AchievementCatalog.OlfatoDeSorpresa);
            if (graded.Count(p => p.IsUpsetHit) >= 5) Add(member.Id, AchievementCatalog.CazadorDeSorpresas);

            // Jornadas calificadas en las que el miembro jugo (al menos un pick calificado).
            var memberWeeks = graded.GroupBy(WeekKey).Select(g => g.Key).Where(qualifiedWeeks.ContainsKey).ToList();
            if (memberWeeks.Count >= 5) Add(member.Id, AchievementCatalog.FielAlColiseo);

            int noAutoWeeks = 0;
            foreach (var weekKey in memberWeeks)
            {
                var weekPicks = mine.Where(p => WeekKey(p) == weekKey).ToList();
                bool allManual = weekPicks.All(p => !p.IsAutoFilled);
                if (allManual) noAutoWeeks++;

                var realIds = RealMatchIds(weekKey);
                var pickedIds = weekPicks.Select(p => p.MatchId).ToHashSet();
                bool coversWeek = realIds.Count > 0 && realIds.All(pickedIds.Contains);

                if (allManual && coversWeek) Add(member.Id, AchievementCatalog.JornadaCompleta);

                var gradedWeek = weekPicks.Where(p => p.IsHit.HasValue).ToList();
                var realPicks = weekPicks.Where(p => realIds.Contains(p.MatchId)).ToList();
                if (coversWeek && realIds.Count >= 5 && realPicks.All(p => p.IsHit == true))
                {
                    Add(member.Id, AchievementCatalog.SemanaInvicta);
                }

                // La racha se mide sobre TODOS los picks de la jornada: un pick sin calificar corta la racha.
                if (MaxHitStreak(weekPicks) >= 5) Add(member.Id, AchievementCatalog.RachaDeFuego);
            }

            if (noAutoWeeks >= 10) Add(member.Id, AchievementCatalog.ConstanciaDeAcero);
        }

        // SOLO_YO_LO_VI: partido con >= 4 miembros activos con pick CALIFICADO y exactamente 1 acierto.
        var activeIds = members.Select(m => m.Id).ToHashSet();
        foreach (var matchGroup in seasonPicks.Where(p => activeIds.Contains(p.MemberId) && p.IsHit.HasValue).GroupBy(p => p.MatchId))
        {
            if (matchGroup.Select(p => p.MemberId).Distinct().Count() < 4) continue;
            var hits = matchGroup.Where(p => p.IsHit == true).Select(p => p.MemberId).Distinct().ToList();
            if (hits.Count == 1) Add(hits[0], AchievementCatalog.SoloYoLoVi);
        }

        // EN_EL_PODIO: Top 3 (con al menos un acierto) de alguna jornada calificada.
        foreach (var (_, weekPicks) in qualifiedWeeks)
        {
            var matches = weekPicks
                .Select(p => p.Match)
                .Where(m => m != null)
                .GroupBy(m => m.Id)
                .Select(g => g.First())
                .ToList();
            var standings = _scoringEngine.CalculateWeeklyStandings(members, weekPicks, matches);
            // Ranking de competicion: el umbral son los aciertos de quien ocupa la posicion 3 (o la ultima si hay menos);
            // los empatados en ese umbral tambien llegan al podio.
            var ordered = standings.OrderByDescending(s => s.Hits).ToList();
            if (ordered.Count == 0) continue;
            int threshold = ordered[Math.Min(3, ordered.Count) - 1].Hits;
            foreach (var s in standings.Where(s => s.Hits >= threshold && s.Hits > 0))
            {
                Add(s.MemberId, AchievementCatalog.EnElPodio);
            }
        }

        // TRICAMPEON_DE_JORNADA: >= 3 galardones MVP dentro de las jornadas de la temporada.
        var seasonWeekKeys = picksByWeek.Keys.ToHashSet();
        foreach (var g in seasonAwards
                     .Where(a => a.AwardType == "MVP" && seasonWeekKeys.Contains(a.WeekId))
                     .GroupBy(a => a.MemberId))
        {
            if (g.Select(a => a.WeekId).Distinct().Count() >= 3 && members.Any(m => m.Id == g.Key))
            {
                Add(g.Key, AchievementCatalog.TricampeonDeJornada);
            }
        }

        return result;
    }

    public static int WeekKey(Pick p) => p.Match?.WeekId is > 0 ? p.Match.WeekId : p.Match?.Week?.Id ?? 0;

    private static int MaxHitStreak(IEnumerable<Pick> weekPicks)
    {
        int best = 0, current = 0;
        foreach (var p in weekPicks.OrderBy(p => p.Match?.DateUtc ?? DateTime.MinValue).ThenBy(p => p.MatchId))
        {
            current = p.IsHit == true ? current + 1 : 0;
            if (current > best) best = current;
        }
        return best;
    }
}
