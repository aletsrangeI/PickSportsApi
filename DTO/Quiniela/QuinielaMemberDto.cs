namespace DTO.Quiniela;

public class QuinielaMemberDto
{
    public int Id { get; set; }
    public int QuinielaId { get; set; }
    public int UserId { get; set; }
    public string Alias { get; set; } = null!;
    public string Role { get; set; } = "MEMBER";
    public bool PaidFee { get; set; }
    public int TotalHits { get; set; }
    public int TotalUpsets { get; set; }
    public int TotalHumillaciones { get; set; }
    public int CurrentStreak { get; set; }
    public int BestStreak { get; set; }
    public DateTime JoinedAt { get; set; }
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
}
