using AutoMapper;
using Common;
using Domain.Entities;
using DTO.User;
using Interface.Persistence;
using Interface.UseCases;
using Validator;

namespace UseCases.Users;

public class UsersApplication : IUsersApplication
{
    private readonly IAppLogger<UsersApplication> _logger;
    private readonly IMapper _mapper;
    private readonly IUnitOfWork _unitOfWork;
    private readonly UsersDTOValidator _validationRules;

    public UsersApplication(IUnitOfWork unitOfWork, IMapper mapper, UsersDTOValidator validationRules,
        IAppLogger<UsersApplication> logger)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _validationRules = validationRules;
        _logger = logger;
    }

    public Response<UserDTO> Authenticate(string email, string password)
    {
        var response = new Response<UserDTO>();
        var validation = _validationRules.Validate(
            new UserDTO { Email = email, Password = password }
        );

        if (!validation.IsValid)
        {
            response.Message = "Errores de validacion";
            response.Errors = validation.Errors;
            return response;
        }

        try
        {
            var user = _unitOfWork.Users.Authenticate(email, password);

            if (user == null)
            {
                response.Message = "El usuario no existe";
                response.Errors = validation.Errors;
                response.isSuccess = false;
                response.Data = _mapper.Map<UserDTO>(user);
            }
            else
            {
                response.Data = _mapper.Map<UserDTO>(user);
                response.isSuccess = true;
                response.Message = "Autenticación exitosa";
            }
        }
        catch
            (InvalidOperationException) // Esto es propio de dapper sucede cuando no se puede mapear el resultado de la consulta a un objeto
        {
            response.isSuccess = false;
            response.Message = "El usuario no existe";
        }
        catch (Exception ex)
        {
            response.Message = ex.Message;
        }

        return response;
    }

    #region Metodos sincronos

    public Response<bool> Insert(UserDTO userDto)
    {
        var response = new Response<bool>();

        try
        {
            var user = _mapper.Map<User>(userDto);
            response.Data = _unitOfWork.Users.Insert(user);

            if (response.Data)
            {
                response.isSuccess = true;
                response.Message = "Usuario creado exitosamente";
            }
        }
        catch (Exception e)
        {
            response.Message = e.Message;
        }

        return response;
    }

    public Response<bool> Update(UserDTO userDto)
    {
        var response = new Response<bool>();

        try
        {
            var user = _mapper.Map<User>(userDto);
            response.Data = _unitOfWork.Users.Update(user);

            if (response.Data)
            {
                response.isSuccess = true;
                response.Message = "Usuario actualizado exitosamente";
            }
        }
        catch (Exception e)
        {
            response.Message = e.Message;
        }

        return response;
    }

    public Response<bool> Delete(int id)
    {
        var response = new Response<bool>();
        var user = _unitOfWork.Users.Get(id);

        if (user == null)
        {
            response.Message = "El usuario no existe";
            response.isSuccess = false;
        }
        else
        {
            response.Data = _unitOfWork.Users.Delete(id);
            response.isSuccess = response.Data;
            response.Message = response.Data ? "Usuario eliminado exitosamente" : "Error al eliminar el usuario";
        }

        return response;
    }

    public Response<UserDTO> Get(int id)
    {
        var response = new Response<UserDTO>();

        try
        {
            var user = _unitOfWork.Users.Get(id);
            response.Data = _mapper.Map<UserDTO>(user);

            if (response.Data != null)
            {
                response.isSuccess = true;
                response.Message = "Consulta exitosa";
            }
        }
        catch (Exception e)
        {
            response.Message = e.Message;
        }

        return response;
    }

    public Response<IEnumerable<UserDTO>> GetAll()
    {
        var response = new Response<IEnumerable<UserDTO>>();
        try
        {
            var users = _unitOfWork.Users.GetAll();
            response.Data = _mapper.Map<IEnumerable<UserDTO>>(users);
            if (response.Data != null)
            {
                response.isSuccess = true;
                response.Message = "Consulta exitosa";
                _logger.LogInformation(response.Message);
            }
        }
        catch (Exception ex)
        {
            response.Message = ex.Message;
            _logger.LogError(ex.Message);
        }

        return response;
    }

    public ResponsePagination<IEnumerable<UserDTO>> GetAllWithPagination(
        int PageNumber,
        int PageSize
    )
    {
        var response = new ResponsePagination<IEnumerable<UserDTO>>();
        try
        {
            var count = _unitOfWork.Users.Count();
            var users = _unitOfWork.Users.GetAllWithPagination(PageNumber, PageSize);
            response.Data = _mapper.Map<IEnumerable<UserDTO>>(users);

            if (response.Data != null)
            {
                response.isSuccess = true;
                response.Message = "Consulta exitosa";
                response.PageNumber = PageNumber;
                response.TotalPages = (int)Math.Ceiling(count / (double)PageSize);
                response.TotalCount = count;
                _logger.LogInformation(response.Message);
            }
        }
        catch (Exception ex)
        {
            response.Message = ex.Message;
            _logger.LogError(ex.Message);
        }

        return response;
    }

    public Response<int> Count()
    {
        var response = new Response<int>();
        try
        {
            response.Data = _unitOfWork.Users.Count();
            response.isSuccess = true;
            response.Message = "Consulta exitosa";
            _logger.LogInformation(response.Message);
        }
        catch (Exception ex)
        {
            response.Message = ex.Message;
            _logger.LogError(ex.Message);
        }

        return response;
    }

    #endregion

    #region Metodos asincronos

    public async Task<Response<bool>> InsertAsync(UserDTO userDto)
    {
        var response = new Response<bool>();

        try
        {
            var user = _mapper.Map<User>(userDto);
            response.Data = await _unitOfWork.Users.InsertAsync(user);

            if (response.Data)
            {
                response.isSuccess = true;
                response.Message = "Usuario creado exitosamente";
            }
        }
        catch (Exception e)
        {
            response.Message = e.Message;
        }

        return response;
    }

    public async Task<Response<bool>> UpdateAsync(UserDTO userDto)
    {
        var response = new Response<bool>();

        try
        {
            var user = _mapper.Map<User>(userDto);
            response.Data = await _unitOfWork.Users.UpdateAsync(user);

            if (response.Data)
            {
                response.isSuccess = true;
                response.Message = "Usuario actualizado exitosamente";
            }
        }
        catch (Exception e)
        {
            response.Message = e.Message;
        }

        return response;
    }

    public async Task<Response<bool>> DeleteAsync(int id)
    {
        var response = new Response<bool>();
        var user = await _unitOfWork.Users.GetAsync(id);

        if (user == null)
        {
            response.Message = "El usuario no existe";
            response.isSuccess = false;
        }
        else
        {
            response.Data = await _unitOfWork.Users.DeleteAsync(id);
            response.isSuccess = response.Data;
            response.Message = response.Data ? "Usuario eliminado exitosamente" : "Error al eliminar el usuario";
        }

        return response;
    }

    public async Task<Response<UserDTO>> GetAsync(int id)
    {
        var response = new Response<UserDTO>();

        try
        {
            var user = await _unitOfWork.Users.GetAsync(id);
            response.Data = _mapper.Map<UserDTO>(user);

            if (response.Data != null)
            {
                response.isSuccess = true;
                response.Message = "Consulta exitosa";
            }
        }
        catch (Exception e)
        {
            response.Message = e.Message;
        }

        return response;
    }

    public async Task<Response<IEnumerable<UserDTO>>> GetAllAsync()
    {
        var response = new Response<IEnumerable<UserDTO>>();
        try
        {
            var users = await _unitOfWork.Users.GetAllAsync();
            response.Data = _mapper.Map<IEnumerable<UserDTO>>(users);
            if (response.Data != null)
            {
                response.isSuccess = true;
                response.Message = "Consulta exitosa";
                _logger.LogInformation(response.Message);
            }
        }
        catch (Exception ex)
        {
            response.Message = ex.Message;
            _logger.LogError(ex.Message);
        }

        return response;
    }

    public async Task<ResponsePagination<IEnumerable<UserDTO>>> GetAllWithPaginationAsync(
        int PageNumber,
        int PageSize
    )
    {
        var response = new ResponsePagination<IEnumerable<UserDTO>>();
        try
        {
            var count = await _unitOfWork.Users.CountAsync();
            var users = await _unitOfWork.Users.GetAllWithPaginationAsync(PageNumber, PageSize);
            response.Data = _mapper.Map<IEnumerable<UserDTO>>(users);

            if (response.Data != null)
            {
                response.isSuccess = true;
                response.Message = "Consulta exitosa";
                response.PageNumber = PageNumber;
                response.TotalPages = (int)Math.Ceiling(count / (double)PageSize);
                response.TotalCount = count;
                _logger.LogInformation(response.Message);
            }
        }
        catch (Exception ex)
        {
            response.Message = ex.Message;
            _logger.LogError(ex.Message);
        }

        return response;
    }

    public async Task<Response<int>> CountAsync()
    {
        var response = new Response<int>();
        try
        {
            response.Data = await _unitOfWork.Users.CountAsync();
            response.isSuccess = true;
            response.Message = "Consulta exitosa";
            _logger.LogInformation(response.Message);
        }
        catch (Exception ex)
        {
            response.Message = ex.Message;
            _logger.LogError(ex.Message);
        }

        return response;
    }

    #endregion
}