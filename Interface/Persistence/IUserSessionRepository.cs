using Domain.Entities;

namespace Interface.Persistence;

public interface IUserSessionRepository : IGenericRepository<UserSession>
{
    /// <summary>Sesion cuyo refresh token vigente o anterior es ese valor (la regla de gracia la decide el caso de uso).</summary>
    Task<UserSession?> GetByRefreshTokenAsync(string refreshToken);

    /// <summary>Conserva las <paramref name="maxSessions"/> sesiones mas recientes del usuario y borra el resto y las vencidas.</summary>
    Task PruneAsync(int userId, int maxSessions, DateTime nowUtc);
}
