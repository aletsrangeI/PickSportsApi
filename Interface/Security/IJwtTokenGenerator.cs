using Domain.Entities;

namespace Interface.Security;

public interface IJwtTokenGenerator
{
    string GenerateToken(User user);
}
