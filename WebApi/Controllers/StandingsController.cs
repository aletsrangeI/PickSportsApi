using System.Security.Claims;
using Interface.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[Authorize]
[ApiController]
[Route("api/quinielas")]
public class StandingsController : ControllerBase
{
    private readonly IScoringApplication _scoringApplication;

    public StandingsController(IScoringApplication scoringApplication)
    {
        _scoringApplication = scoringApplication;
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                    ?? User.FindFirst(ClaimTypes.Name)?.Value;
        return int.TryParse(claim, out var id) ? id : 0;
    }

    /// <summary>
    /// GET /api/quinielas/{id}/standings?weekId={weekId}
    /// Obtiene la tabla semanal con desempates en cascada y la tabla general acumulada.
    /// </summary>
    [HttpGet("{id}/standings")]
    public async Task<IActionResult> GetStandings(int id, [FromQuery] int weekId)
    {
        if (weekId <= 0)
        {
            return BadRequest(new { isSuccess = false, message = "El parámetro weekId es requerido y debe ser mayor a 0." });
        }

        var response = await _scoringApplication.GetStandingsAsync(id, weekId);
        if (!response.isSuccess)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    /// <summary>
    /// POST /api/quinielas/{id}/weeks/{weekId}/score
    /// Ejecuta la calificación de la jornada, asigna galardones y actualiza totales acumulados.
    /// </summary>
    [HttpPost("{id}/weeks/{weekId}/score")]
    public async Task<IActionResult> ScoreWeek(int id, int weekId)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        var response = await _scoringApplication.ScoreWeekAsync(id, weekId, userId);
        if (!response.isSuccess)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    /// <summary>
    /// GET /api/quinielas/{id}/awards?weekId={weekId}
    /// Obtiene los galardones de una jornada o la vitrina histórica si no se especifica weekId.
    /// </summary>
    [HttpGet("{id}/awards")]
    public async Task<IActionResult> GetAwards(int id, [FromQuery] int? weekId)
    {
        var response = await _scoringApplication.GetAwardsAsync(id, weekId);
        if (!response.isSuccess)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }
}
