using Domain.Entities;

namespace Interface.Security;

public interface IJwtTokenGenerator
{
    string GenerateToken(User user);
    string GenerateRefreshToken();

    /// <summary>
    /// Valida firma, emisor y audiencia de un access token ignorando solo su expiracion.
    /// Devuelve null si el token no es valido (firma alterada, otro emisor, algoritmo distinto, etc.).
    /// </summary>
    System.Security.Claims.ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
}
