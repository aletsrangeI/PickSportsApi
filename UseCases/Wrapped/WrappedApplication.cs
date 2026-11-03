using System.Text.Json;
using Common;
using Domain.Entities;
using DTO.Wrapped;
using Interface.Persistence;
using Interface.UseCases;
using UseCases.Bulletin;

namespace UseCases.Wrapped;

/// <summary>SPEC-014: orquesta la carga de datos, el motor puro y el cache (snapshot) del Wrapped.</summary>
public class WrappedApplication : IWrappedApplication
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IUnitOfWork _unitOfWork;
    private readonly SeasonWrappedEngine _engine;
    private readonly LeagueDerbyDetector _derbyDetector;

    public WrappedApplication(IUnitOfWork unitOfWork, SeasonWrappedEngine engine, LeagueDerbyDetector derbyDetector)
    {
        _unitOfWork = unitOfWork;
        _engine = engine;
        _derbyDetector = derbyDetector;
    }

    public async Task<Response<SeasonWrappedDto>> GetWrappedAsync(int quinielaId, int seasonId, int userId)
    {
        var response = new Response<SeasonWrappedDto>();

        var quiniela = await _unitOfWork.Quinielas.GetAsync(quinielaId);
        if (quiniela == null) return Fail(response, "Quiniela no encontrada.");

        var season = await _unitOfWork.Seasons.GetAsync(seasonId);
        if (season == null || season.LeagueId != quiniela.LeagueId) return Fail(response, "Temporada no encontrada.");

        var membership = await _unitOfWork.QuinielaMembers.GetMembershipAsync(quinielaId, userId);
        if (membership == null) return Fail(response, "No perteneces a esta quiniela.");

        if (!season.IsFinished) return Fail(response, "La temporada aun no ha finalizado.");

        var existing = await _unitOfWork.SeasonWrappedSnapshots.GetByKeyAsync(quinielaId, seasonId, userId);
        if (existing != null)
        {
            var cached = JsonSerializer.Deserialize<SeasonWrappedDto>(existing.WrappedPayloadJson, JsonOptions);
            if (cached != null)
            {
                response.isSuccess = true;
                response.Data = cached;
                return response;
            }
        }

        var data = await LoadSeasonDataAsync(quinielaId, season);
        if (!data.MembersWithPicks.Contains(membership.Id))
            return Fail(response, "No hay informacion suficiente para generar tu Wrapped.");

        var dto = _engine.Build(data.BuildInput(membership.Id));

        try
        {
            if (existing != null)
            {
                existing.WrappedPayloadJson = JsonSerializer.Serialize(dto, JsonOptions);
                existing.GeneratedAtUtc = DateTime.UtcNow;
                await _unitOfWork.SeasonWrappedSnapshots.UpdateAsync(existing);
            }
            else
            {
                await _unitOfWork.SeasonWrappedSnapshots.InsertAsync(NewSnapshot(quinielaId, seasonId, userId, dto));
            }
            await _unitOfWork.Save();
        }
        catch (Exception ex) when (IsDbUpdateException(ex))
        {
            // Request paralelo ya inserto el snapshot (indice unico QuinielaId, SeasonId, UserId): se devuelve el ganador.
            var winner = await _unitOfWork.SeasonWrappedSnapshots.GetByKeyAsync(quinielaId, seasonId, userId);
            var winnerDto = winner == null ? null : JsonSerializer.Deserialize<SeasonWrappedDto>(winner.WrappedPayloadJson, JsonOptions);
            if (winnerDto == null) return Fail(response, "No se pudo generar tu Wrapped. Intenta de nuevo.");
            response.isSuccess = true;
            response.Data = winnerDto;
            return response;
        }

        response.isSuccess = true;
        response.Data = dto;
        return response;
    }

    public async Task<Response<WrappedGenerationResultDto>> GenerateAllAsync(int quinielaId, int seasonId, int requestingUserId)
    {
        var response = new Response<WrappedGenerationResultDto>();

        var quiniela = await _unitOfWork.Quinielas.GetAsync(quinielaId);
        if (quiniela == null) return Fail(response, "Quiniela no encontrada.");

        var season = await _unitOfWork.Seasons.GetAsync(seasonId);
        if (season == null || season.LeagueId != quiniela.LeagueId) return Fail(response, "Temporada no encontrada.");

        var membership = await _unitOfWork.QuinielaMembers.GetMembershipAsync(quinielaId, requestingUserId);
        var isQuinielaAdmin = membership != null &&
            (string.Equals(membership.Role, "OWNER", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(membership.Role, "ADMIN", StringComparison.OrdinalIgnoreCase));

        if (!isQuinielaAdmin)
        {
            var user = await _unitOfWork.Users.GetAsync(requestingUserId);
            var isGlobalAdmin = user != null && string.Equals(user.Role, "ADMIN", StringComparison.Ordinal);
            if (!isGlobalAdmin)
            {
                return Fail(response, "Solo un administrador puede generar los Wrapped.");
            }
        }

        if (!season.IsFinished) return Fail(response, "La temporada aun no ha finalizado.");

        var data = await LoadSeasonDataAsync(quinielaId, season);
        var generated = 0;
        var skipped = 0;

        var processedUsers = new HashSet<int>();
        foreach (var member in data.Members)
        {
            if (member.UserId <= 0 || !processedUsers.Add(member.UserId) || !data.MembersWithPicks.Contains(member.MemberId))
            {
                skipped++;
                continue;
            }

            var dto = _engine.Build(data.BuildInput(member.MemberId));
            var existing = await _unitOfWork.SeasonWrappedSnapshots.GetByKeyAsync(quinielaId, seasonId, member.UserId);
            if (existing != null)
            {
                existing.WrappedPayloadJson = JsonSerializer.Serialize(dto, JsonOptions);
                existing.GeneratedAtUtc = DateTime.UtcNow;
                await _unitOfWork.SeasonWrappedSnapshots.UpdateAsync(existing);
            }
            else
            {
                await _unitOfWork.SeasonWrappedSnapshots.InsertAsync(NewSnapshot(quinielaId, seasonId, member.UserId, dto));
            }
            generated++;
        }

        await _unitOfWork.Save();

        response.isSuccess = true;
        response.Data = new WrappedGenerationResultDto { SeasonId = seasonId, Generated = generated, Skipped = skipped };
        return response;
    }

    public async Task<Response<WrappedAvailabilityDto>> GetAvailabilityAsync(int quinielaId, int userId)
    {
        var response = new Response<WrappedAvailabilityDto>();

        var quiniela = await _unitOfWork.Quinielas.GetAsync(quinielaId);
        if (quiniela == null) return Fail(response, "Quiniela no encontrada.");

        var membership = await _unitOfWork.QuinielaMembers.GetMembershipAsync(quinielaId, userId);
        if (membership == null) return Fail(response, "No perteneces a esta quiniela.");

        response.isSuccess = true;
        response.Data = new WrappedAvailabilityDto { IsAvailable = false };

        if (!quiniela.LeagueId.HasValue) return response;

        var seasons = (await _unitOfWork.Seasons.GetAllAsync() ?? Enumerable.Empty<Season>())
            .Where(s => s.LeagueId == quiniela.LeagueId.Value && s.IsFinished)
            .OrderByDescending(s => s.EndDate ?? s.Created)
            .ToList();
        if (seasons.Count == 0) return response;

        var picks = ((await _unitOfWork.Picks.GetAllPicksForQuinielaAsync(quinielaId)) ?? Enumerable.Empty<Pick>())
            .Where(p => p.MemberId == membership.Id)
            .ToList();

        foreach (var season in seasons)
        {
            if (picks.Any(p => PickSeasonId(p) == season.Id))
            {
                response.Data = new WrappedAvailabilityDto
                {
                    IsAvailable = true,
                    SeasonId = season.Id,
                    SeasonName = season.Name
                };
                break;
            }
        }

        return response;
    }

    public async Task<Response<bool>> SetSeasonFinishedAsync(int seasonId, bool isFinished, int requestingUserId)
    {
        var response = new Response<bool>();

        var user = await _unitOfWork.Users.GetAsync(requestingUserId);
        if (user == null || !string.Equals(user.Role, "ADMIN", StringComparison.Ordinal))
            return Fail(response, "Solo un administrador puede finalizar la temporada.");

        var season = await _unitOfWork.Seasons.GetAsync(seasonId);
        if (season == null) return Fail(response, "Temporada no encontrada.");

        season.IsFinished = isFinished;
        season.FinishedAtUtc = isFinished ? DateTime.UtcNow : null;
        await _unitOfWork.Seasons.UpdateAsync(season);
        await _unitOfWork.Save();

        response.isSuccess = true;
        response.Data = isFinished;
        return response;
    }

    // ---------------------------------------------------------------- helpers

    private static bool IsDbUpdateException(Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException)
            if (e.GetType().Name == "DbUpdateException") return true;
        return false;
    }

    /// <summary>Construye la entrada pura del motor (expuesto para pruebas del mapeo).</summary>
    public async Task<WrappedEngineInput> BuildEngineInputAsync(int quinielaId, Season season, int targetMemberId)
        => (await LoadSeasonDataAsync(quinielaId, season)).BuildInput(targetMemberId);

    private static int? PickSeasonId(Pick pick) => pick.Match?.Week?.SeasonId;

    private static Response<T> Fail<T>(Response<T> response, string message)
    {
        response.isSuccess = false;
        response.Message = message;
        return response;
    }

    private static SeasonWrappedSnapshot NewSnapshot(int quinielaId, int seasonId, int userId, SeasonWrappedDto dto) => new()
    {
        QuinielaId = quinielaId,
        SeasonId = seasonId,
        UserId = userId,
        WrappedPayloadJson = JsonSerializer.Serialize(dto, JsonOptions),
        GeneratedAtUtc = DateTime.UtcNow,
        // El repositorio filtra por Active y el interceptor de auditoria no lo asigna.
        Active = true
    };

    private async Task<SeasonData> LoadSeasonDataAsync(int quinielaId, Season season)
    {
        var members = ((await _unitOfWork.QuinielaMembers.GetMembersAsync(quinielaId)) ?? Enumerable.Empty<QuinielaMember>()).ToList();
        var allPicks = ((await _unitOfWork.Picks.GetAllPicksForQuinielaAsync(quinielaId)) ?? Enumerable.Empty<Pick>()).ToList();
        var seasonMatches = ((await _unitOfWork.Matches.GetBySeasonIdAsync(season.Id)) ?? Enumerable.Empty<Match>()).ToList();
        var allAwards = ((await _unitOfWork.WeeklyAwards.GetAllAwardsForQuinielaAsync(quinielaId)) ?? Enumerable.Empty<WeeklyAward>()).ToList();

        var seasonMatchIds = seasonMatches.Select(m => m.Id).ToHashSet();
        var seasonPicks = allPicks
            .Where(p => p.Match?.Week != null ? p.Match.Week.SeasonId == season.Id : seasonMatchIds.Contains(p.MatchId))
            .ToList();

        // Semanas conocidas: de los picks (Match.Week) y de los partidos de la temporada.
        var weeksById = new Dictionary<int, Week>();
        foreach (var pick in seasonPicks)
            if (pick.Match?.Week != null) weeksById.TryAdd(pick.Match.Week.Id, pick.Match.Week);
        foreach (var match in seasonMatches)
            if (match.Week != null) weeksById.TryAdd(match.Week.Id, match.Week);

        var memberInfos = members.Select(m => new WrappedMemberInfo(
            m.Id,
            m.UserId,
            !string.IsNullOrWhiteSpace(m.Alias) ? m.Alias : (m.User?.DisplayName ?? string.Empty),
            m.User?.AvatarUrl,
            m.TotalPoints,
            m.TotalHits,
            m.TotalUpsets,
            m.TotalHumillaciones,
            m.BestStreak)).ToList();

        var matchInfos = new List<WrappedMatchInfo>();
        foreach (var match in seasonMatches)
        {
            if (!weeksById.TryGetValue(match.WeekId, out var week)) continue;

            var derbyName = _derbyDetector.DetectDerbies(new[] { match }).FirstOrDefault()?.DerbyName;
            matchInfos.Add(new WrappedMatchInfo(
                match.Id,
                match.WeekId,
                week.WeekNumber,
                match.DateUtc,
                week.FirstGameUtc,
                match.HomeTeam?.Abbreviation ?? string.Empty,
                match.AwayTeam?.Abbreviation ?? string.Empty,
                match.HomeTeam?.Name ?? string.Empty,
                match.AwayTeam?.Name ?? string.Empty,
                match.HomeScore,
                match.AwayScore,
                match.WinnerAbbr,
                match.IsUpset,
                derbyName));
        }

        var knownMatchIds = matchInfos.Select(m => m.MatchId).ToHashSet();
        var pickInfos = seasonPicks.Where(p => knownMatchIds.Contains(p.MatchId)).Select(p => new WrappedPickInfo(
            p.MemberId,
            p.MatchId,
            p.PickAbbr,
            p.IsAutoFilled,
            p.IsHit,
            p.IsUpsetHit,
            p.IsHumillacion,
            p.IsSomnifero,
            p.IsEmpateFallido,
            p.LastModified ?? p.Created)).ToList();

        var awardInfos = allAwards
            .Where(a => weeksById.ContainsKey(a.WeekId))
            .Select(a => new WrappedAwardInfo(a.MemberId, a.WeekId, a.AwardType))
            .ToList();

        return new SeasonData(season, memberInfos, matchInfos, pickInfos, awardInfos,
            pickInfos.Select(p => p.MemberId).ToHashSet());
    }

    private sealed record SeasonData(
        Season Season,
        List<WrappedMemberInfo> Members,
        List<WrappedMatchInfo> Matches,
        List<WrappedPickInfo> Picks,
        List<WrappedAwardInfo> Awards,
        HashSet<int> MembersWithPicks)
    {
        public WrappedEngineInput BuildInput(int targetMemberId) =>
            new(Season.Id, Season.Name, targetMemberId, Members, Matches, Picks, Awards);
    }
}

