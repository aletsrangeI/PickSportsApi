using Common;
using Domain.Entities;
using DTO.Broadcast;
using Interface.Persistence;
using Interface.UseCases;

namespace UseCases.Broadcasters;

/// <summary>
/// SPEC-015 · Orquestador de "Dónde Ver".
/// Prioridad de fuentes: MANUAL (admin) &gt; LIGAMX (sitio oficial) &gt; RULE (regla de localía).
/// Una fuente de menor prioridad nunca sobrescribe a una de mayor prioridad.
/// </summary>
public class BroadcastService : IBroadcastService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly BroadcastRuleEngine _ruleEngine;
    private readonly ILigaMxBroadcastScraper _scraper;
    private readonly IAppLogger<BroadcastService> _logger;

    public BroadcastService(
        IUnitOfWork unitOfWork,
        BroadcastRuleEngine ruleEngine,
        ILigaMxBroadcastScraper scraper,
        IAppLogger<BroadcastService> logger)
    {
        _unitOfWork = unitOfWork;
        _ruleEngine = ruleEngine;
        _scraper    = scraper;
        _logger     = logger;
    }

    public async Task<int> ApplyDefaultBroadcastersAsync(int weekId, CancellationToken ct = default)
    {
        var (_, league) = await ResolveWeekAndLeagueAsync(weekId);
        if (!BroadcastChannelCatalog.IsLigaMx(league)) return 0;

        var matches = (await _unitOfWork.Matches.GetByWeekIdAsync(weekId)).ToList();
        return await ApplyDefaultsAsync(matches);
    }

    public async Task<BroadcastSyncResultDto> SyncFromOfficialSourceAsync(int weekId, CancellationToken ct = default)
    {
        var result = new BroadcastSyncResultDto { WeekId = weekId };

        var (week, league) = await ResolveWeekAndLeagueAsync(weekId);
        if (week == null || !BroadcastChannelCatalog.IsLigaMx(league)) return result;

        var matches = (await _unitOfWork.Matches.GetByWeekIdAsync(weekId)).ToList();
        result.DefaultsApplied = await ApplyDefaultsAsync(matches);
        result.ManualPreserved = matches.Count(m => m.BroadcastersSource == BroadcastChannelCatalog.SourceManual);

        var scraped = await _scraper.FetchCurrentWeekAsync(ct);
        var forThisWeek = scraped
            .Where(s => s.WeekNumber == null || s.WeekNumber == week.WeekNumber)
            .ToList();
        result.OfficialSourceAvailable = forThisWeek.Count > 0;

        foreach (var match in matches)
        {
            if (match.BroadcastersSource == BroadcastChannelCatalog.SourceManual) continue;

            var home = BroadcastRuleEngine.NormalizeAbbreviation(match.HomeTeam?.Abbreviation);
            var away = BroadcastRuleEngine.NormalizeAbbreviation(match.AwayTeam?.Abbreviation);

            var official = forThisWeek.FirstOrDefault(s =>
                BroadcastRuleEngine.NormalizeAbbreviation(s.HomeAbbr) == home &&
                BroadcastRuleEngine.NormalizeAbbreviation(s.AwayAbbr) == away);
            if (official == null) continue;

            var channels = BroadcastChannelCatalog.Sanitize(official.Channels);
            if (channels.Count == 0) continue;

            var serialized = BroadcastChannelCatalog.Serialize(channels);
            if (serialized == match.Broadcasters && match.BroadcastersSource == BroadcastChannelCatalog.SourceLigaMx)
                continue;

            match.Broadcasters       = serialized;
            match.BroadcastersSource = BroadcastChannelCatalog.SourceLigaMx;
            await _unitOfWork.Matches.UpdateAsync(match);
            result.OfficialUpdated++;
        }

        if (!result.OfficialSourceAvailable)
            _logger.LogWarning("[Broadcast] Jornada {WeekId}: sin señales oficiales; se conservan los canales por localía.", weekId);

        return result;
    }

    public async Task<Response<BroadcastSyncResultDto>> SyncWeekBroadcastersAsync(int weekId, int userId, CancellationToken ct = default)
    {
        var response = new Response<BroadcastSyncResultDto>();

        if (!await CanManageBroadcastsAsync(userId))
        {
            response.isSuccess = false;
            response.Message   = "No tienes permisos de administrador para sincronizar canales.";
            return response;
        }

        var (week, league) = await ResolveWeekAndLeagueAsync(weekId);
        if (week == null)
        {
            response.isSuccess = false;
            response.Message   = "La jornada no existe.";
            return response;
        }

        if (!BroadcastChannelCatalog.IsLigaMx(league))
        {
            response.isSuccess = false;
            response.Message   = "\"Dónde Ver\" solo está disponible para la Liga MX.";
            return response;
        }

        var result = await SyncFromOfficialSourceAsync(weekId, ct);

        response.isSuccess = true;
        response.Data      = result;
        response.Message   = result.OfficialSourceAvailable
            ? $"Canales actualizados: {result.OfficialUpdated} confirmados por Liga MX, {result.DefaultsApplied} por localía."
            : $"Liga MX no publicó señales para esta jornada; se aplicó la regla de localía ({result.DefaultsApplied} partidos).";
        return response;
    }

    public async Task<Response<MatchBroadcastersDto>> UpdateMatchBroadcastersAsync(int matchId, List<string> channels, int userId)
    {
        var response = new Response<MatchBroadcastersDto>();

        if (!await CanManageBroadcastsAsync(userId))
        {
            response.isSuccess = false;
            response.Message   = "No tienes permisos de administrador para editar canales.";
            return response;
        }

        var match = await _unitOfWork.Matches.GetByIdWithTeamsAsync(matchId);
        if (match == null)
        {
            response.isSuccess = false;
            response.Message   = "El partido no existe.";
            return response;
        }

        var sanitized = BroadcastChannelCatalog.Sanitize(channels);

        if (sanitized.Count == 0)
        {
            // Lista vacía: el admin quita su override y se restablece la regla de localía
            sanitized                = _ruleEngine.Resolve(match.HomeTeam?.Abbreviation).ToList();
            match.BroadcastersSource = BroadcastChannelCatalog.SourceRule;
        }
        else
        {
            match.BroadcastersSource = BroadcastChannelCatalog.SourceManual;
        }

        match.Broadcasters = BroadcastChannelCatalog.Serialize(sanitized);
        await _unitOfWork.Matches.UpdateAsync(match);

        response.isSuccess = true;
        response.Message   = match.BroadcastersSource == BroadcastChannelCatalog.SourceManual
            ? "Canales del partido actualizados."
            : "Se restablecieron los canales por localía.";
        response.Data = new MatchBroadcastersDto
        {
            MatchId      = match.Id,
            Broadcasters = sanitized,
            Source       = match.BroadcastersSource
        };
        return response;
    }

    // ───────────────────────────────────────────────────────────────────────

    /// <summary>Asigna la regla de localía solo a partidos sin canales o cuyo origen ya es la regla.</summary>
    private async Task<int> ApplyDefaultsAsync(List<Match> matches)
    {
        var applied = 0;

        foreach (var match in matches)
        {
            if (match.Broadcasters != null && match.BroadcastersSource != BroadcastChannelCatalog.SourceRule)
                continue;

            var serialized = BroadcastChannelCatalog.Serialize(_ruleEngine.Resolve(match.HomeTeam?.Abbreviation));
            if (serialized == match.Broadcasters) continue;

            match.Broadcasters       = serialized;
            match.BroadcastersSource = BroadcastChannelCatalog.SourceRule;
            await _unitOfWork.Matches.UpdateAsync(match);
            applied++;
        }

        return applied;
    }

    private async Task<(Week? week, League? league)> ResolveWeekAndLeagueAsync(int weekId)
    {
        var week = await _unitOfWork.Weeks.GetAsync(weekId);
        if (week == null) return (null, null);

        var season = await _unitOfWork.Seasons.GetAsync(week.SeasonId);
        if (season == null) return (week, null);

        var league = await _unitOfWork.Leagues.GetAsync(season.LeagueId);
        return (week, league);
    }

    /// <summary>ADMIN global, o OWNER/ADMIN de alguna quiniela (mismo criterio que la UI de Calendario).</summary>
    private async Task<bool> CanManageBroadcastsAsync(int userId)
    {
        var user = await _unitOfWork.Users.GetAsync(userId);
        if (string.Equals(user?.Role, "ADMIN", StringComparison.OrdinalIgnoreCase)) return true;

        var memberships = await _unitOfWork.QuinielaMembers.GetByUserIdAsync(userId);
        return memberships.Any(m =>
            string.Equals(m.Role, "OWNER", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(m.Role, "ADMIN", StringComparison.OrdinalIgnoreCase));
    }
}
