namespace DTO.Migration;

public class MigrationExecuteRequestDto
{
    public string QuinielaName { get; set; } = "Liga MX Clausura 2026";
    public string AdminPlayerAlias { get; set; } = "Alex";
    public decimal EntryFee { get; set; } = 0.00m;
    public decimal FirstPlacePct { get; set; } = 70.00m;
    public decimal SecondPlacePct { get; set; } = 20.00m;
    public decimal ThirdPlacePct { get; set; } = 10.00m;
}
