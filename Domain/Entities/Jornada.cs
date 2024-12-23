using System.ComponentModel.DataAnnotations.Schema;
using Domain.Common;

namespace Domain.Entities;

public class Jornada : BaseAuditableEntity
{
    [ForeignKey("Liga")] public int LigaId { get; set; }

    public ContenidoCatalogo Liga { get; set; }
    public ICollection<PartidosJornada> Partidos { get; set; }
}