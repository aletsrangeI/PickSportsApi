using System.Security.Claims;
using Common;
using DTO.Challenges;
using Interface.Persistence;
using Interface.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[Authorize]
[ApiController]
[Route("api/quinielas/{quinielaId:int}")]
public class ChallengesController : ControllerBase
{
    private readonly IChallengeApplication _challengeApplication;
    private readonly IUnitOfWork _unitOfWork;

    public ChallengesController(IChallengeApplication challengeApplication, IUnitOfWork unitOfWork)
    {
        _challengeApplication = challengeApplication;
        _unitOfWork = unitOfWork;
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? User.FindFirst(ClaimTypes.Name)?.Value;
        return int.TryParse(claim, out var id) ? id : 0;
    }

    private async Task<bool> IsUserAdminAsync(int quinielaId)
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.Equals(role, "ADMIN", StringComparison.OrdinalIgnoreCase))
            return true;

        var userId = GetCurrentUserId();
        if (userId <= 0) return false;

        var member = await _unitOfWork.QuinielaMembers.GetMembershipAsync(quinielaId, userId);
        return member != null && (string.Equals(member.Role, "ADMIN", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(member.Role, "OWNER", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// GET /api/quinielas/{quinielaId}/weeks/{weekId}/challenges
    /// Obtiene todos los retos de la jornada (con progreso en vivo, eventos de Doble Vida y opciones ocupadas).
    /// </summary>
    [HttpGet("weeks/{weekId:int}/challenges")]
    public async Task<IActionResult> GetChallengesByWeek(int quinielaId, int weekId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        try
        {
            var challenges = await _challengeApplication.GetChallengesByWeekAsync(quinielaId, weekId, userId, ct);
            return Ok(new Response<List<ChallengeDto>>
            {
                Data = challenges,
                isSuccess = true,
                Message = "Retos de la jornada obtenidos exitosamente."
            });
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException or UnauthorizedAccessException)
        {
            return BadRequest(new Response<List<ChallengeDto>>
            {
                isSuccess = false,
                Message = ex.Message
            });
        }
    }

    /// <summary>
    /// GET /api/quinielas/{quinielaId}/challenges/pending
    /// Obtiene las invitaciones a reto pendientes para el usuario actual.
    /// </summary>
    [HttpGet("challenges/pending")]
    public async Task<IActionResult> GetPendingInvitations(int quinielaId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        try
        {
            var pending = await _challengeApplication.GetPendingInvitationsAsync(quinielaId, userId, ct);
            return Ok(new Response<List<ChallengeDto>>
            {
                Data = pending,
                isSuccess = true,
                Message = "Invitaciones pendientes obtenidas exitosamente."
            });
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException or UnauthorizedAccessException)
        {
            return BadRequest(new Response<List<ChallengeDto>>
            {
                isSuccess = false,
                Message = ex.Message
            });
        }
    }

    /// <summary>
    /// POST /api/quinielas/{quinielaId}/weeks/{weekId}/challenges
    /// Lanza un nuevo reto entre 2 o más jugadores y dispara notificación Web Push.
    /// </summary>
    [HttpPost("weeks/{weekId:int}/challenges")]
    public async Task<IActionResult> CreateChallenge(int quinielaId, int weekId, [FromBody] CreateChallengeDto dto, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        try
        {
            var created = await _challengeApplication.CreateChallengeAsync(quinielaId, weekId, userId, dto, ct);
            return Ok(new Response<ChallengeDto>
            {
                Data = created,
                isSuccess = true,
                Message = "¡Reto lanzado exitosamente!"
            });
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException or UnauthorizedAccessException)
        {
            return BadRequest(new Response<ChallengeDto>
            {
                isSuccess = false,
                Message = ex.Message
            });
        }
    }

    /// <summary>
    /// POST /api/quinielas/{quinielaId}/challenges/{challengeId}/respond
    /// Acepta (con validación de pronóstico no duplicado) o pasa del reto (ACCEPT / DECLINE).
    /// </summary>
    [HttpPost("challenges/{challengeId:int}/respond")]
    public async Task<IActionResult> RespondChallenge(int quinielaId, int challengeId, [FromBody] RespondChallengeDto dto, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        try
        {
            var updated = await _challengeApplication.RespondChallengeAsync(quinielaId, challengeId, userId, dto, ct);
            var isAccept = string.Equals(dto.Action, "ACCEPT", StringComparison.OrdinalIgnoreCase);
            return Ok(new Response<ChallengeDto>
            {
                Data = updated,
                isSuccess = true,
                Message = isAccept ? "¡Has aceptado el reto!" : "Has pasado de este reto."
            });
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException or UnauthorizedAccessException)
        {
            return BadRequest(new Response<ChallengeDto>
            {
                isSuccess = false,
                Message = ex.Message
            });
        }
    }

    /// <summary>
    /// POST /api/quinielas/{quinielaId}/challenges/{challengeId}/participants/{memberId}/settle
    /// Marca la deuda de un participante perdedor como pagada ("🤝 Apuesta Cumplida").
    /// </summary>
    [HttpPost("challenges/{challengeId:int}/participants/{memberId:int}/settle")]
    public async Task<IActionResult> SettleDebt(int quinielaId, int challengeId, int memberId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        try
        {
            var isAdmin = await IsUserAdminAsync(quinielaId);
            var updated = await _challengeApplication.SettleDebtAsync(quinielaId, challengeId, memberId, userId, isAdmin, ct);
            return Ok(new Response<ChallengeDto>
            {
                Data = updated,
                isSuccess = true,
                Message = "Apuesta marcada como cumplida."
            });
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException or UnauthorizedAccessException)
        {
            return BadRequest(new Response<ChallengeDto>
            {
                isSuccess = false,
                Message = ex.Message
            });
        }
    }

    /// <summary>
    /// DELETE /api/quinielas/{quinielaId}/challenges/{challengeId}
    /// Cancela un reto si aún se encuentra en PENDING_ACCEPTANCE.
    /// </summary>
    [HttpDelete("challenges/{challengeId:int}")]
    public async Task<IActionResult> CancelChallenge(int quinielaId, int challengeId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        try
        {
            var cancelled = await _challengeApplication.CancelChallengeAsync(quinielaId, challengeId, userId, ct);
            return Ok(new Response<bool>
            {
                Data = cancelled,
                isSuccess = true,
                Message = "Reto cancelado exitosamente."
            });
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException or UnauthorizedAccessException)
        {
            return BadRequest(new Response<bool>
            {
                isSuccess = false,
                Message = ex.Message
            });
        }
    }
}
