using System.Net;
using System.Text.Json;
using Common;
using DTO.Notifications;
using Interface.Persistence;
using Interface.UseCases;
using Microsoft.Extensions.Configuration;
using WebPush;

namespace UseCases.Notifications;

public class WebPushNotificationService : IWebPushNotificationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;
    private readonly IAppLogger<WebPushNotificationService> _logger;

    private readonly string _subject;
    private readonly string _publicKey;
    private readonly string _privateKey;
    private readonly bool _isEnabled;

    // SPEC-023: lista de usuarios permitidos para push. Sin la clave = sin filtro (producción). Si la clave está
    // configurada pero no produce ningún id válido, no se envía a nadie (falla cerrado).
    private readonly HashSet<int> _allowedUserIds;
    private readonly bool _allowlistConfigured;

    public WebPushNotificationService(
        IUnitOfWork unitOfWork,
        IConfiguration configuration,
        IAppLogger<WebPushNotificationService> logger)
    {
        _unitOfWork = unitOfWork;
        _configuration = configuration;
        _logger = logger;

        _subject = _configuration["Vapid:Subject"] ?? "mailto:admin@picksports.orionsys.net";
        _publicKey = _configuration["Vapid:PublicKey"] ?? string.Empty;
        _privateKey = _configuration["Vapid:PrivateKey"] ?? string.Empty;

        // Clave ausente o vacía = encendido, para que producción se comporte igual que antes.
        // Si la clave existe, solo un "true" explícito lo enciende: un valor no interpretable lo apaga.
        var rawPushEnabled = _configuration["Push:Enabled"];
        var pushEnabledByConfig = string.IsNullOrWhiteSpace(rawPushEnabled)
            || (bool.TryParse(rawPushEnabled, out var pushEnabled) && pushEnabled);
        var hasVapidKeys = !string.IsNullOrWhiteSpace(_publicKey) && !string.IsNullOrWhiteSpace(_privateKey);

        _isEnabled = pushEnabledByConfig && hasVapidKeys;
        (_allowedUserIds, _allowlistConfigured) = ReadAllowedUserIds(_configuration);

        if (_allowlistConfigured && _allowedUserIds.Count == 0)
        {
            _logger.LogWarning("[WebPush] Push:AllowedUserIds está configurada pero no contiene ningún id válido: no se enviará push a nadie.");
        }

        if (!pushEnabledByConfig)
        {
            _logger.LogInformation("[WebPush] Push deshabilitado por configuración (Push:Enabled=false).");
        }
        else if (!hasVapidKeys)
        {
            _logger.LogWarning("[WebPush] VAPID keys no están configuradas en appsettings. Las notificaciones push estarán deshabilitadas.");
        }
    }

    private static (HashSet<int> Ids, bool Configured) ReadAllowedUserIds(IConfiguration configuration)
    {
        var ids = new HashSet<int>();
        var configured = false;
        var section = configuration.GetSection("Push:AllowedUserIds");
        if (section is null) return (ids, configured);

        // Admite el formato de arreglo (Push:AllowedUserIds:0=1) y una lista separada por comas.
        foreach (var child in section.GetChildren())
        {
            if (string.IsNullOrWhiteSpace(child.Value)) continue;
            configured = true;
            if (int.TryParse(child.Value, out var id)) ids.Add(id);
        }

        if (!string.IsNullOrWhiteSpace(section.Value))
        {
            configured = true;
            foreach (var part in section.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (int.TryParse(part, out var id)) ids.Add(id);
            }
        }

        return (ids, configured);
    }

    private bool IsUserAllowed(int userId) => !_allowlistConfigured || _allowedUserIds.Contains(userId);

    public string GetVapidPublicKey() => _publicKey;

    public async Task<int> SendNotificationToUserAsync(int userId, PushNotificationPayload payload, CancellationToken ct = default)
    {
        if (!_isEnabled) return 0;
        if (!IsUserAllowed(userId)) return 0;

        var subscriptions = (await _unitOfWork.PushSubscriptions.GetActiveSubscriptionsByUserAsync(userId)).ToList();
        if (subscriptions.Count == 0) return 0;

        return await SendToSubscriptionsAsync(subscriptions, payload, ct);
    }

    public async Task<int> SendNotificationToUsersAsync(IEnumerable<int> userIds, PushNotificationPayload payload, CancellationToken ct = default)
    {
        if (!_isEnabled) return 0;

        var sentCount = 0;
        foreach (var userId in userIds.Distinct())
        {
            sentCount += await SendNotificationToUserAsync(userId, payload, ct);
        }
        return sentCount;
    }

    public async Task<int> SendNotificationToQuinielaAsync(int quinielaId, PushNotificationPayload payload, CancellationToken ct = default)
    {
        if (!_isEnabled) return 0;

        var subscriptions = (await _unitOfWork.PushSubscriptions.GetActiveSubscriptionsByQuinielaAsync(quinielaId))
            .Where(s => IsUserAllowed(s.UserId))
            .ToList();
        if (subscriptions.Count == 0) return 0;

        return await SendToSubscriptionsAsync(subscriptions, payload, ct);
    }

    public async Task<bool> SendTestNotificationAsync(int userId, CancellationToken ct = default)
    {
        var payload = new PushNotificationPayload(
            Title: "⚽ ¡Notificaciones activadas!",
            Message: "Todo listo en PickSports. Recibirás avisos de inicio de jornada, marcadores en vivo al terminar tus partidos y la tabla de posiciones.",
            Url: "/",
            Data: new { type = "activation", sentAt = DateTime.UtcNow }
        );

        var sentCount = await SendNotificationToUserAsync(userId, payload, ct);
        return sentCount > 0;
    }

    private async Task<int> SendToSubscriptionsAsync(
        IEnumerable<Domain.Entities.PushSubscription> subscriptions,
        PushNotificationPayload payload,
        CancellationToken ct)
    {
        var payloadJson = JsonSerializer.Serialize(payload);
        var vapidDetails = new VapidDetails(_subject, _publicKey, _privateKey);
        var client = new WebPushClient();
        var sentCount = 0;

        foreach (var sub in subscriptions)
        {
            try
            {
                var pushSubscription = new WebPush.PushSubscription(sub.Endpoint, sub.P256dhKey, sub.AuthKey);
                await client.SendNotificationAsync(pushSubscription, payloadJson, vapidDetails, ct);

                sub.LastNotifiedUtc = DateTime.UtcNow;
                await _unitOfWork.PushSubscriptions.UpdateAsync(sub);
                sentCount++;
            }
            catch (WebPushException ex) when (ex.StatusCode == HttpStatusCode.Gone || ex.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning("[WebPush] Endpoint expirado o revocado ({0}). Desactivando suscripción ID={1}.", ex.StatusCode, sub.Id);
                await _unitOfWork.PushSubscriptions.DeactivateEndpointAsync(sub.Endpoint);
            }
            catch (Exception ex)
            {
                _logger.LogError("[WebPush] Error enviando push a suscripción ID={0} (Endpoint={1}): {2}", sub.Id, sub.Endpoint, ex.Message);
            }
        }

        if (sentCount > 0)
        {
            await _unitOfWork.Save(ct);
        }

        return sentCount;
    }
}
