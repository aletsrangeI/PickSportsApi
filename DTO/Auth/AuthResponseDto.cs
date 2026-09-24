namespace DTO.Auth;

public class AuthResponseDto
{
    public string Token { get; set; } = null!;
    public UserProfileDto User { get; set; } = null!;
}
