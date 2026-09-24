namespace DTO.Auth;

public class UnclaimedQuinielaMembersDto
{
    public int QuinielaId { get; set; }
    public string QuinielaName { get; set; } = null!;
    public List<UnclaimedMemberItemDto> Members { get; set; } = new();
}

public class UnclaimedMemberItemDto
{
    public int MemberId { get; set; }
    public int UserId { get; set; }
    public string Alias { get; set; } = null!;
    public int TotalHits { get; set; }
    public int TotalUpsets { get; set; }
    public int CurrentStreak { get; set; }
    public string ClaimToken { get; set; } = null!;
}
