namespace DTO.Auth;

public class ClaimInfoDto
{
    public string Alias { get; set; } = null!;
    public string QuinielaName { get; set; } = null!;
    public int QuinielaId { get; set; }
    public int TotalHits { get; set; }
    public int TotalUpsets { get; set; }
    public int CurrentStreak { get; set; }
    public string EmailPlaceholder { get; set; } = null!;
    public bool IsAlreadyClaimed { get; set; }
}
