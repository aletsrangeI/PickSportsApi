namespace DTO.User;

public class UserDTO
{
    public int Id { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public string Email { get; set; }
    public int Role { get; set; }
    public string Token { get; set; }
}