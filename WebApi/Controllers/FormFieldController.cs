using DTO.FormField;
using Interface.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

public class FormFieldController : Controller  
{
    private readonly IFormFieldApplication _FormFieldApplication;

    public FormFieldController(IFormFieldApplication FormFieldApplication)
    {
        _FormFieldApplication = FormFieldApplication;
    }

    #region Metodos sincronos

    [HttpPost("Insert")]
    public IActionResult Insert([FromBody] FormFieldDTO FormFieldsDto)
    {
        var response = _FormFieldApplication.Insert(FormFieldsDto);
        return Ok(response);
    }

    [HttpPut("Update")]
    public IActionResult Update([FromBody] FormFieldDTO FormFieldsDto)
    {
        var response = _FormFieldApplication.Update(FormFieldsDto);
        return Ok(response);
    }

    [HttpDelete("Delete")]
    public IActionResult Delete(int id)
    {
        var response = _FormFieldApplication.Delete(id);
        return Ok(response);
    }

    [HttpGet("GetAll")]
    public IActionResult GetAll()
    {
        var response = _FormFieldApplication.GetAll();
        return Ok(response);
    }

    [HttpGet("GetById/{id}")]
    public IActionResult GetById(int id)
    {
        var response = _FormFieldApplication.Get(id);
        return Ok(response);
    }

    [HttpGet("GetallWithPagination")]
    public IActionResult GetallWithPagination(int page, int pageSize)
    {
        var response = _FormFieldApplication.GetAllWithPagination(page, pageSize);
        return Ok(response);
    }

    [HttpGet("Count")]
    public IActionResult Count()
    {
        var response = _FormFieldApplication.Count();
        return Ok(response);
    }

    #endregion

    #region metodos asincronos

    [HttpPost("InsertAsync")]
    public async Task<IActionResult> InsertAsync([FromBody] FormFieldDTO FormFieldsDto)
    {
        var response = await _FormFieldApplication.InsertAsync(FormFieldsDto);
        return Ok(response);
    }

    [HttpPut("UpdateAsync")]
    public async Task<IActionResult> UpdateAsync([FromBody] FormFieldDTO FormFieldsDto)
    {
        var response = await _FormFieldApplication.UpdateAsync(FormFieldsDto);
        return Ok(response);
    }

    [HttpDelete("DeleteAsync")]
    public async Task<IActionResult> DeleteAsync(int id)
    {
        var response = await _FormFieldApplication.DeleteAsync(id);
        return Ok(response);
    }

    [HttpGet("GetAllAsync")]
    public async Task<IActionResult> GetAllAsync()
    {
        var response = await _FormFieldApplication.GetAllAsync();
        return Ok(response);
    }

    [HttpGet("GetByIdAsync/{id}")]
    public async Task<IActionResult> GetByIdAsync(int id)
    {
        var response = await _FormFieldApplication.GetAsync(id);
        return Ok(response);
    }

    [HttpGet("GetallWithPaginationAsync")]
    public async Task<IActionResult> GetallWithPaginationAsync(int page, int pageSize)
    {
        var response = await _FormFieldApplication.GetAllWithPaginationAsync(page, pageSize);
        return Ok(response);
    }

    [HttpGet("CountAsync")]
    public async Task<IActionResult> CountAsync()
    {
        var response = await _FormFieldApplication.CountAsync();
        return Ok(response);
    }

    #endregion

    [HttpGet("GetFormFieldByFormCatId/{id}")]
    public IActionResult GetFormFieldByFormCatId(int id)
    {
        var response = _FormFieldApplication.GetFormFieldByFormCatId(id);
        return Ok(response);
    }
}