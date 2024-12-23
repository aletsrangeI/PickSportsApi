using AutoMapper;
using Common;
using Domain.Entities;
using DTO.Catalogo;
using Interface.Persistence;
using Interface.UseCases;
using Validator;

namespace UseCases.Catalogos;

public class CatalogosApplication : ICatalogosApplication
{
    private readonly IAppLogger<CatalogosApplication> _logger;
    private readonly IMapper _mapper;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CatalogosDTOValidator _validationRules;

    public CatalogosApplication(IUnitOfWork unitOfWork, IMapper mapper, CatalogosDTOValidator validationRules,
        IAppLogger<CatalogosApplication> logger)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _validationRules = validationRules;
        _logger = logger;
    }

    #region Metodos sincronos

    public Response<bool> Insert(CatalogoDTO CatalogoDTO)
    {
        var response = new Response<bool>();

        try
        {
            var Catalogo = _mapper.Map<Catalogo>(CatalogoDTO);
            response.Data = _unitOfWork.Catalogos.Insert(Catalogo);

            if (response.Data)
            {
                response.isSuccess = true;
                response.Message = "Indice Catalogo creado exitosamente";
            }
        }
        catch (Exception e)
        {
            response.Message = e.Message;
        }

        return response;
    }

    public Response<bool> Update(CatalogoDTO CatalogoDTO)
    {
        var response = new Response<bool>();

        try
        {
            var Catalogo = _mapper.Map<Catalogo>(CatalogoDTO);
            response.Data = _unitOfWork.Catalogos.Update(Catalogo);

            if (response.Data)
            {
                response.isSuccess = true;
                response.Message = "Indice Catalogo actualizado exitosamente";
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
        var Catalogo = _unitOfWork.Catalogos.Get(id);

        if (Catalogo == null)
        {
            response.Message = "El Indice Catalogo no existe";
            response.isSuccess = false;
        }
        else
        {
            response.Data = _unitOfWork.Catalogos.Delete(id);
            response.isSuccess = response.Data;
            response.Message = response.Data
                ? "Indice Catalogo eliminado exitosamente"
                : "Error al eliminar el Indice Catalogo";
        }

        return response;
    }

    public Response<CatalogoDTO> Get(int id)
    {
        var response = new Response<CatalogoDTO>();

        try
        {
            var Catalogo = _unitOfWork.Catalogos.Get(id);
            response.Data = _mapper.Map<CatalogoDTO>(Catalogo);

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

    public Response<IEnumerable<CatalogoDTO>> GetAll()
    {
        var response = new Response<IEnumerable<CatalogoDTO>>();
        try
        {
            var Catalogos = _unitOfWork.Catalogos.GetAll();
            response.Data = _mapper.Map<IEnumerable<CatalogoDTO>>(Catalogos);
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

    public ResponsePagination<IEnumerable<CatalogoDTO>> GetAllWithPagination(
        int PageNumber,
        int PageSize
    )
    {
        var response = new ResponsePagination<IEnumerable<CatalogoDTO>>();
        try
        {
            var count = _unitOfWork.Catalogos.Count();
            var Catalogos = _unitOfWork.Catalogos.GetAllWithPagination(PageNumber, PageSize);
            response.Data = _mapper.Map<IEnumerable<CatalogoDTO>>(Catalogos);

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
            response.Data = _unitOfWork.Catalogos.Count();
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

    public async Task<Response<bool>> InsertAsync(CatalogoDTO CatalogoDTO)
    {
        var response = new Response<bool>();

        try
        {
            var Catalogo = _mapper.Map<Catalogo>(CatalogoDTO);
            response.Data = await _unitOfWork.Catalogos.InsertAsync(Catalogo);

            if (response.Data)
            {
                response.isSuccess = true;
                response.Message = "Indice Catalogo creado exitosamente";
            }
        }
        catch (Exception e)
        {
            response.Message = e.Message;
        }

        return response;
    }

    public async Task<Response<bool>> UpdateAsync(CatalogoDTO CatalogoDTO)
    {
        var response = new Response<bool>();

        try
        {
            var Catalogo = _mapper.Map<Catalogo>(CatalogoDTO);
            response.Data = await _unitOfWork.Catalogos.UpdateAsync(Catalogo);

            if (response.Data)
            {
                response.isSuccess = true;
                response.Message = "Indice Catalogo actualizado exitosamente";
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
        var Catalogo = await _unitOfWork.Catalogos.GetAsync(id);

        if (Catalogo == null)
        {
            response.Message = "El Indice Catalogo no existe";
            response.isSuccess = false;
        }
        else
        {
            response.Data = await _unitOfWork.Catalogos.DeleteAsync(id);
            response.isSuccess = response.Data;
            response.Message = response.Data
                ? "Indice Catalogo eliminado exitosamente"
                : "Error al eliminar el Indice Catalogo";
        }

        return response;
    }

    public async Task<Response<CatalogoDTO>> GetAsync(int id)
    {
        var response = new Response<CatalogoDTO>();

        try
        {
            var Catalogo = await _unitOfWork.Catalogos.GetAsync(id);
            response.Data = _mapper.Map<CatalogoDTO>(Catalogo);

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

    public async Task<Response<IEnumerable<CatalogoDTO>>> GetAllAsync()
    {
        var response = new Response<IEnumerable<CatalogoDTO>>();
        try
        {
            var Catalogos = await _unitOfWork.Catalogos.GetAllAsync();
            response.Data = _mapper.Map<IEnumerable<CatalogoDTO>>(Catalogos);
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

    public async Task<ResponsePagination<IEnumerable<CatalogoDTO>>> GetAllWithPaginationAsync(
        int PageNumber,
        int PageSize
    )
    {
        var response = new ResponsePagination<IEnumerable<CatalogoDTO>>();
        try
        {
            var count = await _unitOfWork.Catalogos.CountAsync();
            var Catalogos = await _unitOfWork.Catalogos.GetAllWithPaginationAsync(PageNumber, PageSize);
            response.Data = _mapper.Map<IEnumerable<CatalogoDTO>>(Catalogos);

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
            response.Data = await _unitOfWork.Catalogos.CountAsync();
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