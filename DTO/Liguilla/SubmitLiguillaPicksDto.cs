namespace DTO.Liguilla;

/// <summary>
/// DTO para enviar los pronósticos de Liguilla agrupados por ronda.
/// </summary>
public class SubmitLiguillaPicksDto
{
    /// <summary>
    /// Diccionario de listas de IDs de equipos seleccionados por ronda.
    /// </summary>
    public Dictionary<string, List<int>> PicksByRound { get; set; } = new();
}
