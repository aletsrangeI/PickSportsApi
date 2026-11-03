using Domain.Common;

namespace Domain.Entities;

/// <summary>
/// SPEC-014: cache del Wrapped de un miembro para una temporada (JSON de SeasonWrappedDto).
/// Indice unico (QuinielaId, SeasonId, UserId).
/// </summary>
public class SeasonWrappedSnapshot : BaseAuditableEntity
{
    public int QuinielaId { get; set; }
    public Quiniela Quiniela { get; set; } = null!;

    public int SeasonId { get; set; }
    public Season Season { get; set; } = null!;

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>DTO serializado (System.Text.Json) con las slides y la card final.</summary>
    public string WrappedPayloadJson { get; set; } = null!;

    public DateTime GeneratedAtUtc { get; set; }
}
