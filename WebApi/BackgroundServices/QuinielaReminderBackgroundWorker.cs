using Common;
using Interface.UseCases;

namespace WebApi.BackgroundServices;

/// <summary>
/// Worker en segundo plano que supervisa el calendario semanal y despacha recordatorios inteligentes:
/// - Lunes (12:00 PM CST): Apertura de la siguiente jornada.
/// - Martes a Viernes (11:00 AM CST): Recordatorio a quienes no han completado sus pronósticos.
/// - Días de partido (10:30 AM CST): Cartelera matutina de juegos del día.
/// Se ejecuta periódicamente cada 15 minutos de forma idempotente con protección anti-spam.
/// </summary>
public class QuinielaReminderBackgroundWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAppLogger<QuinielaReminderBackgroundWorker> _logger;

    public QuinielaReminderBackgroundWorker(
        IServiceScopeFactory scopeFactory,
        IAppLogger<QuinielaReminderBackgroundWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[QuinielaReminderWorker] Iniciando servicio en segundo plano de recordatorios de quiniela.");

        // Retardo inicial de 20 segundos para permitir que la app y la BD completen su inicio
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var reminderApp = scope.ServiceProvider.GetRequiredService<IQuinielaReminderApplication>();

                await reminderApp.ProcessDailyRemindersAsync(null, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError("[QuinielaReminderWorker] Error en ciclo de recordatorios: {0}\n{1}", ex.Message, ex.StackTrace ?? "");
            }

            // Reposo de 15 minutos entre comprobaciones
            await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
        }

        _logger.LogInformation("[QuinielaReminderWorker] Deteniendo servicio de recordatorios.");
    }
}
