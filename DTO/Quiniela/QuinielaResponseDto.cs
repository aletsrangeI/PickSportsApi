namespace DTO.Quiniela;

public class QuinielaResponseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int LeagueId { get; set; }
    public string LeagueCode { get; set; } = null!;
    public string LeagueName { get; set; } = null!;
    public string SportName { get; set; } = null!;
    public string InviteCode { get; set; } = null!;
    public decimal EntryFee { get; set; }
    public decimal FirstPlacePct { get; set; }
    public decimal SecondPlacePct { get; set; }
    public decimal ThirdPlacePct { get; set; }
    public bool IsActive { get; set; }
    public int OwnerId { get; set; }
    public string OwnerName { get; set; } = null!;
    public string? UserRole { get; set; }
    public int MembersCount { get; set; }
    public DateTime Created { get; set; }
}
