namespace Domain.Entities;

/// <summary>
/// Sesion de un dispositivo o navegador. Cada login crea una fila, de modo que entrar desde otro
/// dispositivo no invalida las sesiones existentes.
/// </summary>
public class UserSession
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>Refresh token vigente de este dispositivo.</summary>
    public string RefreshToken { get; set; } = null!;

    /// <summary>
    /// Refresh token inmediatamente anterior a la ultima rotacion. Se acepta solo hasta
    /// <see cref="PreviousRefreshTokenValidUntil"/> para que dos ventanas que renuevan a la vez
    /// (PWA abierta + ventana de una notificacion) no invaliden la sesion una a la otra.
    /// </summary>
    public string? PreviousRefreshToken { get; set; }
    public DateTime? PreviousRefreshTokenValidUntil { get; set; }

    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastUsedAt { get; set; }
}
