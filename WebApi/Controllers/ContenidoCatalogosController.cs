using DTO.ContenidoCatalogo;
using Interface.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public class ContenidoCatalogo : Controller
{
    private readonly IContenidoCatalogoApplication _contenidoCatalogoApplication;

    public ContenidoCatalogo(IContenidoCatalogoApplication ContenidoCatalogoApplication)
    {
        _contenidoCatalogoApplication = ContenidoCatalogoApplication;
    }

    [HttpGet("GetContenidoCatalogoByCatalogoId/{id}")]
    public IActionResult GetContenidoCatalogoByCatalogoId(int id)
    {
        var response = _contenidoCatalogoApplication.GetContenidoCatalogoByCatalogoId(id);
        return Ok(response);
    }

    [HttpGet("GetContenidoCatalogoByCatalogoIdAsync/{id}")]
    public IActionResult GetContenidoCatalogoByCatalogoIdAsync(int id)
    {
        var response = _contenidoCatalogoApplication.GetContenidoCatalogoByCatalogoIdAsync(id);
        return Ok(response);
    }

    #region Metodos sincronos

    [HttpPost("Insert")]
    public IActionResult Insert([FromBody] ContenidoCatalogoDTO ContenidoCatalogosDto)
    {
        var response = _contenidoCatalogoApplication.Insert(ContenidoCatalogosDto);
        return Ok(response);
    }

    [HttpPut("Update")]
    public IActionResult Update([FromBody] ContenidoCatalogoDTO ContenidoCatalogosDto)
    {
        var response = _contenidoCatalogoApplication.Update(ContenidoCatalogosDto);
        return Ok(response);
    }

    [HttpDelete("Delete")]
    public IActionResult Delete(int id)
    {
        var response = _contenidoCatalogoApplication.Delete(id);
        return Ok(response);
    }

    [HttpGet("GetAll")]
    public IActionResult GetAll()
    {
        var response = _contenidoCatalogoApplication.GetAll();
        return Ok(response);
    }

    [HttpGet("GetById/{id}")]
    public IActionResult GetById(int id)
    {
        var response = _contenidoCatalogoApplication.Get(id);
        return Ok(response);
    }

    [HttpGet("GetallWithPagination")]
    public IActionResult GetallWithPagination(int page, int pageSize)
    {
        var response = _contenidoCatalogoApplication.GetAllWithPagination(page, pageSize);
        return Ok(response);
    }

    [HttpGet("Count")]
    public IActionResult Count()
    {
        var response = _contenidoCatalogoApplication.Count();
        return Ok(response);
    }

    #endregion

    #region metodos asincronos

    [HttpPost("InsertAsync")]
    public async Task<IActionResult> InsertAsync([FromBody] ContenidoCatalogoDTO ContenidoCatalogosDto)
    {
        var response = await _contenidoCatalogoApplication.InsertAsync(ContenidoCatalogosDto);
        return Ok(response);
    }

    [HttpPut("UpdateAsync")]
    public async Task<IActionResult> UpdateAsync([FromBody] ContenidoCatalogoDTO ContenidoCatalogosDto)
    {
        var response = await _contenidoCatalogoApplication.UpdateAsync(ContenidoCatalogosDto);
        return Ok(response);
    }

    [HttpDelete("DeleteAsync")]
    public async Task<IActionResult> DeleteAsync(int id)
    {
        var response = await _contenidoCatalogoApplication.DeleteAsync(id);
        return Ok(response);
    }

    [HttpGet("GetAllAsync")]
    public async Task<IActionResult> GetAllAsync()
    {
        var response = await _contenidoCatalogoApplication.GetAllAsync();
        return Ok(response);
    }

    [HttpGet("GetByIdAsync/{id}")]
    public async Task<IActionResult> GetByIdAsync(int id)
    {
        var response = await _contenidoCatalogoApplication.GetAsync(id);
        return Ok(response);
    }

    [HttpGet("GetallWithPaginationAsync")]
    public async Task<IActionResult> GetallWithPaginationAsync(int page, int pageSize)
    {
        var response = await _contenidoCatalogoApplication.GetAllWithPaginationAsync(page, pageSize);
        return Ok(response);
    }

    [HttpGet("CountAsync")]
    public async Task<IActionResult> CountAsync()
    {
        var response = await _contenidoCatalogoApplication.CountAsync();
        return Ok(response);
    }

    #endregion
}