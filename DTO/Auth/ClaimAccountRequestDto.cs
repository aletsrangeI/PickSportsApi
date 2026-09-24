namespace DTO.Auth;

public class ClaimAccountRequestDto
{
    public string Token { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string? DisplayName { get; set; }
    public string? Username { get; set; }
}
