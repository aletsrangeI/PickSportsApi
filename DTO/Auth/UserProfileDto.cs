namespace DTO.Auth;

public class UserProfileDto
{
    public int Id { get; set; }
    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string? AvatarUrl { get; set; }
    public string Role { get; set; } = null!;
}
