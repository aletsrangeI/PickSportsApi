using Common;
using DTO.Espn;
using Interface.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[Authorize]
[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;

    public AdminController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <summary>GET /api/admin/espn-health — semáforo de estado ESPN con último log.</summary>
    [HttpGet("espn-health")]
    public async Task<IActionResult> GetEspnHealth()
    {
        var latest = await _unitOfWork.EspnHealthLogs.GetLatestHealthStatusAsync();

        if (latest == null)
        {
            return Ok(new Response<EspnHealthDto>
            {
                isSuccess = true,
                Message   = "Sin datos de salud todavía.",
                Data      = new EspnHealthDto
                {
                    Status        = "YELLOW",
                    IsSuccess     = false,
                    LatencyMs     = 0,
                    EndpointTested = "N/A",
                    CheckedAt     = DateTime.UtcNow,
                    ErrorMessage  = "No hay registros de sincronización aún."
                }
            });
        }

        // Semáforo: verde si éxito y latencia < 2s, amarillo si éxito pero lento, rojo si falló
        var status = latest.IsSuccess
            ? (latest.LatencyMs < 2000 ? "GREEN" : "YELLOW")
            : "RED";

        return Ok(new Response<EspnHealthDto>
        {
            isSuccess = true,
            Message   = "Estado ESPN.",
            Data      = new EspnHealthDto
            {
                Status         = status,
                IsSuccess      = latest.IsSuccess,
                LatencyMs      = latest.LatencyMs,
                EndpointTested = latest.EndpointTested,
                CheckedAt      = latest.CheckedAt,
                ErrorMessage   = latest.ErrorMessage
            }
        });
    }
}
