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

    // Ausente o vacío = encendido (producción no cambia). Si la clave existe, solo un "true" explícito lo enciende:
    // un valor que no se pueda interpretar ("no", "0", "off") lo apaga en vez de dejar el worker corriendo.
    private static bool IsEnabled(IConfiguration configuration, string key)
    {
        var raw = configuration[key];
        if (string.IsNullOrWhiteSpace(raw)) return true;
        return bool.TryParse(raw, out var enabled) && enabled;
    }
}
