using Common;
using Domain.Entities;
using Interface.Persistence;
using Interface.UseCases;

namespace UseCases.Seasons;

public class SeasonProvisioner : ISeasonProvisioner
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEspnSyncService _espnSync;
    private readonly IAppLogger<SeasonProvisioner> _logger;

    public SeasonProvisioner(IUnitOfWork unitOfWork, IEspnSyncService espnSync, IAppLogger<SeasonProvisioner> logger)
    {
        _unitOfWork = unitOfWork;
        _espnSync = espnSync;
        _logger = logger;
    }

    public async Task<SeasonProvisionResult> EnsureReadyAsync(League league, CancellationToken ct = default)
    {
        var season = await _unitOfWork.Seasons.GetCurrentSeasonAsync(league.Id);
        var created = false;

        if (season == null)
        {
            season = BuildSeason(league, DateTime.UtcNow);
            if (!await _unitOfWork.Seasons.InsertAsync(season))
            {
                return new SeasonProvisionResult(null, false, false,
                    "No se pudo crear la temporada de la liga. Inténtalo de nuevo o crea la temporada manualmente.");
            }
            created = true;
            _logger.LogInformation("[H-011] Temporada '{0}' creada para la liga {1}.", season.Name, league.Code);
        }

        var weeks = await _unitOfWork.Weeks.GetBySeasonIdAsync(season.Id);
        if (weeks.Any())
        {
            return new SeasonProvisionResult(season, created, true, null);
        }

        try
        {
            var sync = await _espnSync.SyncFullSeasonAsync(season.Id, ct);
            if (!sync.IsSuccess)
            {
                var reason = sync.ErrorMessage ?? "ESPN no devolvió datos";
                _logger.LogWarning("[H-011] Sync inicial falló para '{0}': {1}", season.Name, reason);
                return new SeasonProvisionResult(season, created, false,
                    $"La quiniela se creó, pero no se pudo cargar el calendario desde ESPN ({reason}). Reintenta con \"Sync ESPN\".");
            }

            var syncedWeeks = (await _unitOfWork.Weeks.GetBySeasonIdAsync(season.Id)).ToList();
            var hasWeeks = sync.WeeksSynced > 0 || syncedWeeks.Any();

            // WeeksCount nace en 17 y manda en el selector de jornadas y en qué jornadas son "fase final".
            // Se ajusta al calendario regular real que publicó ESPN (Serie A: 38, Champions: 8 de fase de liga,
            // NFL: 18), tanto hacia arriba como hacia abajo. L3 (sin datos) no cuenta: es solo configuración.
            // Liga MX conserva su valor: su Liguilla va justo después de las jornadas regulares.
            var maxWeek = syncedWeeks.Count > 0 ? syncedWeeks.Max(w => w.WeekNumber) : 0;
            if (league.Code != "mex.1" && sync.FallbackLevel is "L1_CALENDAR" or "L2_SCOREBOARD"
                && maxWeek > 0 && maxWeek != league.WeeksCount)
            {
                league.WeeksCount = maxWeek;
                await _unitOfWork.Leagues.UpdateAsync(league);
            }
            // L3 crea jornadas "vacías" a partir de la configuración: sirven, pero sin partidos.
            string? warning = sync.FallbackLevel == "L3_CONFIG"
                ? "La quiniela se creó, pero ESPN aún no publica partidos para esta liga. Las jornadas se llenarán al sincronizar."
                : null;
            return new SeasonProvisionResult(season, created, hasWeeks, warning);
        }
        catch (Exception ex)
        {
            _logger.LogError("[H-011] Excepción en sync inicial de '{0}': {1}", season.Name, ex.Message);
            return new SeasonProvisionResult(season, created, false,
                "La quiniela se creó, pero ESPN no respondió al cargar el calendario. Reintenta con \"Sync ESPN\".");
        }
    }

    /// <summary>
    /// El deporte se deduce de EspnApiPath ("sports/football/nfl"), igual que el sync. League.SportKey no es
    /// confiable: la semilla no lo fija y la NFL de la base real tiene el valor por defecto "soccer".
    /// </summary>
    private static bool IsAmericanFootball(League league)
    {
        var parts = (league.EspnApiPath ?? "").Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2 && parts[^2] != "sports")
            return string.Equals(parts[^2], "football", StringComparison.OrdinalIgnoreCase);
        return string.Equals(league.SportKey, "football", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Nombre/tipo de la temporada nueva. Liga MX mantiene su formato Apertura/Clausura;
    /// el resto usa temporada anual (fútbol americano: temporada regular, tipo ESPN 2).
    /// </summary>
    public static Season BuildSeason(League league, DateTime nowUtc)
    {
        var year = nowUtc.Year;
        string name;
        int seasonType;

        if (league.Code == "mex.1")
        {
            var apertura = nowUtc.Month >= 7;
            name = $"{(apertura ? "Apertura" : "Clausura")} {year}";
            seasonType = apertura ? 2 : 1;
        }
        else if (IsAmericanFootball(league))
        {
            name = $"Temporada Regular {year}";
            seasonType = 2;
        }
        else
        {
            name = $"Temporada {year}";
            seasonType = 1;
        }

        return new Season
        {
            LeagueId = league.Id,
            Year = year,
            SeasonType = seasonType,
            Name = name,
            IsCurrent = true,
            Active = true,
            Created = nowUtc
        };
    }
}
