using System.ComponentModel.DataAnnotations.Schema;
using Domain.Entities;

namespace DTO.FormField;

public class FormFieldDTO
{
    public string Type { get; set; }
    public string Name { get; set; }
    public string Placeholder { get; set; }
    public string Label { get; set; }
    public string Value { get; set; }
    public List<FormValidation> Validations { get; set; } = new();
    public List<SelectFormOption>? Options { get; set; } = new();
    public Domain.Entities.ContenidoCatalogo Formulario { get; set; }
    public int? CatalogoId { get; set; }
    public Domain.Entities.Catalogo Catalogo { get; set; }
    public int Order { get; set; }
}