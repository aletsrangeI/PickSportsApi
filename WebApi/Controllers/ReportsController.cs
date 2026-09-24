using Interface.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[Authorize]
[ApiController]
[Route("api/quinielas")]
public class ReportsController : ControllerBase
{
    private readonly IWhatsAppReportService _reportService;

    public ReportsController(IWhatsAppReportService reportService)
    {
        _reportService = reportService;
    }

    /// <summary>
    /// GET /api/quinielas/{id}/reports/whatsapp/reminder?weekId={weekId}
    /// Alerta de cierre con cuenta regresiva y faltantes para WhatsApp.
    /// </summary>
    [HttpGet("{id}/reports/whatsapp/reminder")]
    public async Task<IActionResult> GetReminderReport(int id, [FromQuery] int weekId)
    {
        if (weekId <= 0)
        {
            return BadRequest(new { isSuccess = false, message = "El parámetro weekId es requerido." });
        }

        var appBaseUrl = $"{Request.Scheme}://{Request.Host}";
        var response = await _reportService.GenerateReminderReportAsync(id, weekId, appBaseUrl);
        if (!response.isSuccess)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    /// <summary>
    /// GET /api/quinielas/{id}/reports/whatsapp/summary?weekId={weekId}
    /// El Periódico del Lunes: resultados oficiales, podio, tabla y galardones.
    /// </summary>
    [HttpGet("{id}/reports/whatsapp/summary")]
    public async Task<IActionResult> GetSummaryReport(int id, [FromQuery] int weekId)
    {
        if (weekId <= 0)
        {
            return BadRequest(new { isSuccess = false, message = "El parámetro weekId es requerido." });
        }

        var response = await _reportService.GenerateSummaryReportAsync(id, weekId);
        if (!response.isSuccess)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    /// <summary>
    /// GET /api/quinielas/{id}/reports/whatsapp/prizepool
    /// Estado de la bolsa de premios (70% / 20% / 10%).
    /// </summary>
    [HttpGet("{id}/reports/whatsapp/prizepool")]
    public async Task<IActionResult> GetPrizePoolReport(int id)
    {
        var response = await _reportService.GeneratePrizePoolReportAsync(id);
        if (!response.isSuccess)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    /// <summary>
    /// GET /api/quinielas/{id}/reports/whatsapp/player?weekId={weekId}&memberId={memberId}
    /// Reporte individual detallado por participante.
    /// </summary>
    [HttpGet("{id}/reports/whatsapp/player")]
    public async Task<IActionResult> GetPlayerReport(int id, [FromQuery] int weekId, [FromQuery] int memberId)
    {
        if (weekId <= 0 || memberId <= 0)
        {
            return BadRequest(new { isSuccess = false, message = "Los parámetros weekId y memberId son requeridos." });
        }

        var response = await _reportService.GeneratePlayerReportAsync(id, weekId, memberId);
        if (!response.isSuccess)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }
}
