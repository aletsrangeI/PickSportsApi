using Common;
using Domain.Entities;
using DTO.Scoring;
using Interface.Persistence;
using Interface.UseCases;

namespace UseCases.Scoring;

public class ScoringApplication : IScoringApplication
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ScoringEngine _scoringEngine;

    public ScoringApplication(IUnitOfWork unitOfWork, ScoringEngine scoringEngine)
    {
        _unitOfWork = unitOfWork;
        _scoringEngine = scoringEngine;
    }

    public async Task<Response<QuinielaStandingsResponseDto>> GetStandingsAsync(int quinielaId, int weekId)
    {
        var response = new Response<QuinielaStandingsResponseDto>();

        var quiniela = await _unitOfWork.Quinielas.GetAsync(quinielaId);
        if (quiniela == null)
        {
            response.isSuccess = false;
            response.Message = "Quiniela no encontrada.";
            return response;
        }

        var week = await _unitOfWork.Weeks.GetAsync(weekId);
        if (week == null)
        {
            response.isSuccess = false;
            response.Message = "Jornada no encontrada.";
            return response;
        }

        var members = (await _unitOfWork.QuinielaMembers.GetMembersAsync(quinielaId)).ToList();
        var matches = (await _unitOfWork.Matches.GetByWeekIdAsync(weekId)).ToList();
        var weekPicks = (await _unitOfWork.Picks.GetAllPicksForWeekAsync(quinielaId, weekId)).ToList();

        // 1. Tabla Semanal (Desempate en cascada)
        var weeklyStandings = _scoringEngine.CalculateWeeklyStandings(members, weekPicks, matches);

        // 2. Tabla General Acumulada
        var allHistoricalPicks = (await _unitOfWork.Picks.GetAllPicksForQuinielaAsync(quinielaId)).ToList();
        var picksByMember = allHistoricalPicks
            .Where(p => p.IsHit.HasValue)
            .GroupBy(p => p.MemberId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var generalList = new List<MemberStandingDto>();
        foreach (var m in members)
        {
            var mHistorical = picksByMember.GetValueOrDefault(m.Id) ?? new List<Pick>();
            int totalPicks = mHistorical.Count;
            int totalHits = mHistorical.Count(p => p.IsHit == true);
            int totalUpsets = mHistorical.Count(p => p.IsUpsetHit);
            int totalHumillaciones = mHistorical.Count(p => p.IsHumillacion);
            int totalSomniferos = mHistorical.Count(p => p.IsSomnifero);
            int totalEmpatesFallidos = mHistorical.Count(p => p.IsEmpateFallido);
            decimal acc = totalPicks > 0 ? Math.Round((decimal)totalHits / totalPicks * 100m, 1) : 0m;

            var (currentStreak, bestStreak) = _scoringEngine.CalculateMemberStreaks(mHistorical);

            generalList.Add(new MemberStandingDto
            {
                MemberId = m.Id,
                UserId = m.UserId,
                Alias = m.Alias,
                DisplayName = m.User?.DisplayName ?? m.Alias,
                AvatarUrl = m.User?.AvatarUrl,
                Hits = totalHits,
                TotalPicks = totalPicks,
                AccuracyPct = acc,
                UpsetHits = totalUpsets,
                Humillaciones = totalHumillaciones,
                Somniferos = totalSomniferos,
                EmpatesFallidos = totalEmpatesFallidos,
                CurrentStreak = currentStreak,
                BestStreak = bestStreak
            });
        }

        var sortedGeneral = generalList
            .OrderByDescending(s => s.Hits)
            .ThenByDescending(s => s.UpsetHits)
            .ThenBy(s => s.Humillaciones)
            .ThenBy(s => s.Alias)
            .ToList();

        for (int i = 0; i < sortedGeneral.Count; i++)
        {
            sortedGeneral[i].Rank = i + 1;
        }

        var finishedMatchesCount = matches.Count(m => string.Equals(m.StatusState, "post", StringComparison.OrdinalIgnoreCase));

        response.isSuccess = true;
        response.Message = "Posiciones obtenidas correctamente.";
        response.Data = new QuinielaStandingsResponseDto
        {
            QuinielaId = quinielaId,
            WeekId = weekId,
            WeekNumber = week.WeekNumber,
            WeekName = week.Name,
            WeekStatus = week.Status,
            FinishedMatchesCount = finishedMatchesCount,
            TotalMatchesCount = matches.Count,
            WeeklyStandings = weeklyStandings,
            GeneralStandings = sortedGeneral
        };

        return response;
    }

    public async Task<Response<ScoreWeekResultDto>> ScoreWeekAsync(int quinielaId, int weekId, int userId)
    {
        var response = new Response<ScoreWeekResultDto>();

        var quiniela = await _unitOfWork.Quinielas.GetAsync(quinielaId);
        if (quiniela == null)
        {
            response.isSuccess = false;
            response.Message = "Quiniela no encontrada.";
            return response;
        }

        var membership = await _unitOfWork.QuinielaMembers.GetMembershipAsync(quinielaId, userId);
        if (membership == null || (membership.Role != "ADMIN" && membership.Role != "OWNER"))
        {
            response.isSuccess = false;
            response.Message = "Solo los administradores u owners pueden calificar la jornada.";
            return response;
        }

        var week = await _unitOfWork.Weeks.GetAsync(weekId);
        if (week == null)
        {
            response.isSuccess = false;
            response.Message = "Jornada no encontrada.";
            return response;
        }

        var members = (await _unitOfWork.QuinielaMembers.GetMembersAsync(quinielaId)).ToList();
        var matches = (await _unitOfWork.Matches.GetByWeekIdAsync(weekId)).ToList();
        var weekPicks = (await _unitOfWork.Picks.GetAllPicksForWeekAsync(quinielaId, weekId)).ToList();

        bool isFootball = string.Equals(quiniela.League?.Sport?.Name, "Football", StringComparison.OrdinalIgnoreCase) 
                       || string.Equals(quiniela.League?.Code, "nfl", StringComparison.OrdinalIgnoreCase);

        // 1. Evaluar aciertos, upsets y castigos
        _scoringEngine.EvaluatePicksAndMatches(matches, weekPicks, members.Count, isFootball);

        // 2. Persistir picks calificados
        foreach (var pick in weekPicks)
        {
            await _unitOfWork.Picks.UpsertPickAsync(pick);
        }

        // 3. Persistir partidos (IsUpset)
        foreach (var match in matches)
        {
            await _unitOfWork.Matches.UpdateAsync(match);
        }

        // 4. Calcular tabla semanal
        var weeklyStandings = _scoringEngine.CalculateWeeklyStandings(members, weekPicks, matches);

        // 5. Generar y persistir galardones
        var awards = _scoringEngine.CalculateAwards(quinielaId, weekId, weeklyStandings, matches, weekPicks);
        await _unitOfWork.WeeklyAwards.DeleteAwardsForWeekAsync(quinielaId, weekId);

        foreach (var award in awards)
        {
            await _unitOfWork.WeeklyAwards.InsertAsync(award);
        }

        // 6. Actualizar acumulados y rachas en QuinielaMember
        var allHistoricalPicks = (await _unitOfWork.Picks.GetAllPicksForQuinielaAsync(quinielaId)).ToList();
        var historicalByMember = allHistoricalPicks
            .Where(p => p.IsHit.HasValue)
            .GroupBy(p => p.MemberId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var member in members)
        {
            var mHistorical = historicalByMember.GetValueOrDefault(member.Id) ?? new List<Pick>();
            member.TotalHits = mHistorical.Count(p => p.IsHit == true);
            member.TotalUpsets = mHistorical.Count(p => p.IsUpsetHit);
            member.TotalHumillaciones = mHistorical.Count(p => p.IsHumillacion);

            var (currentStreak, bestStreak) = _scoringEngine.CalculateMemberStreaks(mHistorical);
            member.CurrentStreak = currentStreak;
            member.BestStreak = bestStreak;
            member.LastModified = DateTime.UtcNow;

            await _unitOfWork.QuinielaMembers.UpdateAsync(member);
        }

        // 7. Marcar jornada como SCORED
        week.Status = "SCORED";
        week.ScoredAt = DateTime.UtcNow;
        week.LastModified = DateTime.UtcNow;
        await _unitOfWork.Weeks.UpdateAsync(week);

        await _unitOfWork.Save();

        var finishedMatchesCount = matches.Count(m => string.Equals(m.StatusState, "post", StringComparison.OrdinalIgnoreCase));

        response.isSuccess = true;
        response.Message = $"Jornada {week.WeekNumber} calificada exitosamente. {finishedMatchesCount} partidos procesados, {weekPicks.Count} picks evaluados y {awards.Count} galardones asignados.";
        response.Data = new ScoreWeekResultDto
        {
            QuinielaId = quinielaId,
            WeekId = weekId,
            ScoredMatchesCount = finishedMatchesCount,
            PicksEvaluatedCount = weekPicks.Count,
            AwardsGeneratedCount = awards.Count,
            Message = response.Message
        };

        return response;
    }

    public async Task<Response<IEnumerable<WeeklyAwardDto>>> GetAwardsAsync(int quinielaId, int? weekId)
    {
        var response = new Response<IEnumerable<WeeklyAwardDto>>();

        var quiniela = await _unitOfWork.Quinielas.GetAsync(quinielaId);
        if (quiniela == null)
        {
            response.isSuccess = false;
            response.Message = "Quiniela no encontrada.";
            return response;
        }

        IEnumerable<WeeklyAward> awards;
        if (weekId.HasValue && weekId.Value > 0)
        {
            awards = await _unitOfWork.WeeklyAwards.GetAwardsByWeekAsync(quinielaId, weekId.Value);
        }
        else
        {
            awards = await _unitOfWork.WeeklyAwards.GetAllAwardsForQuinielaAsync(quinielaId);
        }

        var dtos = awards.Select(a => new WeeklyAwardDto
        {
            Id = a.Id,
            QuinielaId = a.QuinielaId,
            WeekId = a.WeekId,
            MemberId = a.MemberId,
            MemberAlias = a.Member?.Alias ?? "Anónimo",
            DisplayName = a.Member?.User?.DisplayName ?? a.Member?.Alias,
            AvatarUrl = a.Member?.User?.AvatarUrl,
            AwardType = a.AwardType,
            AwardValue1 = a.AwardValue1,
            AwardValue2 = a.AwardValue2,
            Notes = a.Notes
        }).ToList();

        response.isSuccess = true;
        response.Message = "Galardones obtenidos.";
        response.Data = dtos;
        return response;
    }
}
