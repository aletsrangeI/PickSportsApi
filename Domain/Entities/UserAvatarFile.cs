namespace Domain.Entities;

public class UserAvatarFile
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string FileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public byte[] Data { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
