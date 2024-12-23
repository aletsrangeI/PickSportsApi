using AutoMapper;
using Common;
using Domain.Entities;
using DTO.FormField;
using Interface.Persistence;
using Interface.UseCases;
using Validator;

namespace UseCases.FormFields;

public class FormFieldApplication : IFormFieldApplication
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly FormFieldDTOValidator _validationRules;
    private readonly IAppLogger<FormFieldApplication> _logger;

    public FormFieldApplication(IUnitOfWork unitOfWork, IMapper mapper,
        FormFieldDTOValidator validationRules,
        IAppLogger<FormFieldApplication> logger)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _validationRules = validationRules;
        _logger = logger;
    }

    #region Metodos sincronos

    public Response<bool> Insert(FormFieldDTO FormFieldDto)
    {
        var response = new Response<bool>();

        try
        {
            var FormField = _mapper.Map<FormField>(FormFieldDto);
            response.Data = _unitOfWork.FormFields.Insert(FormField);

            if (response.Data)
            {
                response.isSuccess = true;
                response.Message = "FormField creado exitosamente";
            }
        }
        catch (Exception e)
        {
            response.Message = e.Message;
        }

        return response;
    }

    public Response<bool> Update(FormFieldDTO FormFieldDto)
    {
        var response = new Response<bool>();

        try
        {
            var FormField = _mapper.Map<FormField>(FormFieldDto);
            response.Data = _unitOfWork.FormFields.Update(FormField);

            if (response.Data)
            {
                response.isSuccess = true;
                response.Message = "FormField actualizado exitosamente";
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
        var FormField = _unitOfWork.FormFields.Get(id);

        if (FormField == null)
        {
            response.Message = "El FormField no existe";
            response.isSuccess = false;
        }
        else
        {
            response.Data = _unitOfWork.FormFields.Delete(id);
            response.isSuccess = response.Data;
            response.Message = response.Data
                ? "FormField eliminado exitosamente"
                : "Error al eliminar el FormField";
        }

        return response;
    }

    public Response<FormFieldDTO> Get(int id)
    {
        var response = new Response<FormFieldDTO>();

        try
        {
            var FormField = _unitOfWork.FormFields.Get(id);
            response.Data = _mapper.Map<FormFieldDTO>(FormField);

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

    public Response<IEnumerable<FormFieldDTO>> GetAll()
    {
        var response = new Response<IEnumerable<FormFieldDTO>>();
        try
        {
            var FormFields = _unitOfWork.FormFields.GetAll();
            response.Data = _mapper.Map<IEnumerable<FormFieldDTO>>(FormFields);
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

    public ResponsePagination<IEnumerable<FormFieldDTO>> GetAllWithPagination(
        int PageNumber,
        int PageSize
    )
    {
        var response = new ResponsePagination<IEnumerable<FormFieldDTO>>();
        try
        {
            var count = _unitOfWork.FormFields.Count();
            var FormFields = _unitOfWork.FormFields.GetAllWithPagination(PageNumber, PageSize);
            response.Data = _mapper.Map<IEnumerable<FormFieldDTO>>(FormFields);

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
            response.Data = _unitOfWork.FormFields.Count();
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

    public async Task<Response<bool>> InsertAsync(FormFieldDTO FormFieldDto)
    {
        var response = new Response<bool>();

        try
        {
            var FormField = _mapper.Map<FormField>(FormFieldDto);
            response.Data = await _unitOfWork.FormFields.InsertAsync(FormField);

            if (response.Data)
            {
                response.isSuccess = true;
                response.Message = "FormField creado exitosamente";
            }
        }
        catch (Exception e)
        {
            response.Message = e.Message;
        }

        return response;
    }

    public async Task<Response<bool>> UpdateAsync(FormFieldDTO FormFieldDto)
    {
        var response = new Response<bool>();

        try
        {
            var FormField = _mapper.Map<FormField>(FormFieldDto);
            response.Data = await _unitOfWork.FormFields.UpdateAsync(FormField);

            if (response.Data)
            {
                response.isSuccess = true;
                response.Message = "FormField actualizado exitosamente";
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
        var FormField = await _unitOfWork.FormFields.GetAsync(id);

        if (FormField == null)
        {
            response.Message = "El FormField no existe";
            response.isSuccess = false;
        }
        else
        {
            response.Data = await _unitOfWork.FormFields.DeleteAsync(id);
            response.isSuccess = response.Data;
            response.Message = response.Data
                ? "FormField eliminado exitosamente"
                : "Error al eliminar el FormField";
        }

        return response;
    }

    public async Task<Response<FormFieldDTO>> GetAsync(int id)
    {
        var response = new Response<FormFieldDTO>();

        try
        {
            var FormField = await _unitOfWork.FormFields.GetAsync(id);
            response.Data = _mapper.Map<FormFieldDTO>(FormField);

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

    public async Task<Response<IEnumerable<FormFieldDTO>>> GetAllAsync()
    {
        var response = new Response<IEnumerable<FormFieldDTO>>();
        try
        {
            var FormFields = await _unitOfWork.FormFields.GetAllAsync();
            response.Data = _mapper.Map<IEnumerable<FormFieldDTO>>(FormFields);
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

    public async Task<ResponsePagination<IEnumerable<FormFieldDTO>>> GetAllWithPaginationAsync(
        int PageNumber,
        int PageSize
    )
    {
        var response = new ResponsePagination<IEnumerable<FormFieldDTO>>();
        try
        {
            var count = await _unitOfWork.FormFields.CountAsync();
            var FormFields =
                await _unitOfWork.FormFields.GetAllWithPaginationAsync(PageNumber, PageSize);
            response.Data = _mapper.Map<IEnumerable<FormFieldDTO>>(FormFields);

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
            response.Data = await _unitOfWork.FormFields.CountAsync();
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

    public Response<List<FormFieldDTO>> GetFormFieldByFormCatId(int id)
    {
        var response = new Response<List<FormFieldDTO>>();

        try
        {
            var FormField = _unitOfWork.FormFields.GetFormFieldByFormCatId(id);
            response.Data = _mapper.Map<List<FormFieldDTO>>(FormField);

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

    #endregion
}