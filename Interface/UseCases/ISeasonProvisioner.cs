using Domain.Entities;

namespace Interface.UseCases;

/// <summary>
/// H-011: deja lista la temporada de una liga (existe, es la vigente y tiene jornadas/partidos)
/// para que una quiniela recién creada se pueda iniciar e invitar sin pasos manuales en ESPN.
/// </summary>
public interface ISeasonProvisioner
{
    /// <summary>
    /// Devuelve la temporada vigente de la liga; si no existe la crea y sincroniza su calendario desde ESPN.
    /// Es idempotente: no duplica temporadas ni jornadas. Un fallo de ESPN no lanza excepción:
    /// la temporada queda creada y <see cref="SeasonProvisionResult.Warning"/> explica qué falta.
    /// </summary>
    Task<SeasonProvisionResult> EnsureReadyAsync(League league, CancellationToken ct = default);
}

public record SeasonProvisionResult(
    Season? Season,
    /// <summary>True si en esta llamada se creó la temporada.</summary>
    bool Created,
    /// <summary>True si la temporada ya tiene jornadas (sincronizadas ahora o previamente).</summary>
    bool HasWeeks,
    /// <summary>Texto para el usuario cuando algo quedó pendiente (p. ej. ESPN no respondió).</summary>
    string? Warning);
