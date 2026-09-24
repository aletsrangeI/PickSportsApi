using Domain.Entities;

namespace Interface.Persistence;

public interface IUserRepository : IGenericRepository<User>
{
    User Authenticate(string username, string password);
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByUsernameAsync(string username);
    Task<User?> AuthenticateAsync(string emailOrUsername, string password);
}