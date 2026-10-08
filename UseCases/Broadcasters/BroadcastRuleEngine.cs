namespace UseCases.Broadcasters;

/// <summary>
/// SPEC-015 · Motor de reglas por localía: asigna los canales oficiales de un partido de Liga MX
/// según los derechos de transmisión del club local (Matriz Oficial, sección 3 de la spec).
/// Las claves son las abreviaturas de ESPN; se aceptan alias históricos (NEC, PUM, TIG, CRU).
/// Desde el Apertura 2026 los clubes del ecosistema FOX van por FOX / FOX One (Fox Corporation);
/// Fox Sports México (Grupo Lauman) ya no transmite partidos.
/// </summary>
public class BroadcastRuleEngine
{
    private static readonly Dictionary<string, string[]> RightsByHomeTeam = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AME"]  = ["Canal 5", "TUDN", "ViX Premium"],
        ["ATS"]  = ["Canal 5", "TUDN", "ViX Premium"],
        ["ATL"]  = ["Azteca 7", "Azteca Deportes"],
        ["ASL"]  = ["ESPN", "Disney+", "ViX Premium"],
        ["CAZ"]  = ["Canal 5", "TUDN", "ViX Premium"],
        ["GDL"]  = ["Amazon Prime Video"],
        ["JUA"]  = ["Azteca 7", "FOX", "Azteca Deportes", "FOX One", "ViX Premium"],
        ["LEO"]  = ["FOX", "FOX One", "ViX Premium"],
        ["MTY"]  = ["Canal 5", "TUDN", "ViX Premium"],
        ["NCX"]  = ["FOX", "FOX One", "ViX Premium"],
        ["PAC"]  = ["FOX", "FOX One", "Claro Sports"],
        ["PUE"]  = ["Azteca 7", "FOX", "Azteca Deportes", "FOX One"],
        ["QRO"]  = ["FOX", "FOX One"],
        ["SAN"]  = ["ESPN", "TUDN", "Disney+", "ViX Premium"],
        ["UANL"] = ["Azteca 7", "FOX", "Azteca Deportes", "FOX One"],
        ["TIJ"]  = ["FOX", "FOX One"],
        ["TOL"]  = ["Canal 5", "TUDN", "ViX Premium"],
        ["UNAM"] = ["Las Estrellas", "Canal 5", "TUDN", "ViX Premium"],
    };

    /// <summary>Alias usados por la spec o por datos históricos → abreviatura ESPN vigente.</summary>
    private static readonly Dictionary<string, string> AbbreviationAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["NEC"] = "NCX",
        ["PUM"] = "UNAM",
        ["TIG"] = "UANL",
        ["CRU"] = "CAZ",
        ["SLP"] = "ASL",
        ["CHV"] = "GDL",
        ["XOL"] = "TIJ",
    };

    /// <summary>Abreviaturas canónicas de los 18 clubes cubiertos por la matriz.</summary>
    public static IReadOnlyCollection<string> CoveredTeams => RightsByHomeTeam.Keys;

    /// <summary>Convierte un alias a la abreviatura ESPN canónica (mayúsculas).</summary>
    public static string NormalizeAbbreviation(string? abbreviation)
    {
        if (string.IsNullOrWhiteSpace(abbreviation)) return string.Empty;
        var key = abbreviation.Trim().ToUpperInvariant();
        return AbbreviationAliases.TryGetValue(key, out var canonical) ? canonical : key;
    }

    /// <summary>
    /// Resuelve los canales por defecto para el club local. Si no hay coincidencia devuelve "Por confirmar".
    /// </summary>
    public IReadOnlyList<string> Resolve(string? homeTeamAbbreviation)
    {
        var key = NormalizeAbbreviation(homeTeamAbbreviation);
        return RightsByHomeTeam.TryGetValue(key, out var channels)
            ? channels
            : [BroadcastChannelCatalog.PorConfirmar];
    }
}
