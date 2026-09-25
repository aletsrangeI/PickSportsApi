using System.Security.Claims;
using DTO.Bulletin;
using Interface.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[Authorize]
[ApiController]
[Route("api/quinielas/{quinielaId:int}/bulletin")]
public class BulletinController : ControllerBase
{
    private readonly IBulletinApplication _bulletinApplication;

    public BulletinController(IBulletinApplication bulletinApplication)
    {
        _bulletinApplication = bulletinApplication;
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? User.FindFirst(ClaimTypes.Name)?.Value;
        return int.TryParse(claim, out var id) ? id : 0;
    }

    /// <summary>
    /// GET /api/quinielas/{quinielaId}/bulletin?weekId={weekId}
    /// Obtiene la edición del Periódico Semanal. Si no se envía weekId,
    /// retorna la última jornada calificada (SCORED) o la activa.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetBulletin(int quinielaId, [FromQuery] int? weekId)
    {
        var response = await _bulletinApplication.GetBulletinAsync(quinielaId, weekId);
        if (!response.isSuccess)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    /// <summary>
    /// PUT /api/quinielas/{quinielaId}/bulletin/{weekId}/announcement
    /// Publica o edita el anuncio oficial del administrador para la jornada (Solo Admin/Owner).
    /// </summary>
    [HttpPut("{weekId:int}/announcement")]
    public async Task<IActionResult> UpdateAnnouncement(int quinielaId, int weekId, [FromBody] UpdateAnnouncementDto dto)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        var response = await _bulletinApplication.UpdateAnnouncementAsync(quinielaId, weekId, userId, dto);
        if (!response.isSuccess)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }
}
