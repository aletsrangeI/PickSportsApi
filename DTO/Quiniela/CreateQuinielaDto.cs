namespace DTO.Quiniela;

public class CreateQuinielaDto
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int LeagueId { get; set; }
    public decimal EntryFee { get; set; } = 0.00m;
    public decimal FirstPlacePct { get; set; } = 70.00m;
    public decimal SecondPlacePct { get; set; } = 20.00m;
    public decimal ThirdPlacePct { get; set; } = 10.00m;
}
