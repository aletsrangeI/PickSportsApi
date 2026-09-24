using System.Security.Claims;
using DTO.Pick;
using Interface.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[Authorize]
[ApiController]
[Route("api/quinielas")]
public class PicksController : ControllerBase
{
    private readonly IPickApplication _pickApplication;

    public PicksController(IPickApplication pickApplication)
    {
        _pickApplication = pickApplication;
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                    ?? User.FindFirst(ClaimTypes.Name)?.Value;
        return int.TryParse(claim, out var id) ? id : 0;
    }

    /// <summary>
    /// GET /api/quinielas/{id}/picks?weekId={weekId}
    /// Consulta de picks: restringido a propios si PUBLISHED; público a miembros si LOCKED o SCORED.
    /// </summary>
    [HttpGet("{id}/picks")]
    public async Task<IActionResult> GetPicks(int id, [FromQuery] int weekId)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        if (weekId <= 0)
        {
            return BadRequest(new { isSuccess = false, message = "El parámetro weekId es requerido y debe ser mayor a 0." });
        }

        var response = await _pickApplication.GetPicksAsync(id, weekId, userId);
        if (!response.isSuccess)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    /// <summary>
    /// POST /api/quinielas/{id}/picks
    /// Upsert de pick con validación de horario límite y estado de jornada.
    /// </summary>
    [HttpPost("{id}/picks")]
    public async Task<IActionResult> SubmitPick(int id, [FromBody] SubmitPickRequestDto request)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        var response = await _pickApplication.SubmitPickAsync(id, userId, request);
        if (!response.isSuccess)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    /// <summary>
    /// POST /api/quinielas/{id}/weeks/{weekId}/lock-and-autofill
    /// Forzar cierre manual y autollenado aleatorio seguro con auditoría (Admin u Owner).
    /// </summary>
    [HttpPost("{id}/weeks/{weekId}/lock-and-autofill")]
    public async Task<IActionResult> LockAndAutofill(int id, int weekId)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        var response = await _pickApplication.LockAndAutofillAsync(id, weekId, userId);
        if (!response.isSuccess)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }
}
