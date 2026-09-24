using Domain.Common;

namespace Domain.Entities;

public class PushSubscription : BaseEntity
{
    public int UserId { get; set; }
    public string Endpoint { get; set; } = null!;
    public string P256dhKey { get; set; } = null!;
    public string AuthKey { get; set; } = null!;
    public string? DeviceDescription { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastNotifiedUtc { get; set; }

    public bool IsActive
    {
        get => Active;
        set => Active = value;
    }

    public User User { get; set; } = null!;
}
