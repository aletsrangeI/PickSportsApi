using System.ComponentModel.DataAnnotations.Schema;
using Domain.Common;

namespace Domain.Entities;

public class Partido : BaseAuditableEntity
{
    [ForeignKey("Liga")] public int LigaId { get; set; }
    public ContenidoCatalogo Liga { get; set; }

    [ForeignKey("EquipoLocal")] public int IdLocal { get; set; }

    [ForeignKey("EquipoVisitante")] public int IdVisitante { get; set; }

    [ForeignKey("ResultadoCatalogo")] public int Resultado { get; set; }

    public ContenidoCatalogo EquipoLocal { get; set; }
    public ContenidoCatalogo EquipoVisitante { get; set; }
    public ContenidoCatalogo ResultadoCatalogo { get; set; }
}