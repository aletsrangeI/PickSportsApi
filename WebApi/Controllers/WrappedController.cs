using System.Security.Claims;
using DTO.Wrapped;
using Interface.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[Authorize]
[ApiController]
public class WrappedController : ControllerBase
{
    private readonly IWrappedApplication _wrappedApplication;

    public WrappedController(IWrappedApplication wrappedApplication)
    {
        _wrappedApplication = wrappedApplication;
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? User.FindFirst(ClaimTypes.Name)?.Value;
        return int.TryParse(claim, out var id) ? id : 0;
    }

    /// <summary>GET api/quinielas/{quinielaId}/seasons/{seasonId}/wrapped</summary>
    [HttpGet("api/quinielas/{quinielaId:int}/seasons/{seasonId:int}/wrapped")]
    public async Task<IActionResult> GetWrapped(int quinielaId, int seasonId)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        var response = await _wrappedApplication.GetWrappedAsync(quinielaId, seasonId, userId);
        if (!response.isSuccess) return BadRequest(response);
        return Ok(response);
    }

    /// <summary>POST api/quinielas/{quinielaId}/seasons/{seasonId}/wrapped/generate-all</summary>
    [HttpPost("api/quinielas/{quinielaId:int}/seasons/{seasonId:int}/wrapped/generate-all")]
    public async Task<IActionResult> GenerateAll(int quinielaId, int seasonId)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        var response = await _wrappedApplication.GenerateAllAsync(quinielaId, seasonId, userId);
        if (!response.isSuccess) return BadRequest(response);
        return Ok(response);
    }

    /// <summary>GET api/quinielas/{quinielaId}/wrapped/availability</summary>
    [HttpGet("api/quinielas/{quinielaId:int}/wrapped/availability")]
    public async Task<IActionResult> GetAvailability(int quinielaId)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        var response = await _wrappedApplication.GetAvailabilityAsync(quinielaId, userId);
        if (!response.isSuccess) return BadRequest(response);
        return Ok(response);
    }

    /// <summary>PUT api/seasons/{seasonId}/finish</summary>
    [HttpPut("api/seasons/{seasonId:int}/finish")]
    public async Task<IActionResult> SetSeasonFinished(int seasonId, [FromBody] SetSeasonFinishedDto dto)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        var response = await _wrappedApplication.SetSeasonFinishedAsync(seasonId, dto.IsFinished, userId);
        if (!response.isSuccess) return BadRequest(response);
        return Ok(response);
    }
}
