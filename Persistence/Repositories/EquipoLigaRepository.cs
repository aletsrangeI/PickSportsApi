using Domain.Entities;
using Interface.Persistence;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories;

public class EquipoLigaRepository : IEquipoLigaRepository
{
    protected readonly ApplicationDbContext _dbContext;

    public EquipoLigaRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    #region Metodos sincronos

    public bool Insert(EquipoLiga entity)
    {
        _dbContext.EquipoLigas.Add(entity);
        return _dbContext.SaveChanges() > 0;
    }

    public bool Update(EquipoLiga entity)
    {
        _dbContext.EquipoLigas.Update(entity);
        return _dbContext.SaveChanges() > 0;
    }

    public bool Delete(int id)
    {
        var EquipoLiga = Get(id);
        if (EquipoLiga == null) return false;
        _dbContext.EquipoLigas.Remove(EquipoLiga);
        return _dbContext.SaveChanges() > 0;
    }

    public EquipoLiga Get(int id)
    {
        return _dbContext.EquipoLigas.Find(id);
    }

    public IEnumerable<EquipoLiga> GetAll()
    {
        return _dbContext.EquipoLigas;
    }

    public IEnumerable<EquipoLiga> GetAllWithPagination(int page, int pageSize)
    {
        IEnumerable<EquipoLiga> EquipoLigas =
            _dbContext.EquipoLigas.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return EquipoLigas;
    }

    public int Count()
    {
        return _dbContext.EquipoLigas.Count();
    }

    #endregion

    #region Metodos asincronos

    public async Task<bool> InsertAsync(EquipoLiga entity)
    {
        await _dbContext.EquipoLigas.AddAsync(entity);
        return await _dbContext.SaveChangesAsync() > 0;
    }

    public async Task<bool> UpdateAsync(EquipoLiga entity)
    {
        _dbContext.EquipoLigas.Update(entity);
        return await _dbContext.SaveChangesAsync() > 0;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var EquipoLiga = await GetAsync(id);
        if (EquipoLiga == null) return false;
        _dbContext.EquipoLigas.Remove(EquipoLiga);
        return await _dbContext.SaveChangesAsync() > 0;
    }

    public async Task<EquipoLiga> GetAsync(int id)
    {
        return await _dbContext.EquipoLigas.FindAsync(id);
    }

    public async Task<IEnumerable<EquipoLiga>> GetAllAsync()
    {
        return await _dbContext.EquipoLigas.ToListAsync();
    }

    public async Task<IEnumerable<EquipoLiga>> GetAllWithPaginationAsync(int page, int pageSize)
    {
        IEnumerable<EquipoLiga> EquipoLigas =
            await _dbContext.EquipoLigas.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return EquipoLigas;
    }

    public async Task<int> CountAsync()
    {
        return await _dbContext.EquipoLigas.CountAsync();
    }

    #endregion
}