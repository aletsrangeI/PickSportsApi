using Common;
using DTO.EquipoLiga;

namespace Interface.UseCases;

public interface IEquipoLigaApplication
{
    #region Metodos sincronos

    Response<bool> Insert(EquipoLigaDTO Catalogo);
    Response<bool> Update(EquipoLigaDTO Catalogo);
    Response<bool> Delete(int id);
    Response<EquipoLigaDTO> Get(int id);
    Response<IEnumerable<EquipoLigaDTO>> GetAll();
    ResponsePagination<IEnumerable<EquipoLigaDTO>> GetAllWithPagination(int page, int pageSize);
    Response<int> Count();

    #endregion

    #region Metodos asincronos

    Task<Response<bool>> InsertAsync(EquipoLigaDTO Catalogo);

    Task<Response<bool>> UpdateAsync(EquipoLigaDTO Catalogo);

    Task<Response<bool>> DeleteAsync(int id);

    Task<Response<EquipoLigaDTO>> GetAsync(int id);

    Task<Response<IEnumerable<EquipoLigaDTO>>> GetAllAsync();

    Task<ResponsePagination<IEnumerable<EquipoLigaDTO>>> GetAllWithPaginationAsync(int page, int pageSize);

    Task<Response<int>> CountAsync();

    #endregion
}