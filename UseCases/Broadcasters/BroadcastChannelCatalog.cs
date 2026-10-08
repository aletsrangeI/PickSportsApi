using System.Text.Json;
using Domain.Entities;

namespace UseCases.Broadcasters;

/// <summary>
/// Nombres canónicos de televisoras/plataformas y serialización del campo <c>Match.Broadcasters</c>.
/// </summary>
public static class BroadcastChannelCatalog
{
    public const string PorConfirmar = "Por confirmar";

    public const string SourceRule   = "RULE";
    public const string SourceLigaMx = "LIGAMX";
    public const string SourceManual = "MANUAL";

    public const int MaxChannels      = 8;
    public const int MaxChannelLength = 40;
    public const int MaxStoredLength  = 300;

    private static readonly Dictionary<string, string> CanonicalNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["canal 5"]            = "Canal 5",
        ["canal5"]             = "Canal 5",
        ["las estrellas"]      = "Las Estrellas",
        ["tudn"]               = "TUDN",
        ["vix"]                = "ViX Premium",
        ["vix premium"]        = "ViX Premium",
        ["vix+"]               = "ViX Premium",
        ["azteca 7"]           = "Azteca 7",
        ["azteca7"]            = "Azteca 7",
        ["azteca deportes"]    = "Azteca Deportes",
        ["espn"]               = "ESPN",
        ["disney+"]            = "Disney+",
        ["disney plus"]        = "Disney+",
        ["fox"]                = "FOX",
        // Fox Sports México (Grupo Lauman) salió de izzi/Sky el 30-ago-2026 y ya no transmite Liga MX.
        // ligamx.net aún etiqueta así al canal FOX de Fox Corporation, por eso se mapea a "FOX".
        ["fox sports"]         = "FOX",
        ["foxsports"]          = "FOX",
        ["fox sports mexico"]  = "FOX",
        ["fox sports méxico"]  = "FOX",
        ["fox+"]               = "FOX+",
        ["fox plus"]           = "FOX+",
        ["fox one"]            = "FOX One",
        ["foxone"]             = "FOX One",
        ["claro sports"]       = "Claro Sports",
        ["prime video"]        = "Amazon Prime Video",
        ["amazon prime"]       = "Amazon Prime Video",
        ["amazon prime video"] = "Amazon Prime Video",
        // Caliente TV (incl. "Caliente 2") ya no existe: sus derechos pasaron a FOX One
        ["caliente"]           = "FOX One",
        ["caliente tv"]        = "FOX One",
        ["caliente 2"]         = "FOX One",
        ["por confirmar"]      = PorConfirmar,
    };

    /// <summary>Normaliza un nombre de canal a su forma canónica; nombres desconocidos se conservan recortados.</summary>
    public static string Normalize(string channel)
    {
        var trimmed = string.Join(' ', channel.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return CanonicalNames.TryGetValue(trimmed, out var canonical) ? canonical : trimmed;
    }

    /// <summary>Normaliza, elimina vacíos/duplicados y recorta a los límites de almacenamiento.</summary>
    public static List<string> Sanitize(IEnumerable<string>? channels)
    {
        var result = new List<string>();
        if (channels == null) return result;

        foreach (var raw in channels)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var name = Normalize(raw);
            if (name.Length > MaxChannelLength) name = name[..MaxChannelLength].Trim();
            if (result.Contains(name, StringComparer.OrdinalIgnoreCase)) continue;
            result.Add(name);
            if (result.Count == MaxChannels) break;
        }

        // Garantiza que el JSON quepa en la columna (varchar 300)
        while (result.Count > 1 && Serialize(result).Length > MaxStoredLength)
            result.RemoveAt(result.Count - 1);

        return result;
    }

    public static string Serialize(IEnumerable<string> channels) => JsonSerializer.Serialize(channels);

    /// <summary>
    /// Deserializa el campo persistido y lo normaliza al catálogo vigente (ej. valores viejos "FOX Sports"
    /// o "Caliente 2" se muestran ya corregidos). Tolera JSON inválido o lista separada por comas.
    /// </summary>
    public static List<string> Deserialize(string? stored) => Sanitize(DeserializeRaw(stored));

    private static List<string> DeserializeRaw(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored)) return new List<string>();

        if (stored.TrimStart().StartsWith('['))
        {
            try
            {
                return JsonSerializer.Deserialize<List<string>>(stored)?
                    .Where(c => !string.IsNullOrWhiteSpace(c)).ToList() ?? new List<string>();
            }
            catch (JsonException)
            {
                return new List<string>();
            }
        }

        return stored.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    /// <summary>"Dónde Ver" solo aplica a la Liga MX (los derechos se asignan por club local).</summary>
    public static bool IsLigaMx(League? league)
    {
        if (league == null) return false;
        return string.Equals(league.Code, "mex.1", StringComparison.OrdinalIgnoreCase)
            || (league.EspnApiPath?.Contains("mex.1", StringComparison.OrdinalIgnoreCase) ?? false)
            || (league.Name?.Contains("Liga MX", StringComparison.OrdinalIgnoreCase) ?? false);
    }
}
