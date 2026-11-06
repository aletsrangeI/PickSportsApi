using Domain.Entities;
using Interface.Persistence;

namespace UseCases.Scoring;

/// <summary>
/// Determina la temporada de una quiniela (SPEC-020). Si la quiniela tiene <c>SeasonId</c> manda ese valor;
/// mientras sea null (Eventos Personalizados, liga sin temporadas) se conserva la heurística anterior:
/// la temporada del partido más reciente con picks en esa quiniela.
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
    /// Heurística legada: una jornada aplica a la quiniela si es de su temporada en juego, o si la quiniela aún no tiene picks.
    /// </summary>
    public static bool AppliesToWeek(IEnumerable<Pick> quinielaPicks, Week week)
    {
        var activeSeasonId = ResolveActiveSeasonId(quinielaPicks);
        return activeSeasonId == null || activeSeasonId == week.SeasonId;
    }

    /// <summary>
    /// Una jornada aplica a la quiniela si coincide con <c>Quiniela.SeasonId</c>; con SeasonId null usa la heurística legada.
    /// </summary>
    public static bool AppliesToWeek(Quiniela quiniela, IEnumerable<Pick> quinielaPicks, Week week)
    {
        if (quiniela.SeasonId.HasValue)
        {
            return quiniela.SeasonId.Value == week.SeasonId;
        }

        return AppliesToWeek(quinielaPicks, week);
    }

    /// <summary>
    /// Asigna de forma perezosa la temporada vigente de la liga a una quiniela deportiva activa que quedó sin <c>SeasonId</c>
    /// (se creó cuando su liga aún no tenía temporada). Es seguro: sin temporada no pudo haber jornadas ni picks.
    /// </summary>
    /// <returns>La temporada asignada, o null si no había nada que asignar.</returns>
    public static async Task<Season?> EnsureSeasonAssignedAsync(IUnitOfWork unitOfWork, Quiniela quiniela)
    {
        if (quiniela.SeasonId.HasValue
            || !quiniela.LeagueId.HasValue
            || !quiniela.IsActive
            || quiniela.QuinielaType != Domain.Enums.QuinielaType.Sports)
        {
            return null;
        }

        var current = await unitOfWork.Seasons.GetCurrentSeasonAsync(quiniela.LeagueId.Value);
        if (current == null)
        {
            return null;
        }

        quiniela.SeasonId = current.Id;
        quiniela.Season = current;
        await unitOfWork.Quinielas.UpdateAsync(quiniela);
        return current;
    }

    /// <summary>
    /// Temporada de la quiniela: <c>SeasonId</c> si existe; si no, la vigente de su liga; null si no tiene liga.
    /// </summary>
    public static async Task<int?> ResolveSeasonIdAsync(IUnitOfWork unitOfWork, Quiniela quiniela)
    {
        if (quiniela.SeasonId.HasValue)
        {
            return quiniela.SeasonId.Value;
        }

        if (!quiniela.LeagueId.HasValue)
        {
            return null;
        }

        var current = await unitOfWork.Seasons.GetCurrentSeasonAsync(quiniela.LeagueId.Value);
        return current?.Id;
    }
}
