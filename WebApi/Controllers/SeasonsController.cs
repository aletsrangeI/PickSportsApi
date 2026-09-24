using Common;
using DTO.Espn;
using Interface.Persistence;
using Interface.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class SeasonsController : ControllerBase
{
    private readonly IUnitOfWork      _unitOfWork;
    private readonly IEspnSyncService _espnSync;

    public SeasonsController(IUnitOfWork unitOfWork, IEspnSyncService espnSync)
    {
        _unitOfWork = unitOfWork;
        _espnSync   = espnSync;
    }

    /// <summary>GET /api/seasons — lista de temporadas activas con detección inteligente del torneo en curso y colchón de torneos terminados.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? leagueId = null)
    {
        var seasons = await _unitOfWork.Seasons.GetAllAsync();
        var leagues = (await _unitOfWork.Leagues.GetAllAsync()).ToDictionary(l => l.Id);

        var now = DateTime.UtcNow;
        var currentYear = now.Year;
        var isSecondHalfOfYear = now.Month >= 7; // Julio a Diciembre = Apertura

        var filtered = seasons.Where(s => s.Active);
        if (leagueId.HasValue)
        {
            filtered = filtered.Where(s => s.LeagueId == leagueId.Value);
        }

        var seasonsList = filtered.ToList();
        var dtos = seasonsList
            .Select(s =>
            {
                leagues.TryGetValue(s.LeagueId, out var league);
                var isSplitTournament = league != null && (league.Code == "mex.1" || league.Country == "México");

                bool isCurrent;
                bool isFinished;

                if (s.IsCurrent)
                {
                    isCurrent = true;
                    isFinished = false;
                }
                else
                {
                    // Si alguna otra temporada de la liga tiene IsCurrent explícito en DB, esta no es actual
                    bool hasExplicitCurrent = seasonsList.Any(x => x.LeagueId == s.LeagueId && x.IsCurrent);
                    if (hasExplicitCurrent)
                    {
                        isCurrent = false;
                        isFinished = s.Year < currentYear || (s.EndDate.HasValue && s.EndDate.Value < now);
                    }
                    else
                    {
                        // Fallback heurístico por calendario sólo si ninguna temporada tiene IsCurrent en DB
                        if (isSplitTournament)
                        {
                            isCurrent = s.Year == currentYear &&
                                (isSecondHalfOfYear
                                    ? s.Name.Contains("Apertura", StringComparison.OrdinalIgnoreCase)
                                    : s.Name.Contains("Clausura", StringComparison.OrdinalIgnoreCase));
                            isFinished = !isCurrent && isSecondHalfOfYear && s.Name.Contains("Clausura", StringComparison.OrdinalIgnoreCase);
                        }
                        else
                        {
                            isFinished = s.Year < currentYear || (s.EndDate.HasValue && s.EndDate.Value < now);
                            isCurrent = !isFinished;
                        }
                    }
                }

                return new SeasonSummaryDto
                {
                    Id         = s.Id,
                    LeagueId   = s.LeagueId,
                    Name       = s.Name,
                    Year       = s.Year,
                    IsCurrent  = isCurrent,
                    IsFinished = isFinished,
                    Status     = isFinished ? "FINISHED" : (s.StartDate.HasValue && s.StartDate.Value > now ? "UPCOMING" : "ACTIVE"),
                    StartDate  = s.StartDate,
                    EndDate    = s.EndDate,
                    WeeksCount = 0
                };
            })
            .OrderByDescending(s => s.Year)
            .ThenByDescending(s => s.IsCurrent)
            .ThenBy(s => s.IsFinished);

        return Ok(new Response<IEnumerable<SeasonSummaryDto>>
        {
            isSuccess = true,
            Message   = "Temporadas obtenidas.",
            Data      = dtos
        });
    }

    /// <summary>GET /api/seasons/{id}/weeks — jornadas de una temporada con conteo de partidos.</summary>
    [HttpGet("{id}/weeks")]
    public async Task<IActionResult> GetWeeks(int id)
    {
        var season = await _unitOfWork.Seasons.GetAsync(id);
        var league = season != null ? await _unitOfWork.Leagues.GetAsync(season.LeagueId) : null;
        var maxWeeks = league?.WeeksCount ?? 17;

        var weeks = (await _unitOfWork.Weeks.GetBySeasonIdAsync(id))
            .Where(w => w.Active && w.WeekNumber <= maxWeeks)
            .OrderBy(w => w.WeekNumber)
            .ToList();

        var dtos = new List<WeekSummaryDto>();
        foreach (var w in weeks)
        {
            var weekMatches = (await _unitOfWork.Matches.GetByWeekIdAsync(w.Id)).ToList();
            dtos.Add(new WeekSummaryDto
            {
                Id             = w.Id,
                WeekNumber     = w.WeekNumber,
                Name           = w.Name,
                StartDate      = w.StartDate,
                EndDate        = w.EndDate,
                Status         = w.Status,
                MatchesCount   = weekMatches.Count,
                PostponedCount = weekMatches.Count(m => m.StatusState == "postponed")
            });
        }

        return Ok(new Response<IEnumerable<WeekSummaryDto>>
        {
            isSuccess = true,
            Message   = "Jornadas obtenidas.",
            Data      = dtos
        });
    }

    /// <summary>
    /// POST /api/seasons/{id}/sync-full — sincroniza TODA la temporada con fallback L1→L2→L3.
    /// Retorna SyncResult con FallbackLevel para que la UI informe al admin qué nivel se usó.
    /// </summary>
    [HttpPost("{id}/sync-full")]
    public async Task<IActionResult> SyncFull(int id, CancellationToken ct)
    {
        var result = await _espnSync.SyncFullSeasonAsync(id, ct);

        if (!result.IsSuccess && result.FallbackLevel == "ERROR")
            return BadRequest(new Response<SyncResult>
            {
                isSuccess = false,
                Message   = result.ErrorMessage ?? "Error al sincronizar.",
                Data      = result
            });

        var message = result.FallbackLevel switch
        {
            "L1_CALENDAR"  => $"Temporada sincronizada desde calendario ESPN. {result.WeeksSynced} jornadas, {result.MatchesUpserted} partidos.",
            "L2_SCOREBOARD"=> $"Temporada sincronizada via scoreboard (fallback L2). {result.WeeksSynced} jornadas, {result.MatchesUpserted} partidos.",
            "L3_CONFIG"    => $"Jornadas generadas por configuración (fallback L3). {result.WeeksSynced} semanas creadas sin partidos — sincroniza cada jornada manualmente.",
            _              => $"Temporada sincronizada. Nivel: {result.FallbackLevel}."
        };

        return Ok(new Response<SyncResult>
        {
            isSuccess = true,
            Message   = message,
            Data      = result
        });
    }

    /// <summary>POST /api/seasons — crea una nueva temporada.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSeasonRequest request)
    {
        var season = new Domain.Entities.Season
        {
            LeagueId   = request.LeagueId,
            Year       = request.Year,
            SeasonType = request.SeasonType,
            Name       = request.Name,
            IsCurrent  = request.IsCurrent,
            Active     = true,
            Created    = DateTime.UtcNow
        };
        await _unitOfWork.Seasons.InsertAsync(season);
        await _unitOfWork.Save();

        return Ok(new Response<SeasonSummaryDto>
        {
            isSuccess = true,
            Message   = "Temporada creada con éxito.",
            Data      = new SeasonSummaryDto
            {
                Id         = season.Id,
                LeagueId   = season.LeagueId,
                Name       = season.Name,
                Year       = season.Year,
                IsCurrent  = season.IsCurrent,
                WeeksCount = 0
            }
        });
    }

    /// <summary>POST /api/seasons/ensure-apertura — garantiza que Apertura 2026 exista y esté activa.</summary>
    [HttpPost("ensure-apertura")]
    public async Task<IActionResult> EnsureApertura()
    {
        var leagues = await _unitOfWork.Leagues.GetAllAsync();
        var ligaMx = leagues.FirstOrDefault(l => l.Code == "mex.1");
        if (ligaMx == null)
            return BadRequest(new Response<string> { isSuccess = false, Message = "Liga MX no encontrada." });

        var allSeasons = (await _unitOfWork.Seasons.GetAllAsync())
            .Where(s => s.LeagueId == ligaMx.Id && s.Year == 2026)
            .ToList();

        var apertura = allSeasons.FirstOrDefault(s => s.Name.Contains("Apertura"));
        if (apertura == null)
        {
            apertura = new Domain.Entities.Season
            {
                LeagueId   = ligaMx.Id,
                Year       = 2026,
                SeasonType = 2,
                Name       = "Apertura 2026",
                IsCurrent  = true,
                Active     = true,
                Created    = DateTime.UtcNow
            };
            await _unitOfWork.Seasons.InsertAsync(apertura);
        }
        else
        {
            apertura.IsCurrent = true;
            await _unitOfWork.Seasons.UpdateAsync(apertura);
        }

        foreach (var s in allSeasons.Where(s => s.Id != apertura.Id))
        {
            s.IsCurrent = false;
            await _unitOfWork.Seasons.UpdateAsync(s);
        }

        await _unitOfWork.Save();

        return Ok(new Response<SeasonSummaryDto>
        {
            isSuccess = true,
            Message   = "Temporada Apertura 2026 lista como activa.",
            Data      = new SeasonSummaryDto
            {
                Id         = apertura.Id,
                LeagueId   = apertura.LeagueId,
                Name       = apertura.Name,
                Year       = apertura.Year,
                IsCurrent  = true,
                WeeksCount = 0
            }
        });
    }
}

public record CreateSeasonRequest(int LeagueId, int Year, int SeasonType, string Name, bool IsCurrent);
