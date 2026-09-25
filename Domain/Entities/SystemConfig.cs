using Domain.Common;

namespace Domain.Entities;

public class SystemConfig : BaseAuditableEntity
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsPublic { get; set; } = true;
}
