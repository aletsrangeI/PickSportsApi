using System.ComponentModel.DataAnnotations.Schema;
using Domain.Common;

namespace Domain.Entities;

public class EquipoLiga : BaseAuditableEntity
{
    [ForeignKey("Equipo")] public int EquipoId { get; set; }

    [ForeignKey("Liga")] public int LigaId { get; set; }

    public ContenidoCatalogo Equipo { get; set; }
    public ContenidoCatalogo Liga { get; set; }
}