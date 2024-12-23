using Common;
using DTO.ContenidoCatalogo;

namespace Interface.UseCases;

public interface IContenidoCatalogoApplication
{
    #region Metodos sincronos

    Response<bool> Insert(ContenidoCatalogoDTO Catalogo);
    Response<bool> Update(ContenidoCatalogoDTO Catalogo);
    Response<bool> Delete(int id);
    Response<ContenidoCatalogoDTO> Get(int id);
    Response<IEnumerable<ContenidoCatalogoDTO>> GetAll();
    ResponsePagination<IEnumerable<ContenidoCatalogoDTO>> GetAllWithPagination(int page, int pageSize);
    Response<int> Count();
    Response<IEnumerable<ContenidoCatalogoDTO>> GetContenidoCatalogoByCatalogoId(int id);

    #endregion

    #region Metodos asincronos

    Task<Response<bool>> InsertAsync(ContenidoCatalogoDTO Catalogo);

    Task<Response<bool>> UpdateAsync(ContenidoCatalogoDTO Catalogo);

    Task<Response<bool>> DeleteAsync(int id);

    Task<Response<ContenidoCatalogoDTO>> GetAsync(int id);

    Task<Response<IEnumerable<ContenidoCatalogoDTO>>> GetAllAsync();

    Task<ResponsePagination<IEnumerable<ContenidoCatalogoDTO>>> GetAllWithPaginationAsync(int page, int pageSize);

    Task<Response<int>> CountAsync();

    Task<Response<IEnumerable<ContenidoCatalogoDTO>>> GetContenidoCatalogoByCatalogoIdAsync(int id);

    #endregion
}