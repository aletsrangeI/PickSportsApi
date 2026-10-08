using System.Security.Claims;
using DTO.Broadcast;
using Interface.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class MatchesController : ControllerBase
{
    private readonly IBroadcastService _broadcastService;

    public MatchesController(IBroadcastService broadcastService)
    {
        _broadcastService = broadcastService;
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? User.FindFirst(ClaimTypes.Name)?.Value;
        return int.TryParse(claim, out var id) ? id : 0;
    }

    /// <summary>
    /// PUT /api/matches/{id}/broadcasters — override manual de canales (SPEC-015).
    /// Enviar una lista vacía restablece la regla de localía.
    /// </summary>
    [HttpPut("{id}/broadcasters")]
    public async Task<IActionResult> UpdateBroadcasters(int id, [FromBody] UpdateBroadcastersDto request)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        var response = await _broadcastService.UpdateMatchBroadcastersAsync(id, request.Channels, userId);
        if (!response.isSuccess) return BadRequest(response);

        return Ok(response);
    }
}
