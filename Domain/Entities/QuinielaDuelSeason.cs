using Domain.Common;

namespace Domain.Entities;

/// <summary>
/// SPEC-013 Etapa 2: datos de la temporada de duelos de una quiniela (una fila por quiniela):
/// semilla del sorteo, campeon y la copa/castigo que el campeon fija para la siguiente temporada.
/// </summary>
public class QuinielaDuelSeason : BaseAuditableEntity
{
    public int QuinielaId { get; set; }
    public Quiniela Quiniela { get; set; } = null!;

    public int Seed { get; set; }
    public DateTime GeneratedAt { get; set; }

    public int? ChampionMemberId { get; set; }
    public QuinielaMember? ChampionMember { get; set; }

    public DateTime? ClosedAt { get; set; }

    public string? CupName { get; set; }
    public string? PunishmentText { get; set; }
}
