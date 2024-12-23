using AutoMapper;
using Common;
using Domain.Entities;
using DTO.ContenidoCatalogo;
using Interface.Persistence;
using Interface.UseCases;
using Validator;

namespace UseCases.ContenidoCatalogos;

public class ContenidoCatalogosApplication : IContenidoCatalogoApplication
{
    private readonly IAppLogger<ContenidoCatalogosApplication> _logger;
    private readonly IMapper _mapper;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ContenidoCatalogosDTOValidator _validationRules;

    public ContenidoCatalogosApplication(IUnitOfWork unitOfWork, IMapper mapper,
        ContenidoCatalogosDTOValidator validationRules,
        IAppLogger<ContenidoCatalogosApplication> logger)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _validationRules = validationRules;
        _logger = logger;
    }

    public Response<IEnumerable<ContenidoCatalogoDTO>> GetContenidoCatalogoByCatalogoId(int id)
    {
        var response = new Response<IEnumerable<ContenidoCatalogoDTO>>();
        try
        {
            var ContenidoCatalogos = _unitOfWork.ContenidoCatalogos.GetContenidoCatalogoByCatalogoId(id);
            response.Data = _mapper.Map<IEnumerable<ContenidoCatalogoDTO>>(ContenidoCatalogos);
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

    #region Metodos sincronos

    public Response<bool> Insert(ContenidoCatalogoDTO ContenidoCatalogoDto)
    {
        var response = new Response<bool>();

        try
        {
            var ContenidoCatalogo = _mapper.Map<ContenidoCatalogo>(ContenidoCatalogoDto);
            response.Data = _unitOfWork.ContenidoCatalogos.Insert(ContenidoCatalogo);

            if (response.Data)
            {
                response.isSuccess = true;
                response.Message = "Contenido Catalogo creado exitosamente";
            }
        }
        catch (Exception e)
        {
            response.Message = e.Message;
        }

        return response;
    }

    public Response<bool> Update(ContenidoCatalogoDTO ContenidoCatalogoDto)
    {
        var response = new Response<bool>();

        try
        {
            var ContenidoCatalogo = _mapper.Map<ContenidoCatalogo>(ContenidoCatalogoDto);
            response.Data = _unitOfWork.ContenidoCatalogos.Update(ContenidoCatalogo);

            if (response.Data)
            {
                response.isSuccess = true;
                response.Message = "Contenido Catalogo actualizado exitosamente";
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
        var ContenidoCatalogo = _unitOfWork.ContenidoCatalogos.Get(id);

        if (ContenidoCatalogo == null)
        {
            response.Message = "El Contenido Catalogo no existe";
            response.isSuccess = false;
        }
        else
        {
            response.Data = _unitOfWork.ContenidoCatalogos.Delete(id);
            response.isSuccess = response.Data;
            response.Message = response.Data
                ? "Contenido Catalogo eliminado exitosamente"
                : "Error al eliminar el Contenido Catalogo";
        }

        return response;
    }

    public Response<ContenidoCatalogoDTO> Get(int id)
    {
        var response = new Response<ContenidoCatalogoDTO>();

        try
        {
            var ContenidoCatalogo = _unitOfWork.ContenidoCatalogos.Get(id);
            response.Data = _mapper.Map<ContenidoCatalogoDTO>(ContenidoCatalogo);

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

    public Response<IEnumerable<ContenidoCatalogoDTO>> GetAll()
    {
        var response = new Response<IEnumerable<ContenidoCatalogoDTO>>();
        try
        {
            var ContenidoCatalogos = _unitOfWork.ContenidoCatalogos.GetAll();
            response.Data = _mapper.Map<IEnumerable<ContenidoCatalogoDTO>>(ContenidoCatalogos);
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

    public ResponsePagination<IEnumerable<ContenidoCatalogoDTO>> GetAllWithPagination(
        int PageNumber,
        int PageSize
    )
    {
        var response = new ResponsePagination<IEnumerable<ContenidoCatalogoDTO>>();
        try
        {
            var count = _unitOfWork.ContenidoCatalogos.Count();
            var ContenidoCatalogos = _unitOfWork.ContenidoCatalogos.GetAllWithPagination(PageNumber, PageSize);
            response.Data = _mapper.Map<IEnumerable<ContenidoCatalogoDTO>>(ContenidoCatalogos);

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
            response.Data = _unitOfWork.ContenidoCatalogos.Count();
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

    public async Task<Response<bool>> InsertAsync(ContenidoCatalogoDTO ContenidoCatalogoDto)
    {
        var response = new Response<bool>();

        try
        {
            var ContenidoCatalogo = _mapper.Map<ContenidoCatalogo>(ContenidoCatalogoDto);
            response.Data = await _unitOfWork.ContenidoCatalogos.InsertAsync(ContenidoCatalogo);

            if (response.Data)
            {
                response.isSuccess = true;
                response.Message = "Contenido Catalogo creado exitosamente";
            }
        }
        catch (Exception e)
        {
            response.Message = e.Message;
        }

        return response;
    }

    public async Task<Response<bool>> UpdateAsync(ContenidoCatalogoDTO ContenidoCatalogoDto)
    {
        var response = new Response<bool>();

        try
        {
            var ContenidoCatalogo = _mapper.Map<ContenidoCatalogo>(ContenidoCatalogoDto);
            response.Data = await _unitOfWork.ContenidoCatalogos.UpdateAsync(ContenidoCatalogo);

            if (response.Data)
            {
                response.isSuccess = true;
                response.Message = "Contenido Catalogo actualizado exitosamente";
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
        var ContenidoCatalogo = await _unitOfWork.ContenidoCatalogos.GetAsync(id);

        if (ContenidoCatalogo == null)
        {
            response.Message = "El Contenido Catalogo no existe";
            response.isSuccess = false;
        }
        else
        {
            response.Data = await _unitOfWork.ContenidoCatalogos.DeleteAsync(id);
            response.isSuccess = response.Data;
            response.Message = response.Data
                ? "Contenido Catalogo eliminado exitosamente"
                : "Error al eliminar el Contenido Catalogo";
        }

        return response;
    }

    public async Task<Response<ContenidoCatalogoDTO>> GetAsync(int id)
    {
        var response = new Response<ContenidoCatalogoDTO>();

        try
        {
            var ContenidoCatalogo = await _unitOfWork.ContenidoCatalogos.GetAsync(id);
            response.Data = _mapper.Map<ContenidoCatalogoDTO>(ContenidoCatalogo);

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

    public async Task<Response<IEnumerable<ContenidoCatalogoDTO>>> GetAllAsync()
    {
        var response = new Response<IEnumerable<ContenidoCatalogoDTO>>();
        try
        {
            var ContenidoCatalogos = await _unitOfWork.ContenidoCatalogos.GetAllAsync();
            response.Data = _mapper.Map<IEnumerable<ContenidoCatalogoDTO>>(ContenidoCatalogos);
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

    public async Task<ResponsePagination<IEnumerable<ContenidoCatalogoDTO>>> GetAllWithPaginationAsync(
        int PageNumber,
        int PageSize
    )
    {
        var response = new ResponsePagination<IEnumerable<ContenidoCatalogoDTO>>();
        try
        {
            var count = await _unitOfWork.ContenidoCatalogos.CountAsync();
            var ContenidoCatalogos =
                await _unitOfWork.ContenidoCatalogos.GetAllWithPaginationAsync(PageNumber, PageSize);
            response.Data = _mapper.Map<IEnumerable<ContenidoCatalogoDTO>>(ContenidoCatalogos);

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
            response.Data = await _unitOfWork.ContenidoCatalogos.CountAsync();
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

    public async Task<Response<IEnumerable<ContenidoCatalogoDTO>>> GetContenidoCatalogoByCatalogoIdAsync(int id)
    {
        var response = new Response<IEnumerable<ContenidoCatalogoDTO>>();
        try
        {
            var ContenidoCatalogos = _unitOfWork.ContenidoCatalogos.GetContenidoCatalogoByCatalogoId(id);
            response.Data = _mapper.Map<IEnumerable<ContenidoCatalogoDTO>>(ContenidoCatalogos);
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

    #endregion
}