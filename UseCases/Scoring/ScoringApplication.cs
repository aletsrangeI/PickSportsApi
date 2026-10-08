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

        bool isFootball = string.Equals(quiniela.League?.Sport?.Name, "Football", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(quiniela.League?.Code, "nfl", StringComparison.OrdinalIgnoreCase);

        var allHistoricalPicks = (await _unitOfWork.Picks.GetAllPicksForQuinielaAsync(quinielaId))
            .Where(p => p.Match?.Week?.SeasonId == week.SeasonId)
            .ToList();

        _scoringEngine.EvaluatePicksAndMatches(matches, weekPicks, members.Count, isFootball);
        _scoringEngine.ApplyRecidivistAutofillPenalty(members, weekPicks, allHistoricalPicks, week.WeekNumber, seasonId: week.SeasonId);

        // 1. Tabla Semanal (Desempate en cascada)
        var weeklyStandings = _scoringEngine.CalculateWeeklyStandings(members, weekPicks, matches);

        // 2. Tabla General Acumulada de la temporada (con delta respecto a la jornada anterior)
        var seasonAwards = await _unitOfWork.WeeklyAwards.GetAllAwardsForQuinielaAsync(quinielaId);
        var sortedGeneral = _scoringEngine.CalculateGeneralStandings(members, allHistoricalPicks, week.SeasonId, week.WeekNumber, seasonAwards);

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

        var league = await _unitOfWork.Leagues.GetAsync(quiniela.LeagueId);
        if (league?.IsPlayoffWeek(week.WeekNumber) == true)
        {
            response.isSuccess = false;
            response.Message = "Las jornadas de Liguilla no se califican en la quiniela de temporada regular.";
            return response;
        }

        var members = (await _unitOfWork.QuinielaMembers.GetMembersAsync(quinielaId)).ToList();
        var matches = (await _unitOfWork.Matches.GetByWeekIdAsync(weekId)).ToList();
        var weekPicks = (await _unitOfWork.Picks.GetAllPicksForWeekAsync(quinielaId, weekId)).ToList();

        var allHistoricalPicks = (await _unitOfWork.Picks.GetAllPicksForQuinielaAsync(quinielaId))
            .Where(p => p.Match?.Week?.SeasonId == week.SeasonId)
            .ToList();

        bool isFootball = string.Equals(quiniela.League?.Sport?.Name, "Football", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(quiniela.League?.Code, "nfl", StringComparison.OrdinalIgnoreCase);

        // 1. Evaluar aciertos, upsets y castigos
        _scoringEngine.EvaluatePicksAndMatches(matches, weekPicks, members.Count, isFootball);
        _scoringEngine.ApplyRecidivistAutofillPenalty(members, weekPicks, allHistoricalPicks, week.WeekNumber, seasonId: week.SeasonId);

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

        // 6. Actualizar acumulados y rachas en QuinielaMember.
        // Solo con jornadas de la temporada en juego de la quiniela (la de sus picks más recientes):
        // recalificar una jornada histórica no debe sobrescribir los acumulados de la temporada en curso (H-009).
        var quinielaPicks = (await _unitOfWork.Picks.GetAllPicksForQuinielaAsync(quinielaId)).ToList();
        bool isCurrentSeasonWeek = QuinielaSeasonResolver.AppliesToWeek(quinielaPicks, week);

        allHistoricalPicks = quinielaPicks
            .Where(p => p.Match?.Week?.SeasonId == week.SeasonId)
            .ToList();
        var historicalByMember = allHistoricalPicks
            .Where(p => p.IsHit.HasValue)
            .GroupBy(p => p.MemberId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var member in isCurrentSeasonWeek ? members : new List<QuinielaMember>())
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

        var awardsList = awards.ToList();
        var dtos = new List<WeeklyAwardDto>();

        if (awardsList.Any())
        {
            var members = (await _unitOfWork.QuinielaMembers.GetMembersAsync(quinielaId)).ToList();
            int totalMembers = members.Count;
            var weekGroups = awardsList.GroupBy(a => a.WeekId);

            foreach (var group in weekGroups)
            {
                int wId = group.Key;
                var weekMatches = (await _unitOfWork.Matches.GetByWeekIdAsync(wId)).ToList();
                var weekPicks = (await _unitOfWork.Picks.GetAllPicksForWeekAsync(quinielaId, wId)).ToList();

                _scoringEngine.EvaluatePicksAndMatches(weekMatches, weekPicks, totalMembers);

                foreach (var a in group)
                {
                    var matchDetails = ScoringEngine.GetAwardTriggerDetails(
                        a.AwardType,
                        a.MemberId,
                        weekMatches,
                        weekPicks,
                        totalMembers);

                    string notes = a.Notes ?? string.Empty;

                    // Si no tiene detalle enriquecido o es la nota antigua básica
                    if (matchDetails.Any() && (string.IsNullOrWhiteSpace(notes) || !notes.Contains(". Partidos:")))
                    {
                        int awardVal = int.TryParse(a.AwardValue1, out var parsedVal) ? parsedVal : matchDetails.Count;
                        notes = ScoringEngine.FormatEnrichedNotes(
                            a.AwardType,
                            a.Member?.Alias ?? "Participante",
                            awardVal,
                            matchDetails,
                            ScoringEngine.GetBaseNoteTemplate(a.AwardType));

                        if (a.Notes != notes)
                        {
                            a.Notes = notes;
                            await _unitOfWork.WeeklyAwards.UpdateAsync(a);
                        }
                    }

                    dtos.Add(new WeeklyAwardDto
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
                        Notes = notes,
                        MatchDetails = matchDetails
                    });
                }
            }
        }

        response.isSuccess = true;
        response.Message = "Galardones obtenidos.";
        response.Data = dtos;
        return response;
    }
}
