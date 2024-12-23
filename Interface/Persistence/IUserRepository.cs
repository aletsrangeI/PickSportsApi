using Domain.Entities;

namespace Interface.Persistence;

public interface IUserRepository : IGenericRepository<User>
{
    User Authenticate(string username, string password);
}