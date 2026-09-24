namespace DTO.Espn;

public class TeamDto
{
    public int Id { get; set; }
    public string EspnTeamId { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Abbreviation { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string? LogoUrl { get; set; }
    public string? PrimaryColor { get; set; }
}
