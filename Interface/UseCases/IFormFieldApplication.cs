using Common;
using DTO.FormField;

namespace Interface.UseCases;

public interface IFormFieldApplication
{
    #region Metodos sincronos

    Response<bool> Insert(FormFieldDTO indiceCatalogo);
    Response<bool> Update(FormFieldDTO indiceCatalogo);
    Response<bool> Delete(int id);
    Response<FormFieldDTO> Get(int id);
    Response<IEnumerable<FormFieldDTO>> GetAll();
    ResponsePagination<IEnumerable<FormFieldDTO>> GetAllWithPagination(int page, int pageSize);
    Response<int> Count();

    #endregion

    #region Metodos asincronos

    Task<Response<bool>> InsertAsync(FormFieldDTO indiceCatalogo);

    Task<Response<bool>> UpdateAsync(FormFieldDTO indiceCatalogo);

    Task<Response<bool>> DeleteAsync(int id);

    Task<Response<FormFieldDTO>> GetAsync(int id);

    Task<Response<IEnumerable<FormFieldDTO>>> GetAllAsync();

    Task<ResponsePagination<IEnumerable<FormFieldDTO>>> GetAllWithPaginationAsync(int page, int pageSize);

    Task<Response<int>> CountAsync();

    #endregion

    Response<List<FormFieldDTO>> GetFormFieldByFormCatId(int id);
}