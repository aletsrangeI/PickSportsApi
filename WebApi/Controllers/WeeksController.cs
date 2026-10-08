using System.Security.Claims;
using Common;
using DTO.Espn;
using Interface.Persistence;
using Interface.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UseCases.Broadcasters;

namespace WebApi.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class WeeksController : ControllerBase
{
    private readonly IUnitOfWork     _unitOfWork;
    private readonly IEspnSyncService _espnSync;
    private readonly IBroadcastService _broadcastService;

    public WeeksController(IUnitOfWork unitOfWork, IEspnSyncService espnSync, IBroadcastService broadcastService)
    {
        _unitOfWork       = unitOfWork;
        _espnSync         = espnSync;
        _broadcastService = broadcastService;
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? User.FindFirst(ClaimTypes.Name)?.Value;
        return int.TryParse(claim, out var id) ? id : 0;
    }

    /// <summary>GET /api/weeks/{id}/matches — partidos de una jornada.</summary>
    [HttpGet("{id}/matches")]
    public async Task<IActionResult> GetMatches(int id)
    {
        var matches = await _unitOfWork.Matches.GetByWeekIdAsync(id);

        var dtos = matches.Select(m => new MatchDto
        {
            Id          = m.Id,
            WeekId      = m.WeekId,
            EspnGameId  = m.EspnGameId,
            DateUtc     = DateTime.SpecifyKind(m.DateUtc, DateTimeKind.Utc),
            StatusState = m.StatusState,
            StatusDesc  = m.StatusDesc,
            HomeScore   = m.HomeScore,
            AwayScore   = m.AwayScore,
            WinnerAbbr  = m.WinnerAbbr,
            PostponedToDate = m.PostponedToDate.HasValue ? DateTime.SpecifyKind(m.PostponedToDate.Value, DateTimeKind.Utc) : null,
            Venue       = m.Venue,
            City        = m.City,
            LastSyncUtc = DateTime.SpecifyKind(m.LastSyncUtc, DateTimeKind.Utc),
            Broadcasters = BroadcastChannelCatalog.Deserialize(m.Broadcasters),
            HomeTeam = new TeamDto
            {
                Id           = m.HomeTeam.Id,
                EspnTeamId   = m.HomeTeam.EspnTeamId,
                Name         = m.HomeTeam.Name,
                Abbreviation = m.HomeTeam.Abbreviation,
                DisplayName  = m.HomeTeam.DisplayName,
                LogoUrl      = m.HomeTeam.LogoUrl,
                PrimaryColor = m.HomeTeam.PrimaryColor
            },
            AwayTeam = new TeamDto
            {
                Id           = m.AwayTeam.Id,
                EspnTeamId   = m.AwayTeam.EspnTeamId,
                Name         = m.AwayTeam.Name,
                Abbreviation = m.AwayTeam.Abbreviation,
                DisplayName  = m.AwayTeam.DisplayName,
                LogoUrl      = m.AwayTeam.LogoUrl,
                PrimaryColor = m.AwayTeam.PrimaryColor
            }
        });

        return Ok(new Response<IEnumerable<MatchDto>>
        {
            isSuccess = true,
            Message   = "Partidos obtenidos.",
            Data      = dtos
        });
    }

    /// <summary>POST /api/weeks/{id}/sync-espn — sincroniza una jornada desde ESPN.</summary>
    [HttpPost("{id}/sync-espn")]
    public async Task<IActionResult> SyncFromEspn(int id, CancellationToken ct)
    {
        var result = await _espnSync.SyncWeekAsync(id, ct);

        if (!result.IsSuccess)
            return BadRequest(new Response<SyncResult>
            {
                isSuccess = false,
                Message   = result.ErrorMessage ?? "Error al sincronizar.",
                Data      = result
            });

        return Ok(new Response<SyncResult>
        {
            isSuccess = true,
            Message   = $"Jornada sincronizada. {result.MatchesUpserted} partidos actualizados.",
            Data      = result
        });
    }

    /// <summary>
    /// POST /api/weeks/{id}/sync-broadcasters — reaplica la regla de localía y confirma señales
    /// contra ligamx.net (SPEC-015). Los overrides manuales del admin se preservan.
    /// </summary>
    [HttpPost("{id}/sync-broadcasters")]
    public async Task<IActionResult> SyncBroadcasters(int id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        var response = await _broadcastService.SyncWeekBroadcastersAsync(id, userId, ct);
        if (!response.isSuccess) return BadRequest(response);

        return Ok(response);
    }

    /// <summary>POST /api/weeks/{id}/import-manual-json — importa JSON pegado manualmente.</summary>
    [HttpPost("{id}/import-manual-json")]
    public async Task<IActionResult> ImportManualJson(int id, [FromBody] ImportJsonRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RawJson))
            return BadRequest(new Response<string>
            {
                isSuccess = false,
                Message   = "El JSON no puede estar vacío."
            });

        var result = await _espnSync.ImportManualJsonAsync(id, request.RawJson);

        if (!result.IsSuccess)
            return BadRequest(new Response<SyncResult>
            {
                isSuccess = false,
                Message   = result.ErrorMessage ?? "Error al importar.",
                Data      = result
            });

        return Ok(new Response<SyncResult>
        {
            isSuccess = true,
            Message   = $"JSON importado. {result.MatchesUpserted} partidos cargados.",
            Data      = result
        });
    }
}

public record ImportJsonRequest(string RawJson);
