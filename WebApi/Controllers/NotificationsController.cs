using System.Security.Claims;
using DTO.Notifications;
using Interface.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationsApplication _notificationsApplication;
    private readonly IQuinielaReminderApplication _reminderApplication;

    public NotificationsController(
        INotificationsApplication notificationsApplication,
        IQuinielaReminderApplication reminderApplication)
    {
        _notificationsApplication = notificationsApplication;
        _reminderApplication = reminderApplication;
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                    ?? User.FindFirst(ClaimTypes.Name)?.Value;
        return int.TryParse(claim, out var id) ? id : 0;
    }

    /// <summary>
    /// GET /api/notifications/public-key
    /// Obtiene la clave pública VAPID para suscribir el navegador/dispositivo.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("public-key")]
    public async Task<IActionResult> GetPublicKey()
    {
        var response = await _notificationsApplication.GetVapidPublicKeyAsync();
        if (!response.isSuccess)
        {
            return StatusCode(503, response);
        }
        return Ok(response);
    }

    /// <summary>
    /// POST /api/notifications/subscribe
    /// Registra o actualiza la suscripción push para el usuario autenticado.
    /// </summary>
    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe([FromBody] PushSubscriptionRequestDto request)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        var response = await _notificationsApplication.SubscribeAsync(userId, request);
        if (!response.isSuccess)
        {
            return BadRequest(response);
        }
        return Ok(response);
    }

    /// <summary>
    /// POST /api/notifications/unsubscribe
    /// Desactiva una suscripción push existente.
    /// </summary>
    [HttpPost("unsubscribe")]
    public async Task<IActionResult> Unsubscribe([FromBody] UnsubscribePushRequestDto request)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        var response = await _notificationsApplication.UnsubscribeAsync(userId, request);
        return Ok(response);
    }

    /// <summary>
    /// POST /api/notifications/test
    /// Envía una notificación push de prueba al usuario autenticado.
    /// </summary>
    [HttpPost("test")]
    public async Task<IActionResult> SendTest()
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        var response = await _notificationsApplication.SendTestNotificationAsync(userId);
        if (!response.isSuccess)
        {
            return BadRequest(response);
        }
        return Ok(response);
    }

    /// <summary>
    /// POST /api/notifications/trigger-reminders
    /// Ejecuta de inmediato el ciclo de evaluación y despacho de recordatorios programados (Lunes apertura, Martes-Viernes rezagados, Días de partido).
    /// </summary>
    [HttpPost("trigger-reminders")]
    public async Task<IActionResult> TriggerReminders([FromQuery] DateTime? simulatedUtc = null)
    {
        var sentCount = await _reminderApplication.ProcessDailyRemindersAsync(simulatedUtc);
        return Ok(new
        {
            isSuccess = true,
            message = $"Ciclo de recordatorios ejecutado. Se despacharon {sentCount} notificaciones.",
            sentCount
        });
    }
}
