using Domain.Entities;

namespace UseCases.Scoring;

/// <summary>
/// Determina la temporada en juego de una quiniela mientras no exista Quiniela.SeasonId (SPEC-020):
/// es la temporada del partido más reciente con picks en esa quiniela.
/// </summary>
public static class QuinielaSeasonResolver
{
    /// <returns>El SeasonId en juego, o null si la quiniela aún no tiene picks.</returns>
    public static int? ResolveActiveSeasonId(IEnumerable<Pick> quinielaPicks)
    {
        return quinielaPicks
            .Where(p => p.Match?.Week != null)
            .OrderByDescending(p => p.Match.DateUtc)
            .Select(p => (int?)p.Match.Week.SeasonId)
            .FirstOrDefault();
    }

    /// <summary>
    /// Una jornada aplica a la quiniela si es de su temporada en juego, o si la quiniela aún no tiene picks.
    /// </summary>
    public static bool AppliesToWeek(IEnumerable<Pick> quinielaPicks, Week week)
    {
        var activeSeasonId = ResolveActiveSeasonId(quinielaPicks);
        return activeSeasonId == null || activeSeasonId == week.SeasonId;
    }
}
