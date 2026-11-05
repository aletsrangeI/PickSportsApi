using WebApi.BackgroundServices;

namespace WebApi.Modules.Workers;

/// <summary>
/// Registra los background workers según configuración (SPEC-023).
/// Una clave ausente significa encendido, de modo que producción conserva su comportamiento.
/// </summary>
public static class BackgroundWorkersExtensions
{
    public const string EspnLiveScoreKey = "BackgroundWorkers:EspnLiveScore:Enabled";
    public const string QuinielaReminderKey = "BackgroundWorkers:QuinielaReminder:Enabled";

    public static IServiceCollection AddBackgroundWorkers(this IServiceCollection services, IConfiguration configuration)
    {
        if (IsEnabled(configuration, EspnLiveScoreKey))
        {
            services.AddHostedService<EspnLiveScoreBackgroundWorker>();
        }

        if (IsEnabled(configuration, QuinielaReminderKey))
        {
            services.AddHostedService<QuinielaReminderBackgroundWorker>();
        }

        return services;
    }

    // Ausente o no interpretable = encendido; solo un "false" explícito lo apaga.
    private static bool IsEnabled(IConfiguration configuration, string key) =>
        !bool.TryParse(configuration[key], out var enabled) || enabled;
}
