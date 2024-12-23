using System.Security.Cryptography;
using System.Text;
using Domain.Entities;
using Interface.Persistence;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories;

public class UserRepository : IUserRepository
{
    protected readonly ApplicationDbContext _dbContext;

    public UserRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public string Hash(string password)
    {
        var salt = GenerateSalt();
        var hashedPassword = HashWithSalt(password, salt);
        return $"{Convert.ToBase64String(salt)}:{hashedPassword}";
    }

    public bool Check(string password, string storedHash)
    {
        var parts = storedHash.Split(':');
        var salt = Convert.FromBase64String(parts[0]);
        var hash = parts[1];
        var hashedPassword = HashWithSalt(password, salt);
        return hashedPassword == hash;
    }

    private byte[] GenerateSalt()
    {
        using (var rng = new RNGCryptoServiceProvider())
        {
            var salt = new byte[16];
            rng.GetBytes(salt);
            return salt;
        }
    }

    private string HashWithSalt(string password, byte[] salt)
    {
        using (var sha256 = SHA256.Create())
        {
            var passwordBytes = Encoding.UTF8.GetBytes(password);
            var saltedPassword = new byte[salt.Length + passwordBytes.Length];

            Buffer.BlockCopy(salt, 0, saltedPassword, 0, salt.Length);
            Buffer.BlockCopy(passwordBytes, 0, saltedPassword, salt.Length, passwordBytes.Length);

            return Convert.ToBase64String(sha256.ComputeHash(saltedPassword));
        }
    }

    #region Metodos sincronos

    public bool Insert(User entity)
    {
        entity.Password = Hash(entity.Password);
        _dbContext.Users.Add(entity);
        return _dbContext.SaveChanges() > 0;
    }

    public bool Update(User entity)
    {
        entity.Password = Hash(entity.Password);
        _dbContext.Users.Update(entity);
        return _dbContext.SaveChanges() > 0;
    }

    public bool Delete(int id)
    {
        var entity = Get(id);
        if (entity == null) return false;
        _dbContext.Users.Remove(entity);
        return _dbContext.SaveChanges() > 0;
    }

    public User Get(int id)
    {
        var entity = _dbContext.Users.FirstOrDefault(u => u.Id == id);
        return entity;
    }

    public IEnumerable<User> GetAll()
    {
        return _dbContext.Users;
    }

    public IEnumerable<User> GetAllWithPagination(int page, int pageSize)
    {
        IEnumerable<User> entities = _dbContext.Users.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return entities;
    }

    public int Count()
    {
        return _dbContext.Users.Count();
    }

    public User Authenticate(string username, string password)
    {
        var user = _dbContext.Users.SingleOrDefault(x => x.Email == username);
        if (user == null) return null;
        if (!Check(password, user.Password)) return null;
        return user;
    }

    #endregion

    #region Metodos asincronos

    public async Task<bool> InsertAsync(User entity)
    {
        entity.Password = Hash(entity.Password);
        await _dbContext.Users.AddAsync(entity);
        return await _dbContext.SaveChangesAsync() > 0;
    }

    public async Task<bool> UpdateAsync(User entity)
    {
        entity.Password = Hash(entity.Password);
        _dbContext.Users.Update(entity);
        return await _dbContext.SaveChangesAsync() > 0;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var user = await GetAsync(id);
        if (user == null) return false;
        _dbContext.Users.Remove(user);
        return await _dbContext.SaveChangesAsync() > 0;
    }

    public async Task<User> GetAsync(int id)
    {
        return await _dbContext.Users.FindAsync(id);
    }

    public async Task<IEnumerable<User>> GetAllAsync()
    {
        return await _dbContext.Users.ToListAsync();
    }

    public async Task<IEnumerable<User>> GetAllWithPaginationAsync(int page, int pageSize)
    {
        IEnumerable<User> users = await _dbContext.Users.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return users;
    }

    public async Task<int> CountAsync()
    {
        return await _dbContext.Users.CountAsync();
    }

    #endregion
}