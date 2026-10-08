namespace DTO.Broadcast;

/// <summary>PUT /api/matches/{id}/broadcasters — lista vacía restablece la regla de localía.</summary>
public class UpdateBroadcastersDto
{
    public List<string> Channels { get; set; } = new();
}

public class MatchBroadcastersDto
{
    public int MatchId { get; set; }
    public List<string> Broadcasters { get; set; } = new();
    /// <summary>"RULE" | "LIGAMX" | "MANUAL"</summary>
    public string? Source { get; set; }
}

public class BroadcastSyncResultDto
{
    public int WeekId { get; set; }
    public int DefaultsApplied { get; set; }
    public int OfficialUpdated { get; set; }
    public int ManualPreserved { get; set; }
    /// <summary>Indica si ligamx.net respondió con señales utilizables para la jornada.</summary>
    public bool OfficialSourceAvailable { get; set; }
}

/// <summary>Señales de un partido extraídas del sitio oficial de Liga MX.</summary>
public record ScrapedMatchBroadcast(
    string HomeAbbr,
    string AwayAbbr,
    int? WeekNumber,
    IReadOnlyList<string> Channels);
