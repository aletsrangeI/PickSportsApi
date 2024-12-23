using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Domain.Common;

namespace Domain.Entities;

public class FormField : BaseAuditableEntity
{
    [Required] [MaxLength(50)] public string Type { get; set; }

    [Required] [MaxLength(100)] public string Name { get; set; }

    [MaxLength(255)] public string Placeholder { get; set; }

    [Required] [MaxLength(255)] public string Label { get; set; }

    [MaxLength(255)] public string Value { get; set; }

    public List<FormValidation> Validations { get; set; } = new();

    public List<SelectFormOption>? Options { get; set; } = new();

    [ForeignKey("SeccionCatalogo")] public int FormularioId { get; set; }
    public ContenidoCatalogo Formulario { get; set; }

    [ForeignKey("Seccion")] public int? CatalogoId { get; set; } // Nullable CatalogoId
    public Catalogo Catalogo { get; set; }

    public int Order { get; set; }
}