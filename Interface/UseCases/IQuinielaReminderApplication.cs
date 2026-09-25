namespace Interface.UseCases;

public interface IQuinielaReminderApplication
{
    /// <summary>
    /// Procesa y despacha las notificaciones push diarias según el día de la semana y la hora local (CST / México):
    /// - Lunes (12:00 PM CST): Apertura de la siguiente jornada disponible.
    /// - Martes a Viernes (11:00 AM CST): Recordatorio exclusivo a participantes con picks incompletos.
    /// - Días de partido (10:30 AM CST): Cartelera matutina de los juegos de la fecha.
    /// </summary>
    /// <param name="simulatedNowUtc">Permite simular una fecha/hora específica para pruebas unitarias o manuales.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Cantidad total de notificaciones despachadas en el ciclo.</returns>
    Task<int> ProcessDailyRemindersAsync(DateTime? simulatedNowUtc = null, CancellationToken ct = default);
}
