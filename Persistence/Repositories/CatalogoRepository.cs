using Domain.Entities;
using Interface.Persistence;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories;

public class CatalogoRepository : ICatalogoRepository
{
    protected readonly ApplicationDbContext _dbContext;

    public CatalogoRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    #region Metodos sincronos

    public bool Insert(Catalogo entity)
    {
        _dbContext.Catalogos.Add(entity);
        return _dbContext.SaveChanges() > 0;
    }

    public bool Update(Catalogo entity)
    {
        _dbContext.Catalogos.Update(entity);
        return _dbContext.SaveChanges() > 0;
    }

    public bool Delete(int id)
    {
        var Catalogo = Get(id);
        if (Catalogo == null) return false;
        _dbContext.Catalogos.Remove(Catalogo);
        return _dbContext.SaveChanges() > 0;
    }

    public Catalogo Get(int id)
    {
        return _dbContext.Catalogos.Find(id);
    }

    public IEnumerable<Catalogo> GetAll()
    {
        return _dbContext.Catalogos;
    }

    public IEnumerable<Catalogo> GetAllWithPagination(int page, int pageSize)
    {
        IEnumerable<Catalogo> Catalogos =
            _dbContext.Catalogos.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Catalogos;
    }

    public int Count()
    {
        return _dbContext.Catalogos.Count();
    }

    #endregion

    #region Metodos asincronos

    public async Task<bool> InsertAsync(Catalogo entity)
    {
        await _dbContext.Catalogos.AddAsync(entity);
        return await _dbContext.SaveChangesAsync() > 0;
    }

    public async Task<bool> UpdateAsync(Catalogo entity)
    {
        _dbContext.Catalogos.Update(entity);
        return await _dbContext.SaveChangesAsync() > 0;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var Catalogo = await GetAsync(id);
        if (Catalogo == null) return false;
        _dbContext.Catalogos.Remove(Catalogo);
        return await _dbContext.SaveChangesAsync() > 0;
    }

    public async Task<Catalogo> GetAsync(int id)
    {
        return await _dbContext.Catalogos.FindAsync(id);
    }

    public async Task<IEnumerable<Catalogo>> GetAllAsync()
    {
        return await _dbContext.Catalogos.ToListAsync();
    }

    public async Task<IEnumerable<Catalogo>> GetAllWithPaginationAsync(int page, int pageSize)
    {
        IEnumerable<Catalogo> Catalogos =
            await _dbContext.Catalogos.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Catalogos;
    }

    public async Task<int> CountAsync()
    {
        return await _dbContext.Catalogos.CountAsync();
    }

    #endregion
}