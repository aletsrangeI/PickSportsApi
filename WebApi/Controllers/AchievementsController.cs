using System.Security.Claims;
using DTO.Gamification;
using Interface.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

/// <summary>SPEC-013: trofeos, vitrina por temporada y titulos equipables.</summary>
[Authorize]
[ApiController]
[Route("api/achievements")]
public class AchievementsController : ControllerBase
{
    private readonly IAchievementApplication _achievementApplication;

    public AchievementsController(IAchievementApplication achievementApplication)
    {
        _achievementApplication = achievementApplication;
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? User.FindFirst(ClaimTypes.Name)?.Value;
        return int.TryParse(claim, out var id) ? id : 0;
    }

    [HttpGet("unseen")]
    public async Task<IActionResult> GetUnseen()
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        var response = await _achievementApplication.GetUnseenAsync(userId);
        return Ok(response);
    }

    [HttpPost("{id:int}/acknowledge")]
    public async Task<IActionResult> Acknowledge(int id)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        var response = await _achievementApplication.AcknowledgeAsync(userId, id);
        if (!response.isSuccess) return NotFound(response);

        return Ok(response);
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMyShowcase([FromQuery] int? seasonId)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        var response = await _achievementApplication.GetShowcaseAsync(userId, seasonId);
        if (!response.isSuccess) return BadRequest(response);

        return Ok(response);
    }

    [HttpPut("me/title")]
    public async Task<IActionResult> EquipTitle([FromBody] EquipTitleDto request)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        var response = await _achievementApplication.EquipTitleAsync(userId, request.Code);
        if (!response.isSuccess) return BadRequest(response);

        return Ok(response);
    }
}
