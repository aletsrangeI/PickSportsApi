using Domain.Common;

namespace Domain.Entities;

public class User : BaseAuditableEntity
{
    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;

    public string Password
    {
        get => PasswordHash;
        set => PasswordHash = value;
    }

    public string DisplayName { get; set; } = null!;
    public string? AvatarUrl { get; set; }
    public string Role { get; set; } = "USER"; // ADMIN, USER
    public string? Token { get; set; }

    public ICollection<Quiniela> QuinielasOwned { get; set; } = new List<Quiniela>();
    public ICollection<QuinielaMember> Memberships { get; set; } = new List<QuinielaMember>();
    public ICollection<PushSubscription> PushSubscriptions { get; set; } = new List<PushSubscription>();
}