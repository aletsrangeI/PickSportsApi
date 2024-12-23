using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Domain.Common;

namespace Domain.Entities;

public class ContenidoCatalogo : BaseAuditableEntity
{
    [Required] [MaxLength(100)] public string Nombre { get; set; }

    [MaxLength(250)] public string Descripcion { get; set; }

    public string Opcional { get; set; }

    [ForeignKey("Catalogo")] public int CatalogoId { get; set; }
    public Catalogo Catalogo { get; set; }
}