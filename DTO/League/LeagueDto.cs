namespace DTO.League;

public class LeagueDto
{
    public int Id { get; set; }
    public int SportId { get; set; }
    public string SportName { get; set; } = null!;
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Country { get; set; }
    public string? LogoUrl { get; set; }
    public bool Active { get; set; }
}
