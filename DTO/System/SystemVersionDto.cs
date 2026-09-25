namespace DTO.System;

public class SystemVersionDto
{
    public string ApiVersion { get; set; } = "1.0.0";
    public string LatestClientVersion { get; set; } = "1.0.0";
    public string MinSupportedClientVersion { get; set; } = "1.0.0";
    public bool MaintenanceMode { get; set; } = false;
    public string? MaintenanceMessage { get; set; }
    public DateTime ServerTimeUtc { get; set; } = DateTime.UtcNow;
}
