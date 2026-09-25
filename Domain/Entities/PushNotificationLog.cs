using Domain.Common;

namespace Domain.Entities;

/// <summary>
/// Registro de auditoría e idempotencia para notificaciones push enviadas.
/// Evita envíos duplicados en un mismo día natural o para una misma jornada.
/// </summary>
public class PushNotificationLog : BaseEntity
{
    /// <summary>
    /// Tipo de notificación: WEEK_OPENED, INCOMPLETE_PICKS_REMINDER, MATCHDAY_MORNING_ROUNDUP, etc.
    /// </summary>
    public string NotificationType { get; set; } = null!;

    /// <summary>
    /// ID de la jornada relacionada.
    /// </summary>
    public int WeekId { get; set; }

    /// <summary>
    /// ID de la quiniela relacionada.
    /// </summary>
    public int QuinielaId { get; set; }

    /// <summary>
    /// ID del usuario destinatario (opcional si fue broadcast general a toda la quiniela).
    /// </summary>
    public int? UserId { get; set; }

    /// <summary>
    /// Fecha local de envío en formato YYYY-MM-DD (hora de México) para control de idempotencia diaria.
    /// </summary>
    public string DateLocal { get; set; } = null!;

    /// <summary>
    /// Título de la notificación enviada.
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Mensaje de la notificación enviada.
    /// </summary>
    public string Message { get; set; } = null!;

    /// <summary>
    /// Cantidad de dispositivos a los que se entregó el push.
    /// </summary>
    public int DeliveredCount { get; set; }

    /// <summary>
    /// Fecha y hora UTC en que se despachó.
    /// </summary>
    public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;

    // Navegación
    public Week Week { get; set; } = null!;
    public Quiniela Quiniela { get; set; } = null!;
    public User? User { get; set; }
}
