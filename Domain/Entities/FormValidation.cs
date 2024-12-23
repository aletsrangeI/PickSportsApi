using Domain.Common;

namespace Domain.Entities;

public class FormValidation : BaseAuditableEntity
{
    public string Type { get; set; }
    public string Value { get; set; }
}