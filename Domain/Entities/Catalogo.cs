using System.ComponentModel.DataAnnotations;
using Domain.Common;

namespace Domain.Entities;

public class Catalogo : BaseAuditableEntity
{
    [Required] [MaxLength(100)] public string Nombre { get; set; }

    [MaxLength(250)] public string Descripcion { get; set; }

    [Required] [MaxLength(50)] public string CampoTabla { get; set; }

    public ICollection<ContenidoCatalogo> Contenidos { get; set; }
}