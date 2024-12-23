using Common;
using DTO.User;

namespace Interface.UseCases;

public interface IUsersApplication
{
    Response<UserDTO> Authenticate(string username, string password);

    #region Metodos sincronos

    Response<bool> Insert(UserDTO user);
    Response<bool> Update(UserDTO user);
    Response<bool> Delete(int id);
    Response<UserDTO> Get(int id);
    Response<IEnumerable<UserDTO>> GetAll();
    ResponsePagination<IEnumerable<UserDTO>> GetAllWithPagination(int page, int pageSize);
    Response<int> Count();

    #endregion

    #region Metodos asincronos

    Task<Response<bool>> InsertAsync(UserDTO user);

    Task<Response<bool>> UpdateAsync(UserDTO user);

    Task<Response<bool>> DeleteAsync(int id);

    Task<Response<UserDTO>> GetAsync(int id);

    Task<Response<IEnumerable<UserDTO>>> GetAllAsync();

    Task<ResponsePagination<IEnumerable<UserDTO>>> GetAllWithPaginationAsync(int page, int pageSize);

    Task<Response<int>> CountAsync();

    #endregion
}