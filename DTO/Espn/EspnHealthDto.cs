namespace DTO.Espn;

public class EspnHealthDto
{
    public string Status { get; set; } = null!; // "GREEN" | "YELLOW" | "RED"
    public bool IsSuccess { get; set; }
    public int LatencyMs { get; set; }
    public string EndpointTested { get; set; } = null!;
    public DateTime CheckedAt { get; set; }
    public string? ErrorMessage { get; set; }
}
