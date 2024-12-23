using System.ComponentModel.DataAnnotations.Schema;
using Domain.Common;

namespace Domain.Entities;

public class PartidosJornada : BaseAuditableEntity
{
    [ForeignKey("Jornada")] public int JornadaId { get; set; }

    [ForeignKey("Partido")] public int PartidoId { get; set; }

    public Jornada Jornada { get; set; }
    public Partido Partido { get; set; }
}