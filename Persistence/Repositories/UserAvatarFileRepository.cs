using Domain.Entities;
using Interface.Persistence;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories;

public class UserAvatarFileRepository : GenericRepository<UserAvatarFile>, IUserAvatarFileRepository
{
    public UserAvatarFileRepository(ApplicationDbContext context) : base(context) { }

    public async Task<UserAvatarFile?> GetByFileNameAsync(string fileName, CancellationToken cancellationToken = default)
    {
        return await _dbSet.AsNoTracking().FirstOrDefaultAsync(f => f.FileName == fileName, cancellationToken);
    }

    public async Task<UserAvatarFile?> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FirstOrDefaultAsync(f => f.UserId == userId, cancellationToken);
    }

    public async Task DeleteByFileNameAsync(string fileName, CancellationToken cancellationToken = default)
    {
        var records = await _dbSet.Where(f => f.FileName == fileName).ToListAsync(cancellationToken);
        if (records.Any())
        {
            _dbSet.RemoveRange(records);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
