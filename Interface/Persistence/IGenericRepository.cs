namespace Interface.Persistence;

public interface IGenericRepository<T> where T : class
{
    #region Metodos Sincronos

    bool Insert(T entity);
    bool Update(T entity);
    bool Delete(int id);
    T Get(int id);
    IEnumerable<T> GetAll();
    IEnumerable<T> GetAllWithPagination(int page, int pageSize);
    int Count();

    #endregion

    #region Metodos Asincronos

    Task<bool> InsertAsync(T entity);
    Task<bool> UpdateAsync(T entity);
    Task<bool> DeleteAsync(int id);
    Task<T> GetAsync(int id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<IEnumerable<T>> GetAllWithPaginationAsync(int page, int pageSize);
    Task<int> CountAsync();

    #endregion
}