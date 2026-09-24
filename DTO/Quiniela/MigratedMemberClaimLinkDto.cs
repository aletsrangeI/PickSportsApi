namespace DTO.Quiniela;

public class MigratedMemberClaimLinkDto
{
    public int MemberId { get; set; }
    public int UserId { get; set; }
    public string Alias { get; set; } = null!;
    public string Email { get; set; } = null!;
    public int TotalHits { get; set; }
    public bool IsClaimed { get; set; }
    public string? ClaimToken { get; set; }
    public string? ClaimUrl { get; set; }
    public string? ShareMessage { get; set; }
}
