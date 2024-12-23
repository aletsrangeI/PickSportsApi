using Common;
using DTO.Catalogo;

namespace Interface.UseCases;

public interface ICatalogosApplication
{
    #region Metodos sincronos

    Response<bool> Insert(CatalogoDTO Catalogo);
    Response<bool> Update(CatalogoDTO Catalogo);
    Response<bool> Delete(int id);
    Response<CatalogoDTO> Get(int id);
    Response<IEnumerable<CatalogoDTO>> GetAll();
    ResponsePagination<IEnumerable<CatalogoDTO>> GetAllWithPagination(int page, int pageSize);
    Response<int> Count();

    #endregion

    #region Metodos asincronos

    Task<Response<bool>> InsertAsync(CatalogoDTO Catalogo);

    Task<Response<bool>> UpdateAsync(CatalogoDTO Catalogo);

    Task<Response<bool>> DeleteAsync(int id);

    Task<Response<CatalogoDTO>> GetAsync(int id);

    Task<Response<IEnumerable<CatalogoDTO>>> GetAllAsync();

    Task<ResponsePagination<IEnumerable<CatalogoDTO>>> GetAllWithPaginationAsync(int page, int pageSize);

    Task<Response<int>> CountAsync();

    #endregion
}