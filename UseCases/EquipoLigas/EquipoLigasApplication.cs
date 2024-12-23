using AutoMapper;
using Common;
using Domain.Entities;
using DTO.EquipoLiga;
using Interface.Persistence;
using Interface.UseCases;
using Validator;

namespace UseCases.EquipoLigas;

public class EquipoLigasApplication : IEquipoLigaApplication
{
    private readonly IAppLogger<EquipoLigasApplication> _logger;
    private readonly IMapper _mapper;
    private readonly IUnitOfWork _unitOfWork;
    private readonly EquipoLigaDTOValidator _validationRules;

    public EquipoLigasApplication(IUnitOfWork unitOfWork, IMapper mapper, EquipoLigaDTOValidator validationRules,
        IAppLogger<EquipoLigasApplication> logger)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _validationRules = validationRules;
        _logger = logger;
    }

    #region Metodos sincronos

    public Response<bool> Insert(EquipoLigaDTO EquipoLigaDTO)
    {
        var response = new Response<bool>();

        try
        {
            var EquipoLigas = _mapper.Map<EquipoLiga>(EquipoLigaDTO);
            response.Data = _unitOfWork.EquipoLigas.Insert(EquipoLigas);

            if (response.Data)
            {
                response.isSuccess = true;
                response.Message = "Indice EquipoLigas creado exitosamente";
            }
        }
        catch (Exception e)
        {
            response.Message = e.Message;
        }

        return response;
    }

    public Response<bool> Update(EquipoLigaDTO EquipoLigaDTO)
    {
        var response = new Response<bool>();

        try
        {
            var EquipoLigas = _mapper.Map<EquipoLiga>(EquipoLigaDTO);
            _unitOfWork.EquipoLigas.Insert(EquipoLigas);

            if (response.Data)
            {
                response.isSuccess = true;
                response.Message = "Indice EquipoLigas actualizado exitosamente";
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
        var EquipoLigas = _unitOfWork.EquipoLigas.Get(id);

        if (EquipoLigas == null)
        {
            response.Message = "El Indice EquipoLigas no existe";
            response.isSuccess = false;
        }
        else
        {
            response.Data = _unitOfWork.EquipoLigas.Delete(id);
            response.isSuccess = response.Data;
            response.Message = response.Data
                ? "Indice EquipoLigas eliminado exitosamente"
                : "Error al eliminar el Indice EquipoLigas";
        }

        return response;
    }

    public Response<EquipoLigaDTO> Get(int id)
    {
        var response = new Response<EquipoLigaDTO>();

        try
        {
            var EquipoLigas = _unitOfWork.EquipoLigas.Get(id);
            response.Data = _mapper.Map<EquipoLigaDTO>(EquipoLigas);

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

    public Response<IEnumerable<EquipoLigaDTO>> GetAll()
    {
        var response = new Response<IEnumerable<EquipoLigaDTO>>();
        try
        {
            var EquipoLigas = _unitOfWork.EquipoLigas.GetAll();
            response.Data = _mapper.Map<IEnumerable<EquipoLigaDTO>>(EquipoLigas);
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

    public ResponsePagination<IEnumerable<EquipoLigaDTO>> GetAllWithPagination(
        int PageNumber,
        int PageSize
    )
    {
        var response = new ResponsePagination<IEnumerable<EquipoLigaDTO>>();
        try
        {
            var count = _unitOfWork.EquipoLigas.Count();
            var EquipoLigas = _unitOfWork.EquipoLigas.GetAllWithPagination(PageNumber, PageSize);
            response.Data = _mapper.Map<IEnumerable<EquipoLigaDTO>>(EquipoLigas);

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
            response.Data = _unitOfWork.EquipoLigas.Count();
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

    public async Task<Response<bool>> InsertAsync(EquipoLigaDTO EquipoLigaDTO)
    {
        var response = new Response<bool>();

        try
        {
            var EquipoLigas = _mapper.Map<EquipoLiga>(EquipoLigaDTO);
            response.Data = await _unitOfWork.EquipoLigas.InsertAsync(EquipoLigas);

            if (response.Data)
            {
                response.isSuccess = true;
                response.Message = "Indice EquipoLigas creado exitosamente";
            }
        }
        catch (Exception e)
        {
            response.Message = e.Message;
        }

        return response;
    }

    public async Task<Response<bool>> UpdateAsync(EquipoLigaDTO EquipoLigaDTO)
    {
        var response = new Response<bool>();

        try
        {
            var EquipoLigas = _mapper.Map<EquipoLiga>(EquipoLigaDTO);
            response.Data = await _unitOfWork.EquipoLigas.UpdateAsync(EquipoLigas);

            if (response.Data)
            {
                response.isSuccess = true;
                response.Message = "Indice EquipoLigas actualizado exitosamente";
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
        var EquipoLigas = await _unitOfWork.EquipoLigas.GetAsync(id);

        if (EquipoLigas == null)
        {
            response.Message = "El Indice EquipoLigas no existe";
            response.isSuccess = false;
        }
        else
        {
            response.Data = await _unitOfWork.EquipoLigas.DeleteAsync(id);
            response.isSuccess = response.Data;
            response.Message = response.Data
                ? "Indice EquipoLigas eliminado exitosamente"
                : "Error al eliminar el Indice EquipoLigas";
        }

        return response;
    }

    public async Task<Response<EquipoLigaDTO>> GetAsync(int id)
    {
        var response = new Response<EquipoLigaDTO>();

        try
        {
            var EquipoLigas = await _unitOfWork.EquipoLigas.GetAsync(id);
            response.Data = _mapper.Map<EquipoLigaDTO>(EquipoLigas);

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

    public async Task<Response<IEnumerable<EquipoLigaDTO>>> GetAllAsync()
    {
        var response = new Response<IEnumerable<EquipoLigaDTO>>();
        try
        {
            var EquipoLigas = await _unitOfWork.EquipoLigas.GetAllAsync();
            response.Data = _mapper.Map<IEnumerable<EquipoLigaDTO>>(EquipoLigas);
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

    public async Task<ResponsePagination<IEnumerable<EquipoLigaDTO>>> GetAllWithPaginationAsync(
        int PageNumber,
        int PageSize
    )
    {
        var response = new ResponsePagination<IEnumerable<EquipoLigaDTO>>();
        try
        {
            var count = await _unitOfWork.EquipoLigas.CountAsync();
            var EquipoLigas = await _unitOfWork.EquipoLigas.GetAllWithPaginationAsync(PageNumber, PageSize);
            response.Data = _mapper.Map<IEnumerable<EquipoLigaDTO>>(EquipoLigas);

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
            response.Data = await _unitOfWork.EquipoLigas.CountAsync();
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