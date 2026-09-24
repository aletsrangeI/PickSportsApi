using Domain.Common;
using Interface.Persistence;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories;

public class GenericRepository<T> : IGenericRepository<T> where T : class
{
    protected readonly ApplicationDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public GenericRepository(ApplicationDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    #region Metodos Sincronos

    public virtual bool Insert(T entity)
    {
        _dbSet.Add(entity);
        return _context.SaveChanges() > 0;
    }

    public virtual bool Update(T entity)
    {
        _dbSet.Update(entity);
        return _context.SaveChanges() > 0;
    }

    public virtual bool Delete(int id)
    {
        var entity = _dbSet.Find(id);
        if (entity is null) return false;

        if (entity is BaseEntity baseEntity)
        {
            baseEntity.Active = false;
            _dbSet.Update(entity);
        }
        else
        {
            _dbSet.Remove(entity);
        }

        return _context.SaveChanges() > 0;
    }

    public virtual T Get(int id)
    {
        return _dbSet.Find(id)!;
    }

    public virtual IEnumerable<T> GetAll()
    {
        if (typeof(BaseEntity).IsAssignableFrom(typeof(T)))
        {
            return _dbSet.AsNoTracking().Where(e => ((BaseEntity)(object)e).Active).ToList();
        }
        return _dbSet.AsNoTracking().ToList();
    }

    public virtual IEnumerable<T> GetAllWithPagination(int page, int pageSize)
    {
        var query = _dbSet.AsNoTracking();
        if (typeof(BaseEntity).IsAssignableFrom(typeof(T)))
        {
            query = query.Where(e => ((BaseEntity)(object)e).Active);
        }
        return query.Skip((page - 1) * pageSize).Take(pageSize).ToList();
    }

    public virtual int Count()
    {
        if (typeof(BaseEntity).IsAssignableFrom(typeof(T)))
        {
            return _dbSet.Count(e => ((BaseEntity)(object)e).Active);
        }
        return _dbSet.Count();
    }

    #endregion

    #region Metodos Asincronos

    public virtual async Task<bool> InsertAsync(T entity)
    {
        await _dbSet.AddAsync(entity);
        return await _context.SaveChangesAsync() > 0;
    }

    public virtual async Task<bool> UpdateAsync(T entity)
    {
        _dbSet.Update(entity);
        return await _context.SaveChangesAsync() > 0;
    }

    public virtual async Task<bool> DeleteAsync(int id)
    {
        var entity = await _dbSet.FindAsync(id);
        if (entity is null) return false;

        if (entity is BaseEntity baseEntity)
        {
            baseEntity.Active = false;
            _dbSet.Update(entity);
        }
        else
        {
            _dbSet.Remove(entity);
        }

        return await _context.SaveChangesAsync() > 0;
    }

    public virtual async Task<T> GetAsync(int id)
    {
        return (await _dbSet.FindAsync(id))!;
    }

    public virtual async Task<IEnumerable<T>> GetAllAsync()
    {
        if (typeof(BaseEntity).IsAssignableFrom(typeof(T)))
        {
            return await _dbSet.AsNoTracking().Where(e => ((BaseEntity)(object)e).Active).ToListAsync();
        }
        return await _dbSet.AsNoTracking().ToListAsync();
    }

    public virtual async Task<IEnumerable<T>> GetAllWithPaginationAsync(int page, int pageSize)
    {
        var query = _dbSet.AsNoTracking();
        if (typeof(BaseEntity).IsAssignableFrom(typeof(T)))
        {
            query = query.Where(e => ((BaseEntity)(object)e).Active);
        }
        return await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
    }

    public virtual async Task<int> CountAsync()
    {
        if (typeof(BaseEntity).IsAssignableFrom(typeof(T)))
        {
            return await _dbSet.CountAsync(e => ((BaseEntity)(object)e).Active);
        }
        return await _dbSet.CountAsync();
    }

    #endregion
}
