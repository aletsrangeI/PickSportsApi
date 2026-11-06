using System.Security.Claims;
using DTO.Duels;
using Interface.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

/// <summary>SPEC-013 Etapa 2: duelos 1 vs 1, tabla de duelos y Salon de la Fama. Sin logica: delega en IDuelApplication.</summary>
[Authorize]
[ApiController]
[Route("api")]
public class DuelsController : ControllerBase
{
    private readonly IDuelApplication _duelApplication;

    public DuelsController(IDuelApplication duelApplication)
    {
        _duelApplication = duelApplication;
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? User.FindFirst(ClaimTypes.Name)?.Value;
        return int.TryParse(claim, out var id) ? id : 0;
    }

    [HttpPost("quinielas/{quinielaId:int}/duels/schedule")]
    public async Task<IActionResult> Schedule(int quinielaId)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        var response = await _duelApplication.ScheduleAsync(quinielaId, userId);
        if (!response.isSuccess) return Failure(response);
        return Ok(response);
    }

    [HttpGet("quinielas/{quinielaId:int}/duels/schedule")]
    public async Task<IActionResult> GetSchedule(int quinielaId)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        var response = await _duelApplication.GetScheduleAsync(quinielaId, userId);
        if (!response.isSuccess) return Failure(response);
        return Ok(response);
    }

    [HttpGet("quinielas/{quinielaId:int}/duels")]
    public async Task<IActionResult> GetFixtures(int quinielaId, [FromQuery] int? weekNumber)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        var response = await _duelApplication.GetFixturesAsync(quinielaId, userId, weekNumber);
        if (!response.isSuccess) return Failure(response);
        return Ok(response);
    }

    [HttpGet("quinielas/{quinielaId:int}/duels/standings")]
    public async Task<IActionResult> GetStandings(int quinielaId)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        var response = await _duelApplication.GetStandingsAsync(quinielaId, userId);
        if (!response.isSuccess) return Failure(response);
        return Ok(response);
    }

    [HttpGet("quinielas/{quinielaId:int}/duels/{fixtureId:int}/matchup")]
    public async Task<IActionResult> GetMatchup(int quinielaId, int fixtureId)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        var response = await _duelApplication.GetMatchupAsync(quinielaId, userId, fixtureId);
        if (!response.isSuccess) return Failure(response);
        return Ok(response);
    }

    [HttpPut("quinielas/{quinielaId:int}/duels/cup")]
    public async Task<IActionResult> UpdateCup(int quinielaId, [FromBody] UpdateDuelCupDto request)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        var response = await _duelApplication.UpdateCupAsync(quinielaId, userId, request);
        if (!response.isSuccess) return Failure(response);
        return Ok(response);
    }

    [HttpGet("duels/hall-of-fame")]
    public async Task<IActionResult> GetHallOfFame()
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        var response = await _duelApplication.GetHallOfFameAsync(userId);
        if (!response.isSuccess) return Failure(response);
        return Ok(response);
    }

    private IActionResult Failure<T>(Common.Response<T> response) =>
        response.Message != null && response.Message.EndsWith("no encontrado.", StringComparison.OrdinalIgnoreCase)
            ? NotFound(response)
            : BadRequest(response);
}
