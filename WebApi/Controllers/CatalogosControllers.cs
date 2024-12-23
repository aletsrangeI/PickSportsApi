using DTO.Catalogo;
using Interface.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TimelyIO.Service.WebApi.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public class IndiceCatalogo : Controller
{
    private readonly ICatalogosApplication _indiceCatalogoApplication;

    public IndiceCatalogo(ICatalogosApplication indiceCatalogoApplication)
    {
        _indiceCatalogoApplication = indiceCatalogoApplication;
    }

    #region Metodos sincronos

    [HttpPost("Insert")]
    public IActionResult Insert([FromBody] CatalogoDTO IndiceCatalogosDto)
    {
        var response = _indiceCatalogoApplication.Insert(IndiceCatalogosDto);
        return Ok(response);
    }

    [HttpPut("Update")]
    public IActionResult Update([FromBody] CatalogoDTO IndiceCatalogosDto)
    {
        var response = _indiceCatalogoApplication.Update(IndiceCatalogosDto);
        return Ok(response);
    }

    [HttpDelete("Delete")]
    public IActionResult Delete(int id)
    {
        var response = _indiceCatalogoApplication.Delete(id);
        return Ok(response);
    }

    [HttpGet("GetAll")]
    public IActionResult GetAll()
    {
        var response = _indiceCatalogoApplication.GetAll();
        return Ok(response);
    }

    [HttpGet("GetById/{id}")]
    public IActionResult GetById(int id)
    {
        var response = _indiceCatalogoApplication.Get(id);
        return Ok(response);
    }

    [HttpGet("GetallWithPagination")]
    public IActionResult GetallWithPagination(int page, int pageSize)
    {
        var response = _indiceCatalogoApplication.GetAllWithPagination(page, pageSize);
        return Ok(response);
    }

    [HttpGet("Count")]
    public IActionResult Count()
    {
        var response = _indiceCatalogoApplication.Count();
        return Ok(response);
    }

    #endregion

    #region metodos asincronos

    [HttpPost("InsertAsync")]
    public async Task<IActionResult> InsertAsync([FromBody] CatalogoDTO IndiceCatalogosDto)
    {
        var response = await _indiceCatalogoApplication.InsertAsync(IndiceCatalogosDto);
        return Ok(response);
    }

    [HttpPut("UpdateAsync")]
    public async Task<IActionResult> UpdateAsync([FromBody] CatalogoDTO IndiceCatalogosDto)
    {
        var response = await _indiceCatalogoApplication.UpdateAsync(IndiceCatalogosDto);
        return Ok(response);
    }

    [HttpDelete("DeleteAsync")]
    public async Task<IActionResult> DeleteAsync(int id)
    {
        var response = await _indiceCatalogoApplication.DeleteAsync(id);
        return Ok(response);
    }

    [HttpGet("GetAllAsync")]
    public async Task<IActionResult> GetAllAsync()
    {
        var response = await _indiceCatalogoApplication.GetAllAsync();
        return Ok(response);
    }

    [HttpGet("GetByIdAsync/{id}")]
    public async Task<IActionResult> GetByIdAsync(int id)
    {
        var response = await _indiceCatalogoApplication.GetAsync(id);
        return Ok(response);
    }

    [HttpGet("GetallWithPaginationAsync")]
    public async Task<IActionResult> GetallWithPaginationAsync(int page, int pageSize)
    {
        var response = await _indiceCatalogoApplication.GetAllWithPaginationAsync(page, pageSize);
        return Ok(response);
    }

    [HttpGet("CountAsync")]
    public async Task<IActionResult> CountAsync()
    {
        var response = await _indiceCatalogoApplication.CountAsync();
        return Ok(response);
    }

    #endregion
}