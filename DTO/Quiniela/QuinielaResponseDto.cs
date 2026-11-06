namespace DTO.Quiniela;

public class QuinielaResponseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string QuinielaType { get; set; } = "Sports";
    public int? LeagueId { get; set; }
    public string LeagueCode { get; set; } = null!;
    public string LeagueName { get; set; } = null!;
    public string SportName { get; set; } = null!;
    public string InviteCode { get; set; } = null!;
    public decimal EntryFee { get; set; }
    public decimal FirstPlacePct { get; set; }
    public decimal SecondPlacePct { get; set; }
    public decimal ThirdPlacePct { get; set; }
    public bool IsActive { get; set; }
    public int? SeasonId { get; set; }
    public string? SeasonName { get; set; }
    /// <summary>True si la quiniela se archivó al abrir la siguiente temporada (histórica, solo lectura).</summary>
    public bool IsArchived { get; set; }
    /// <summary>SPEC-013: trofeos, rachas y duelos activos en esta quiniela.</summary>
    public bool GamificationEnabled { get; set; }
    public int OwnerId { get; set; }
    public string OwnerName { get; set; } = null!;
    public string? UserRole { get; set; }
    public int MembersCount { get; set; }
    public DateTime Created { get; set; }
}
