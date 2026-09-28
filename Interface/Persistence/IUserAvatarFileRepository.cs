using Domain.Entities;

namespace Interface.Persistence;

public interface IUserAvatarFileRepository : IGenericRepository<UserAvatarFile>
{
    Task<UserAvatarFile?> GetByFileNameAsync(string fileName, CancellationToken cancellationToken = default);
    Task<UserAvatarFile?> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default);
    Task DeleteByFileNameAsync(string fileName, CancellationToken cancellationToken = default);
}
