using System.Security.Claims;
using Common;
using DTO.Espn;
using DTO.Notifications;
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
    private readonly IUnitOfWork              _unitOfWork;
    private readonly IEspnSyncService         _espnSync;
    private readonly IBroadcastService        _broadcastService;
    private readonly IWebPushNotificationService _webPush;

    public WeeksController(
        IUnitOfWork unitOfWork,
        IEspnSyncService espnSync,
        IBroadcastService broadcastService,
        IWebPushNotificationService webPush)
    {
        _unitOfWork       = unitOfWork;
        _espnSync         = espnSync;
        _broadcastService = broadcastService;
        _webPush          = webPush;
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

    /// <summary>POST /api/weeks/{id}/publish — publica manualmente una jornada en borrador.</summary>
    [HttpPost("{id}/publish")]
    public async Task<IActionResult> PublishWeek(int id, CancellationToken ct)
    {
        var week = await _unitOfWork.Weeks.GetAsync(id);
        if (week == null)
            return NotFound(new Response<string> { isSuccess = false, Message = "Jornada no encontrada." });

        if (week.Status == "PUBLISHED")
            return Ok(new Response<string> { isSuccess = true, Message = $"La jornada {week.WeekNumber} ya está publicada." });

        week.Status = "PUBLISHED";
        week.PublishedAt = DateTime.UtcNow;

        var matches = (await _unitOfWork.Matches.GetByWeekIdAsync(week.Id)).ToList();
        var firstMatchDate = matches.OrderBy(m => m.DateUtc).FirstOrDefault()?.DateUtc;
        if (firstMatchDate.HasValue)
        {
            week.FirstGameUtc = firstMatchDate.Value;
        }

        await _unitOfWork.Weeks.UpdateAsync(week);
        await _unitOfWork.Save(ct);

        // Intentar sincronizar partidos con ESPN
        try
        {
            await _espnSync.SyncWeekAsync(week.Id, ct);
            var syncedMatches = (await _unitOfWork.Matches.GetByWeekIdAsync(week.Id)).ToList();
            var syncedFirstMatch = syncedMatches.OrderBy(m => m.DateUtc).FirstOrDefault()?.DateUtc;
            if (syncedFirstMatch.HasValue && week.FirstGameUtc != syncedFirstMatch.Value)
            {
                week.FirstGameUtc = syncedFirstMatch.Value;
                await _unitOfWork.Weeks.UpdateAsync(week);
                await _unitOfWork.Save(ct);
            }
        }
        catch { }

        // Notificar a quinielas activas
        var season = await _unitOfWork.Seasons.GetAsync(week.SeasonId);
        if (season != null)
        {
            var quinielas = (await _unitOfWork.Quinielas.GetByLeagueIdAsync(season.LeagueId)).Where(q => q.IsActive).ToList();
            foreach (var quiniela in quinielas)
            {
                var payload = new PushNotificationPayload(
                    Title: $"🟢 ¡Jornada {week.WeekNumber} disponible!",
                    Message: $"La jornada {week.WeekNumber} ya está abierta para ingresar pronósticos.",
                    Url: $"/fixtures?weekId={week.Id}",
                    Data: new { type = "week_published", weekId = week.Id, weekNumber = week.WeekNumber }
                );
                await _webPush.SendNotificationToQuinielaAsync(quiniela.Id, payload, ct);
            }
        }

        return Ok(new Response<string>
        {
            isSuccess = true,
            Message = $"Jornada {week.WeekNumber} publicada exitosamente."
        });
    }
}

public record ImportJsonRequest(string RawJson);
