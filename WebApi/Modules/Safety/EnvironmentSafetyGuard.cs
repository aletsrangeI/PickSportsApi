using Microsoft.Extensions.Configuration;
using Npgsql;
using Persistence;

namespace WebApi.Modules.Safety;

/// <summary>
/// Guardia de arranque (SPEC-023): impide que una API no productiva use la base de producción
/// y que producción arranque sin sus secretos o sin la señal explícita de producción.
/// </summary>
public static class EnvironmentSafetyGuard
{
    public const string DefaultProductionDatabaseName = "picksports";

    private const string ProductionEnvironment = "Production";

    /// <summary>
    /// Devuelve la lista de errores; vacía significa seguro. Sin I/O salvo leer variables de entorno
    /// (inyectables con <paramref name="getEnvironmentVariable"/> para las pruebas).
    /// </summary>
    public static IReadOnlyList<string> Validate(
        IConfiguration configuration,
        string? environmentName,
        Func<string, string?>? getEnvironmentVariable = null)
    {
        var errors = new List<string>();
        var isProduction = IsProduction(environmentName);

        // R1
        var connectionString = ConnectionStringResolver.Resolve(configuration, getEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            errors.Add(MissingConfiguration("ConnectionStrings:PickSports"));
        }

        // R2
        if (string.IsNullOrWhiteSpace(configuration["Config:Secret"]))
        {
            errors.Add(MissingConfiguration("Config:Secret"));
        }

        // R3
        if (!isProduction && !string.IsNullOrWhiteSpace(connectionString))
        {
            var productionDatabase = configuration["Safety:ProductionDatabaseName"];
            if (string.IsNullOrWhiteSpace(productionDatabase))
            {
                productionDatabase = DefaultProductionDatabaseName;
            }

            if (TryGetDatabaseName(connectionString, out var database)
                && string.Equals(database, productionDatabase, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"EnvironmentSafety: un entorno no productivo no puede usar la base de producción '{productionDatabase}'.");
            }
        }

        if (isProduction)
        {
            // R4
            var watchDogPassword = configuration["WatchDog:WatchPagePassword"];
            if (string.IsNullOrWhiteSpace(watchDogPassword)
                || string.Equals(watchDogPassword.Trim(), "admin", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("EnvironmentSafety: WatchDog:WatchPagePassword no puede estar vacío ni ser 'admin' en producción.");
            }

            // R5
            if (!bool.TryParse(configuration["Safety:AllowProduction"], out var allowProduction) || !allowProduction)
            {
                errors.Add("EnvironmentSafety: el entorno Production requiere 'Safety:AllowProduction=true'.");
            }
        }

        return errors;
    }

    /// <summary>Lanza <see cref="InvalidOperationException"/> con todos los errores unidos por salto de línea.</summary>
    public static void EnsureSafe(
        IConfiguration configuration,
        string? environmentName,
        Func<string, string?>? getEnvironmentVariable = null)
    {
        var errors = Validate(configuration, environmentName, getEnvironmentVariable);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join("\n", errors));
        }
    }

    // .NET resuelve un ASPNETCORE_ENVIRONMENT ausente a "Production": se trata igual.
    private static bool IsProduction(string? environmentName) =>
        string.IsNullOrWhiteSpace(environmentName)
        || string.Equals(environmentName.Trim(), ProductionEnvironment, StringComparison.OrdinalIgnoreCase);

    private static string MissingConfiguration(string key) =>
        $"EnvironmentSafety: falta la configuración obligatoria '{key}'.";

    private static bool TryGetDatabaseName(string connectionString, out string? database)
    {
        try
        {
            database = new NpgsqlConnectionStringBuilder(connectionString).Database;
            return !string.IsNullOrWhiteSpace(database);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            database = null;
            return false;
        }
    }
}
