using System.Security.Claims;
using DTO.CustomEvents;
using Interface.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[Authorize]
[ApiController]
[Route("api/quinielas/{quinielaId:int}/events")]
public class CustomEventsController : ControllerBase
{
    private readonly ICustomEventApplication _customEventApplication;

    public CustomEventsController(ICustomEventApplication customEventApplication)
    {
        _customEventApplication = customEventApplication;
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                    ?? User.FindFirst(ClaimTypes.Name)?.Value;
        return int.TryParse(claim, out var id) ? id : 0;
    }

    [HttpGet("rounds")]
    public async Task<IActionResult> GetRounds(int quinielaId)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        var response = await _customEventApplication.GetRoundsAsync(quinielaId, userId);
        if (!response.isSuccess)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    [HttpGet("rounds/{roundId:int}")]
    public async Task<IActionResult> GetRoundDetail(int quinielaId, int roundId)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        var response = await _customEventApplication.GetRoundDetailAsync(quinielaId, roundId, userId);
        if (!response.isSuccess)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    [HttpPost("rounds")]
    public async Task<IActionResult> CreateRound(int quinielaId, [FromBody] CreateEventRoundDto request)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        var response = await _customEventApplication.CreateRoundAsync(quinielaId, userId, request);
        if (!response.isSuccess)
        {
            return BadRequest(response);
        }

        return CreatedAtAction(nameof(GetRoundDetail), new { quinielaId, roundId = response.Data.Id }, response);
    }

    [HttpPut("rounds/{roundId:int}")]
    public async Task<IActionResult> UpdateRound(int quinielaId, int roundId, [FromBody] UpdateEventRoundDto request)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        var response = await _customEventApplication.UpdateRoundAsync(quinielaId, roundId, userId, request);
        if (!response.isSuccess)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    [HttpPost("rounds/{roundId:int}/questions")]
    public async Task<IActionResult> AddQuestion(int quinielaId, int roundId, [FromBody] CreateEventQuestionDto request)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        var response = await _customEventApplication.AddQuestionAsync(quinielaId, roundId, userId, request);
        if (!response.isSuccess)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    [HttpPost("rounds/{roundId:int}/picks")]
    public async Task<IActionResult> SubmitPicks(int quinielaId, int roundId, [FromBody] SubmitEventPicksDto request)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        var response = await _customEventApplication.SubmitPicksAsync(quinielaId, roundId, userId, request);
        if (!response.isSuccess)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    [HttpPost("rounds/{roundId:int}/resolve")]
    public async Task<IActionResult> ResolveRound(int quinielaId, int roundId, [FromBody] ResolveEventRoundDto request)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        var response = await _customEventApplication.ResolveRoundAsync(quinielaId, roundId, userId, request);
        if (!response.isSuccess)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    [HttpGet("standings")]
    public async Task<IActionResult> GetStandings(int quinielaId, [FromQuery] int? roundId)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        var response = await _customEventApplication.GetStandingsAsync(quinielaId, roundId, userId);
        if (!response.isSuccess)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }
}
