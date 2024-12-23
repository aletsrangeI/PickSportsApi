using Domain.Entities;

namespace Interface.Persistence;

public interface IContenidoCatalogoRepository : IGenericRepository<ContenidoCatalogo>
{
    IEnumerable<ContenidoCatalogo> GetContenidoCatalogoByCatalogoId(int id);

    Task<List<ContenidoCatalogo>> GetContenidoCatalogoByCatalogoIdAsync(int id);
}