namespace DTO.Quiniela;

public class StartNextSeasonDto
{
    /// <summary>Nombre de la quiniela de la nueva temporada. Si viene vacío se conserva el nombre actual.</summary>
    public string? Name { get; set; }
}
