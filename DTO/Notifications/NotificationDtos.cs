using System.Text.Json.Serialization;

namespace DTO.Notifications;

public class PushSubscriptionRequestDto
{
    [JsonPropertyName("endpoint")]
    public string Endpoint { get; set; } = string.Empty;

    [JsonPropertyName("p256dhKey")]
    public string? P256dhKey { get; set; }

    [JsonPropertyName("authKey")]
    public string? AuthKey { get; set; }

    [JsonPropertyName("deviceDescription")]
    public string? DeviceDescription { get; set; }

    // Soporte para formato nativo de PushSubscription del navegador (keys: { p256dh, auth })
    [JsonPropertyName("keys")]
    public PushSubscriptionKeysDto? Keys { get; set; }

    public string GetP256dh() => !string.IsNullOrWhiteSpace(P256dhKey) ? P256dhKey : Keys?.P256dh ?? string.Empty;
    public string GetAuth() => !string.IsNullOrWhiteSpace(AuthKey) ? AuthKey : Keys?.Auth ?? string.Empty;
}

public class PushSubscriptionKeysDto
{
    [JsonPropertyName("p256dh")]
    public string P256dh { get; set; } = string.Empty;

    [JsonPropertyName("auth")]
    public string Auth { get; set; } = string.Empty;
}

public class UnsubscribePushRequestDto
{
    [JsonPropertyName("endpoint")]
    public string Endpoint { get; set; } = string.Empty;
}

public class VapidPublicKeyDto
{
    [JsonPropertyName("publicKey")]
    public string PublicKey { get; set; } = string.Empty;
}

public record PushNotificationPayload(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("url")] string? Url = null,
    [property: JsonPropertyName("icon")] string? Icon = "/icon-192.png",
    [property: JsonPropertyName("badge")] string? Badge = "/icon-192.png",
    [property: JsonPropertyName("data")] object? Data = null
);
