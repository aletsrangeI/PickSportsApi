using Domain.Entities;

namespace Interface.Persistence;

public interface IDuelRepository : IGenericRepository<DuelFixture>
{
    /// <summary>Todos los duelos de la quiniela (solo lectura, con miembros y usuarios), ordenados por WeekNumber y Round.</summary>
    Task<IEnumerable<DuelFixture>> GetFixturesAsync(int quinielaId);

    /// <summary>Duelos de una fecha (rastreados, para poder resolverlos con UpdateFixtureAsync).</summary>
    Task<IEnumerable<DuelFixture>> GetFixturesByWeekNumberAsync(int quinielaId, int weekNumber);

    Task<DuelFixture?> GetFixtureAsync(int fixtureId);
    Task<bool> InsertFixturesAsync(IEnumerable<DuelFixture> fixtures);
    Task<bool> UpdateFixtureAsync(DuelFixture fixture);
    Task DeleteFixturesAsync(int quinielaId);
    Task<bool> AnyResolvedAsync(int quinielaId);

    Task<QuinielaDuelSeason?> GetSeasonInfoAsync(int quinielaId);
    Task<bool> UpsertSeasonInfoAsync(QuinielaDuelSeason info);

    /// <summary>Temporadas de duelos ya cerradas (con campeon) de las quinielas indicadas, con quiniela, temporada y campeon.</summary>
    Task<IEnumerable<QuinielaDuelSeason>> GetClosedSeasonInfosAsync(IEnumerable<int> quinielaIds);
}
