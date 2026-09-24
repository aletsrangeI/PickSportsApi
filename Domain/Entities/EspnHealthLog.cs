using Domain.Common;

namespace Domain.Entities;

public class EspnHealthLog : BaseEntity
{
    public string EndpointTested { get; set; } = null!;
    public int HttpStatusCode { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public int LatencyMs { get; set; }
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
}
