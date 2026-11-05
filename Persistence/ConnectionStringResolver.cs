using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Persistence;

/// <summary>
/// Única fuente de verdad de la cadena de conexión: la usan el DbContext y la guardia de arranque,
/// de modo que la guardia valida exactamente la cadena que se va a usar.
/// </summary>
public static class ConnectionStringResolver
{
    /// <summary>
    /// Devuelve la primera cadena no vacía, en este orden: variable de entorno ConnectionStrings__PickSports,
    /// ConnectionStrings__picksports_db, configuración "PickSports", "picksports_db",
    /// "ConnectionStrings:PickSports" y "ConnectionStrings:picksports_db". Null si ninguna está definida.
    /// </summary>
    /// <param name="getEnvironmentVariable">Permite aislar las pruebas de las variables reales de la máquina.</param>
    public static string? Resolve(IConfiguration configuration, Func<string, string?>? getEnvironmentVariable = null)
    {
        getEnvironmentVariable ??= Environment.GetEnvironmentVariable;

        return new[]
        {
            getEnvironmentVariable("ConnectionStrings__PickSports"),
            getEnvironmentVariable("ConnectionStrings__picksports_db"),
            configuration.GetConnectionString("PickSports"),
            configuration.GetConnectionString("picksports_db"),
            configuration["ConnectionStrings:PickSports"],
            configuration["ConnectionStrings:picksports_db"]
        }.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));
    }

    /// <summary>
    /// Nombre de la base a la que se conectaría la cadena. Npgsql usa el nombre de usuario como base cuando
    /// no se indica <c>Database</c>, así que ese es el valor efectivo. False si la cadena no se puede interpretar.
    /// </summary>
    public static bool TryGetDatabaseName(string connectionString, out string? database)
    {
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            database = !string.IsNullOrWhiteSpace(builder.Database) ? builder.Database : builder.Username;
            return !string.IsNullOrWhiteSpace(database);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            database = null;
            return false;
        }
    }
}
