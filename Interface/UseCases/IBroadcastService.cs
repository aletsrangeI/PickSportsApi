using Common;
using DTO.Broadcast;

namespace Interface.UseCases;

/// <summary>SPEC-015: orquesta la asignación de canales de transmisión ("Dónde Ver").</summary>
public interface IBroadcastService
{
    /// <summary>Aplica la regla de localía a los partidos de la jornada que no tengan señal manual ni oficial.</summary>
    Task<int> ApplyDefaultBroadcastersAsync(int weekId, CancellationToken ct = default);

    /// <summary>
    /// Reaplica la regla de localía y enriquece con las señales confirmadas en ligamx.net.
    /// Si la fuente oficial falla, conserva los valores por defecto sin lanzar excepciones.
    /// </summary>
    Task<BroadcastSyncResultDto> SyncFromOfficialSourceAsync(int weekId, CancellationToken ct = default);

    /// <summary>Sincronización disparada por un usuario (valida permisos de administrador).</summary>
    Task<Response<BroadcastSyncResultDto>> SyncWeekBroadcastersAsync(int weekId, int userId, CancellationToken ct = default);

    /// <summary>Override manual de los canales de un partido. Lista vacía = restablecer regla de localía.</summary>
    Task<Response<MatchBroadcastersDto>> UpdateMatchBroadcastersAsync(int matchId, List<string> channels, int userId);
}

/// <summary>Extractor de señales de TV del sitio oficial de la Liga MX.</summary>
public interface ILigaMxBroadcastScraper
{
    /// <summary>Devuelve las señales de la jornada publicada en ligamx.net; lista vacía ante cualquier falla.</summary>
    Task<IReadOnlyList<ScrapedMatchBroadcast>> FetchCurrentWeekAsync(CancellationToken ct = default);
}
