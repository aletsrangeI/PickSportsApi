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

        _isEnabled = !string.IsNullOrWhiteSpace(_publicKey) && !string.IsNullOrWhiteSpace(_privateKey);

        if (!_isEnabled)
        {
            _logger.LogWarning("[WebPush] VAPID keys no están configuradas en appsettings. Las notificaciones push estarán deshabilitadas.");
        }
    }

    public string GetVapidPublicKey() => _publicKey;

    public async Task<int> SendNotificationToUserAsync(int userId, PushNotificationPayload payload, CancellationToken ct = default)
    {
        if (!_isEnabled) return 0;

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

        var subscriptions = (await _unitOfWork.PushSubscriptions.GetActiveSubscriptionsByQuinielaAsync(quinielaId)).ToList();
        if (subscriptions.Count == 0) return 0;

        return await SendToSubscriptionsAsync(subscriptions, payload, ct);
    }

    public async Task<bool> SendTestNotificationAsync(int userId, CancellationToken ct = default)
    {
        var payload = new PushNotificationPayload(
            Title: "PickSports Test 🚀",
            Message: "¡Tus notificaciones Web Push están activadas y funcionando con éxito!",
            Url: "/",
            Data: new { type = "test", sentAt = DateTime.UtcNow }
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
