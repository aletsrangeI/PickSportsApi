using System.Security.Claims;
using Common;
using DTO.Liguilla;
using Interface.Persistence;
using Interface.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[Authorize]
[ApiController]
[Route("api/quinielas/{quinielaId:int}/liguilla")]
public class LiguillaController : ControllerBase
{
    private readonly ILiguillaApplication _liguillaApplication;
    private readonly IUnitOfWork _unitOfWork;

    public LiguillaController(ILiguillaApplication liguillaApplication, IUnitOfWork unitOfWork)
    {
        _liguillaApplication = liguillaApplication;
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
    /// GET /api/quinielas/{quinielaId}/liguilla/overview
    /// Devuelve el panorama oficial de la Liguilla: estatus de apertura, clasificados, nodos y bracket del usuario.
    /// Si el torneo regular aún no termina y el usuario no es admin, responde IsAvailable = false y estatus HIDDEN.
    /// </summary>
    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview(int quinielaId)
    {
        var userId = GetCurrentUserId();
        var isAdmin = await IsUserAdminAsync(quinielaId);

        var overview = await _liguillaApplication.GetOverviewAsync(quinielaId, userId, isAdmin);
        return Ok(new Response<LiguillaOverviewDto>
        {
            Data = overview,
            isSuccess = true,
            Message = "Overview de Liguilla obtenido exitosamente."
        });
    }

    /// <summary>
    /// GET /api/quinielas/{quinielaId}/liguilla/my-bracket
    /// Obtiene el bracket del usuario autenticado con puntos y desglose.
    /// </summary>
    [HttpGet("my-bracket")]
    public async Task<IActionResult> GetMyBracket(int quinielaId)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        var bracket = await _liguillaApplication.GetMyBracketAsync(quinielaId, userId);
        return Ok(new Response<LiguillaBracketDetailDto?>
        {
            Data = bracket,
            isSuccess = true,
            Message = bracket != null ? "Bracket obtenido." : "No se ha registrado pronóstico de Liguilla."
        });
    }

    /// <summary>
    /// POST /api/quinielas/{quinielaId}/liguilla/submit
    /// Registra o actualiza el pronóstico de Liguilla (Lock-In).
    /// </summary>
    [HttpPost("submit")]
    public async Task<IActionResult> SubmitBracket(int quinielaId, [FromBody] SubmitLiguillaBracketDto dto)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        try
        {
            var result = await _liguillaApplication.SubmitBracketAsync(quinielaId, userId, dto);
            return Ok(new Response<LiguillaBracketDetailDto>
            {
                Data = result,
                isSuccess = true,
                Message = "¡Pronóstico de Liguilla registrado exitosamente!"
            });
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(new Response<LiguillaBracketDetailDto>
            {
                isSuccess = false,
                Message = ex.Message
            });
        }
    }

    /// <summary>
    /// GET /api/quinielas/{quinielaId}/liguilla/standings
    /// Obtiene la tabla de posiciones independiente de Pick'em Liguilla con los criterios de desempate en cascada.
    /// </summary>
    [HttpGet("standings")]
    public async Task<IActionResult> GetStandings(int quinielaId)
    {
        var standings = await _liguillaApplication.GetStandingsAsync(quinielaId);
        return Ok(new Response<LiguillaStandingsDto>
        {
            Data = standings,
            isSuccess = true,
            Message = "Posiciones de Pick'em Liguilla obtenidas exitosamente."
        });
    }

    /// <summary>
    /// POST /api/quinielas/{quinielaId}/liguilla/admin/resolve-node
    /// Dictamina al ganador oficial de una serie tras concluir el partido de vuelta.
    /// Solo permitido para administradores.
    /// </summary>
    [HttpPost("admin/resolve-node")]
    public async Task<IActionResult> ResolveNode(int quinielaId, [FromBody] ResolveLiguillaNodeRequestDto dto)
    {
        if (!await IsUserAdminAsync(quinielaId))
            return Forbid();

        try
        {
            var success = await _liguillaApplication.ResolveNodeAsync(quinielaId, dto);
            return Ok(new Response<bool>
            {
                Data = success,
                isSuccess = true,
                Message = "Nodo de Liguilla resuelto y puntuaciones actualizadas."
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new Response<bool>
            {
                isSuccess = false,
                Message = ex.Message
            });
        }
    }

    /// <summary>
    /// POST /api/quinielas/{quinielaId}/liguilla/admin/seed-simulation
    /// Sembrado de prueba con los 8 clasificados actuales de la Liga MX para pruebas locales y QA.
    /// Solo permitido para administradores.
    /// </summary>
    [HttpPost("admin/seed-simulation")]
    public async Task<IActionResult> SeedSimulation(int quinielaId)
    {
        if (!await IsUserAdminAsync(quinielaId))
            return Forbid();

        try
        {
            var success = await _liguillaApplication.SeedSimulationNodesAsync(quinielaId);
            return Ok(new Response<bool>
            {
                Data = success,
                isSuccess = true,
                Message = "Simulación de Liguilla sembrada con los primeros 8 lugares de la tabla general."
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new Response<bool>
            {
                isSuccess = false,
                Message = ex.Message
            });
        }
    }
}
