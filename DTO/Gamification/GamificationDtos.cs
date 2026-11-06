namespace DTO.Gamification;

/// <summary>Trofeo desbloqueado (SPEC-013).</summary>
public class AchievementDto
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    /// <summary>BRONZE | SILVER | GOLD | PLATINUM</summary>
    public string Tier { get; set; } = null!;
    public int Gamerscore { get; set; }
    public string? TitleName { get; set; }
    public int SeasonId { get; set; }
    public DateTime UnlockedAt { get; set; }
    public bool IsSeen { get; set; }
}

/// <summary>Trofeo del catalogo aun no desbloqueado en la temporada consultada.</summary>
public class LockedAchievementDto
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Tier { get; set; } = null!;
    public int Gamerscore { get; set; }
}

public class SeasonScoreDto
{
    public int SeasonId { get; set; }
    public string SeasonName { get; set; } = null!;
    public int Gamerscore { get; set; }
}

/// <summary>Vitrina de trofeos de un usuario para una temporada.</summary>
public class AchievementShowcaseDto
{
    public int? SeasonId { get; set; }
    public string? SeasonName { get; set; }
    public int Gamerscore { get; set; }
    public string Level { get; set; } = "Novato";
    /// <summary>Gamerscore necesario para el siguiente nivel; null si ya es el maximo.</summary>
    public int? NextLevelAt { get; set; }
    public string? EquippedTitleCode { get; set; }
    public string? EquippedTitle { get; set; }
    public List<SeasonScoreDto> AvailableSeasons { get; set; } = new();
    public List<AchievementDto> Unlocked { get; set; } = new();
    public List<LockedAchievementDto> Locked { get; set; } = new();
}

public class EquipTitleDto
{
    /// <summary>Codigo del trofeo cuyo titulo se equipa; null para desequipar.</summary>
    public string? Code { get; set; }
}

public class SetGamificationDto
{
    public bool Enabled { get; set; }
}
