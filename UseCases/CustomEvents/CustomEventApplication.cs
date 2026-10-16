using Common;
using Domain.Entities;
using DTO.CustomEvents;
using Interface.Persistence;
using Interface.UseCases;

namespace UseCases.CustomEvents;

public class CustomEventApplication : ICustomEventApplication
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly CustomEventScoringEngine _scoringEngine;

    public CustomEventApplication(IUnitOfWork unitOfWork, CustomEventScoringEngine scoringEngine)
    {
        _unitOfWork = unitOfWork;
        _scoringEngine = scoringEngine;
    }

    public async Task<Response<IEnumerable<EventRoundSummaryDto>>> GetRoundsAsync(int quinielaId, int userId)
    {
        var response = new Response<IEnumerable<EventRoundSummaryDto>>();

        var quiniela = await _unitOfWork.Quinielas.GetAsync(quinielaId);
        if (quiniela == null || !quiniela.IsActive)
        {
            response.isSuccess = false;
            response.Message = "La quiniela no existe o está inactiva.";
            return response;
        }

        var member = await _unitOfWork.QuinielaMembers.GetMembershipAsync(quinielaId, userId);
        var user = await _unitOfWork.Users.GetAsync(userId);
        bool isGlobalAdmin = user?.Role == "ADMIN";

        if (member == null && !isGlobalAdmin)
        {
            response.isSuccess = false;
            response.Message = "No eres miembro de esta quiniela.";
            return response;
        }

        var rounds = (await _unitOfWork.CustomEvents.GetRoundsByQuinielaIdAsync(quinielaId)).ToList();
        var summaryList = new List<EventRoundSummaryDto>();

        foreach (var r in rounds)
        {
            var activeQuestions = r.Questions.Where(q => q.Active).ToList();
            var userPicks = member != null
                ? activeQuestions.SelectMany(q => q.Picks).Where(p => p.Active && p.QuinielaMemberId == member.Id).ToList()
                : new List<EventPick>();

            summaryList.Add(new EventRoundSummaryDto
            {
                Id = r.Id,
                EventGroupId = r.EventGroupId,
                QuinielaId = quinielaId,
                Title = r.Title,
                RoundOrder = r.RoundOrder,
                LockDateUtc = r.LockDateUtc,
                Status = r.Status,
                ResolvedDateUtc = r.ResolvedDateUtc,
                QuestionsCount = activeQuestions.Count,
                UserPicksCount = userPicks.Count,
                TotalPossiblePoints = activeQuestions.Sum(q => q.Points),
                UserPointsEarned = userPicks.Where(p => p.IsHit == true).Sum(p => p.PointsEarned)
            });
        }

        response.isSuccess = true;
        response.Message = "Rondas de eventos consultadas exitosamente.";
        response.Data = summaryList;
        return response;
    }

    public async Task<Response<EventRoundDetailDto>> GetRoundDetailAsync(int quinielaId, int roundId, int userId)
    {
        var response = new Response<EventRoundDetailDto>();

        var quiniela = await _unitOfWork.Quinielas.GetAsync(quinielaId);
        if (quiniela == null || !quiniela.IsActive)
        {
            response.isSuccess = false;
            response.Message = "La quiniela no existe o está inactiva.";
            return response;
        }

        var member = await _unitOfWork.QuinielaMembers.GetMembershipAsync(quinielaId, userId);
        var user = await _unitOfWork.Users.GetAsync(userId);
        bool isGlobalAdmin = user?.Role == "ADMIN";

        if (member == null && !isGlobalAdmin)
        {
            response.isSuccess = false;
            response.Message = "No eres miembro de esta quiniela.";
            return response;
        }

        var round = await _unitOfWork.CustomEvents.GetRoundByIdWithDetailsAsync(roundId);
        if (round == null || !round.Active || (round.EventGroup != null && round.EventGroup.QuinielaId != quinielaId))
        {
            response.isSuccess = false;
            response.Message = "Ronda no encontrada en esta quiniela.";
            return response;
        }

        bool isLocked = round.Status == "Locked" || round.Status == "Resolved" || DateTime.UtcNow >= round.LockDateUtc;
        bool isOwnerOrAdmin = (member != null && (member.Role == "OWNER" || member.Role == "ADMIN")) || isGlobalAdmin;

        var questionDtos = new List<EventQuestionDto>();
        var allMemberPicks = new List<EventMemberPickDto>();

        foreach (var q in round.Questions.Where(q => q.Active))
        {
            var activePicks = q.Picks.Where(p => p.Active).ToList();
            int totalPicksOnQuestion = activePicks.Count;

            var userPick = member != null
                ? activePicks.FirstOrDefault(p => p.QuinielaMemberId == member.Id)
                : null;

            var optionDtos = q.Options.Where(o => o.Active).Select(opt =>
            {
                int picksCount = activePicks.Count(p => p.EventOptionId == opt.Id);
                decimal pickPercentage = totalPicksOnQuestion > 0
                    ? Math.Round((decimal)picksCount / totalPicksOnQuestion * 100m, 1)
                    : 0m;

                return new EventOptionDto
                {
                    Id = opt.Id,
                    EventQuestionId = opt.EventQuestionId,
                    OptionText = opt.OptionText,
                    ImageUrl = opt.ImageUrl,
                    IsCorrect = (round.Status == "Resolved" || isOwnerOrAdmin) ? opt.IsCorrect : null,
                    OptionOrder = opt.OptionOrder,
                    PicksCount = isLocked ? picksCount : (userPick?.EventOptionId == opt.Id ? 1 : 0),
                    PickPercentage = isLocked ? pickPercentage : 0m
                };
            }).ToList();

            questionDtos.Add(new EventQuestionDto
            {
                Id = q.Id,
                EventRoundId = q.EventRoundId,
                QuestionText = q.QuestionText,
                Description = q.Description,
                Points = q.Points,
                QuestionType = q.QuestionType,
                QuestionOrder = q.QuestionOrder,
                Options = optionDtos,
                UserSelectedOptionId = userPick?.EventOptionId,
                UserIsHit = userPick?.IsHit,
                UserPointsEarned = userPick?.PointsEarned ?? 0
            });

            if (isLocked)
            {
                foreach (var pick in activePicks)
                {
                    allMemberPicks.Add(new EventMemberPickDto
                    {
                        MemberId = pick.QuinielaMemberId,
                        MemberAlias = pick.QuinielaMember?.Alias ?? "Participante",
                        AvatarUrl = pick.QuinielaMember?.User?.AvatarUrl,
                        EventQuestionId = pick.EventQuestionId,
                        EventOptionId = pick.EventOptionId,
                        OptionText = pick.EventOption?.OptionText ?? string.Empty,
                        IsHit = pick.IsHit,
                        PointsEarned = pick.PointsEarned,
                        SubmittedAtUtc = pick.SubmittedAtUtc
                    });
                }
            }
        }

        response.isSuccess = true;
        response.Message = "Detalle de la ronda obtenido con éxito.";
        response.Data = new EventRoundDetailDto
        {
            Id = round.Id,
            EventGroupId = round.EventGroupId,
            QuinielaId = quinielaId,
            Title = round.Title,
            RoundOrder = round.RoundOrder,
            LockDateUtc = round.LockDateUtc,
            Status = round.Status,
            ResolvedDateUtc = round.ResolvedDateUtc,
            CurrentUserMemberId = member?.Id,
            IsOwnerOrAdmin = isOwnerOrAdmin,
            Questions = questionDtos,
            AllMemberPicks = allMemberPicks
        };

        return response;
    }

    public async Task<Response<EventRoundSummaryDto>> CreateRoundAsync(int quinielaId, int userId, CreateEventRoundDto request)
    {
        var response = new Response<EventRoundSummaryDto>();

        var quiniela = await _unitOfWork.Quinielas.GetAsync(quinielaId);
        if (quiniela == null || !quiniela.IsActive)
        {
            response.isSuccess = false;
            response.Message = "La quiniela no existe o está inactiva.";
            return response;
        }

        var member = await _unitOfWork.QuinielaMembers.GetMembershipAsync(quinielaId, userId);
        var user = await _unitOfWork.Users.GetAsync(userId);
        bool isGlobalAdmin = user?.Role == "ADMIN";
        bool isOwnerOrAdmin = (member != null && (member.Role == "OWNER" || member.Role == "ADMIN")) || isGlobalAdmin;

        if (!isOwnerOrAdmin)
        {
            response.isSuccess = false;
            response.Message = "Solo el creador o administrador puede crear rondas.";
            return response;
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            response.isSuccess = false;
            response.Message = "El título de la ronda es obligatorio.";
            return response;
        }

        var group = await _unitOfWork.CustomEvents.GetOrCreateEventGroupForQuinielaAsync(quinielaId, quiniela.Name);
        int maxOrder = await _unitOfWork.CustomEvents.GetMaxRoundOrderByQuinielaIdAsync(quinielaId);
        int roundOrder = request.RoundOrder ?? (maxOrder + 1);

        var round = new EventRound
        {
            EventGroupId = group.Id,
            Title = request.Title.Trim(),
            RoundOrder = roundOrder,
            LockDateUtc = DateTime.SpecifyKind(request.LockDateUtc, DateTimeKind.Utc),
            Status = "Open",
            Active = true,
            Created = DateTime.UtcNow,
            CreatedBy = user?.Username ?? "System"
        };

        var inserted = await _unitOfWork.CustomEvents.InsertRoundAsync(round);
        if (!inserted)
        {
            response.isSuccess = false;
            response.Message = "Error al crear la ronda en la base de datos.";
            return response;
        }

        response.isSuccess = true;
        response.Message = "Ronda creada exitosamente.";
        response.Data = new EventRoundSummaryDto
        {
            Id = round.Id,
            EventGroupId = round.EventGroupId,
            QuinielaId = quinielaId,
            Title = round.Title,
            RoundOrder = round.RoundOrder,
            LockDateUtc = round.LockDateUtc,
            Status = round.Status,
            ResolvedDateUtc = null,
            QuestionsCount = 0,
            UserPicksCount = 0,
            TotalPossiblePoints = 0,
            UserPointsEarned = 0
        };

        return response;
    }

    public async Task<Response<EventRoundSummaryDto>> UpdateRoundAsync(int quinielaId, int roundId, int userId, UpdateEventRoundDto request)
    {
        var response = new Response<EventRoundSummaryDto>();

        var member = await _unitOfWork.QuinielaMembers.GetMembershipAsync(quinielaId, userId);
        var user = await _unitOfWork.Users.GetAsync(userId);
        bool isGlobalAdmin = user?.Role == "ADMIN";
        bool isOwnerOrAdmin = (member != null && (member.Role == "OWNER" || member.Role == "ADMIN")) || isGlobalAdmin;

        if (!isOwnerOrAdmin)
        {
            response.isSuccess = false;
            response.Message = "Solo el creador o administrador puede actualizar rondas.";
            return response;
        }

        var round = await _unitOfWork.CustomEvents.GetRoundByIdAsync(roundId);
        if (round == null || !round.Active || (round.EventGroup != null && round.EventGroup.QuinielaId != quinielaId))
        {
            response.isSuccess = false;
            response.Message = "Ronda no encontrada.";
            return response;
        }

        if (round.Status == "Resolved")
        {
            response.isSuccess = false;
            response.Message = "No se puede editar una ronda ya resuelta.";
            return response;
        }

        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            round.Title = request.Title.Trim();
        }

        round.LockDateUtc = DateTime.SpecifyKind(request.LockDateUtc, DateTimeKind.Utc);

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            round.Status = request.Status.Trim();
        }

        round.LastModified = DateTime.UtcNow;
        round.LastModifiedBy = user?.Username ?? "System";

        var updated = await _unitOfWork.CustomEvents.UpdateRoundAsync(round);
        if (!updated)
        {
            response.isSuccess = false;
            response.Message = "Error al actualizar la ronda.";
            return response;
        }

        response.isSuccess = true;
        response.Message = "Ronda actualizada exitosamente.";
        response.Data = new EventRoundSummaryDto
        {
            Id = round.Id,
            EventGroupId = round.EventGroupId,
            QuinielaId = quinielaId,
            Title = round.Title,
            RoundOrder = round.RoundOrder,
            LockDateUtc = round.LockDateUtc,
            Status = round.Status,
            ResolvedDateUtc = round.ResolvedDateUtc
        };

        return response;
    }

    public async Task<Response<EventQuestionDto>> AddQuestionAsync(int quinielaId, int roundId, int userId, CreateEventQuestionDto request)
    {
        var response = new Response<EventQuestionDto>();

        var member = await _unitOfWork.QuinielaMembers.GetMembershipAsync(quinielaId, userId);
        var user = await _unitOfWork.Users.GetAsync(userId);
        bool isGlobalAdmin = user?.Role == "ADMIN";
        bool isOwnerOrAdmin = (member != null && (member.Role == "OWNER" || member.Role == "ADMIN")) || isGlobalAdmin;

        if (!isOwnerOrAdmin)
        {
            response.isSuccess = false;
            response.Message = "Solo el creador o administrador puede agregar preguntas.";
            return response;
        }

        var round = await _unitOfWork.CustomEvents.GetRoundByIdAsync(roundId);
        if (round == null || !round.Active || (round.EventGroup != null && round.EventGroup.QuinielaId != quinielaId))
        {
            response.isSuccess = false;
            response.Message = "Ronda no encontrada.";
            return response;
        }

        if (round.Status == "Resolved")
        {
            response.isSuccess = false;
            response.Message = "No se pueden agregar preguntas a una ronda ya resuelta.";
            return response;
        }

        if (string.IsNullOrWhiteSpace(request.QuestionText))
        {
            response.isSuccess = false;
            response.Message = "El texto de la pregunta es obligatorio.";
            return response;
        }

        if (request.Options == null || request.Options.Count < 2)
        {
            response.isSuccess = false;
            response.Message = "La pregunta debe incluir al menos 2 opciones.";
            return response;
        }

        if (request.Options.Any(o => string.IsNullOrWhiteSpace(o.OptionText)))
        {
            response.isSuccess = false;
            response.Message = "Todas las opciones deben tener texto descriptivo.";
            return response;
        }

        int maxOrder = await _unitOfWork.CustomEvents.GetMaxQuestionOrderByRoundIdAsync(roundId);
        int questionOrder = request.QuestionOrder ?? (maxOrder + 1);

        var question = new EventQuestion
        {
            EventRoundId = roundId,
            QuestionText = request.QuestionText.Trim(),
            Description = request.Description?.Trim(),
            Points = request.Points > 0 ? request.Points : 1,
            QuestionType = string.IsNullOrWhiteSpace(request.QuestionType) ? "SingleChoice" : request.QuestionType.Trim(),
            QuestionOrder = questionOrder,
            Active = true,
            Created = DateTime.UtcNow,
            CreatedBy = user?.Username ?? "System"
        };

        int optOrder = 1;
        foreach (var opt in request.Options)
        {
            question.Options.Add(new EventOption
            {
                OptionText = opt.OptionText.Trim(),
                ImageUrl = opt.ImageUrl?.Trim(),
                OptionOrder = opt.OptionOrder > 0 ? opt.OptionOrder : optOrder++,
                Active = true,
                Created = DateTime.UtcNow,
                CreatedBy = user?.Username ?? "System"
            });
        }

        var inserted = await _unitOfWork.CustomEvents.InsertQuestionAsync(question);
        if (!inserted)
        {
            response.isSuccess = false;
            response.Message = "Error al guardar la pregunta en la base de datos.";
            return response;
        }

        response.isSuccess = true;
        response.Message = "Pregunta agregada exitosamente.";
        response.Data = new EventQuestionDto
        {
            Id = question.Id,
            EventRoundId = question.EventRoundId,
            QuestionText = question.QuestionText,
            Description = question.Description,
            Points = question.Points,
            QuestionType = question.QuestionType,
            QuestionOrder = question.QuestionOrder,
            Options = question.Options.Select(o => new EventOptionDto
            {
                Id = o.Id,
                EventQuestionId = o.EventQuestionId,
                OptionText = o.OptionText,
                ImageUrl = o.ImageUrl,
                IsCorrect = null,
                OptionOrder = o.OptionOrder,
                PicksCount = 0,
                PickPercentage = 0m
            }).ToList()
        };

        return response;
    }

    public async Task<Response<EventRoundDetailDto>> SubmitPicksAsync(int quinielaId, int roundId, int userId, SubmitEventPicksDto request)
    {
        var response = new Response<EventRoundDetailDto>();

        var member = await _unitOfWork.QuinielaMembers.GetMembershipAsync(quinielaId, userId);
        if (member == null || !member.Active)
        {
            response.isSuccess = false;
            response.Message = "No eres miembro activo de esta quiniela.";
            return response;
        }

        var round = await _unitOfWork.CustomEvents.GetRoundByIdWithDetailsAsync(roundId);
        if (round == null || !round.Active || (round.EventGroup != null && round.EventGroup.QuinielaId != quinielaId))
        {
            response.isSuccess = false;
            response.Message = "Ronda no encontrada.";
            return response;
        }

        bool isLocked = round.Status == "Locked" || round.Status == "Resolved" || DateTime.UtcNow >= round.LockDateUtc;
        if (isLocked)
        {
            response.isSuccess = false;
            response.Message = "La ronda está bloqueada. Ya no se pueden emitir o modificar pronósticos.";
            return response;
        }

        if (request.Picks == null || !request.Picks.Any())
        {
            response.isSuccess = false;
            response.Message = "Debe enviar al menos un pronóstico.";
            return response;
        }

        var questionsMap = round.Questions.Where(q => q.Active).ToDictionary(q => q.Id);
        var picksToSave = new List<EventPick>();

        foreach (var pickReq in request.Picks)
        {
            if (!questionsMap.TryGetValue(pickReq.EventQuestionId, out var question))
            {
                response.isSuccess = false;
                response.Message = $"La pregunta {pickReq.EventQuestionId} no pertenece a esta ronda.";
                return response;
            }

            var validOption = question.Options.FirstOrDefault(o => o.Id == pickReq.EventOptionId && o.Active);
            if (validOption == null)
            {
                response.isSuccess = false;
                response.Message = $"La opción {pickReq.EventOptionId} no es válida para la pregunta {question.QuestionText}.";
                return response;
            }

            picksToSave.Add(new EventPick
            {
                EventQuestionId = pickReq.EventQuestionId,
                EventOptionId = pickReq.EventOptionId,
                QuinielaMemberId = member.Id,
                SubmittedAtUtc = DateTime.UtcNow,
                Active = true,
                Created = DateTime.UtcNow
            });
        }

        var saved = await _unitOfWork.CustomEvents.UpsertPicksAsync(picksToSave);
        if (!saved)
        {
            response.isSuccess = false;
            response.Message = "Error al registrar los pronósticos.";
            return response;
        }

        var detailResponse = await GetRoundDetailAsync(quinielaId, roundId, userId);
        detailResponse.Message = "Pronósticos guardados exitosamente.";
        return detailResponse;
    }

    public async Task<Response<EventRoundDetailDto>> ResolveRoundAsync(int quinielaId, int roundId, int userId, ResolveEventRoundDto request)
    {
        var response = new Response<EventRoundDetailDto>();

        var member = await _unitOfWork.QuinielaMembers.GetMembershipAsync(quinielaId, userId);
        var user = await _unitOfWork.Users.GetAsync(userId);
        bool isGlobalAdmin = user?.Role == "ADMIN";
        bool isOwnerOrAdmin = (member != null && (member.Role == "OWNER" || member.Role == "ADMIN")) || isGlobalAdmin;

        if (!isOwnerOrAdmin)
        {
            response.isSuccess = false;
            response.Message = "Solo el creador o administrador puede resolver los resultados de la ronda.";
            return response;
        }

        if (request.Resolutions == null || !request.Resolutions.Any())
        {
            response.isSuccess = false;
            response.Message = "Debe proporcionar los resultados de las preguntas a resolver.";
            return response;
        }

        var round = await _unitOfWork.CustomEvents.GetRoundByIdWithDetailsAsync(roundId);
        if (round == null || !round.Active || (round.EventGroup != null && round.EventGroup.QuinielaId != quinielaId))
        {
            response.isSuccess = false;
            response.Message = "Ronda no encontrada.";
            return response;
        }

        var roundPicks = (await _unitOfWork.CustomEvents.GetPicksByRoundIdAsync(roundId)).ToList();

        // 1. Evaluar aciertos y puntos en opciones y picks
        _scoringEngine.EvaluateRound(round.Questions, request.Resolutions, roundPicks);

        // 2. Marcar ronda como Resuelta
        round.Status = "Resolved";
        round.ResolvedDateUtc = DateTime.UtcNow;
        round.LastModified = DateTime.UtcNow;
        round.LastModifiedBy = user?.Username ?? "System";

        await _unitOfWork.CustomEvents.UpdateRoundAsync(round);
        await _unitOfWork.CustomEvents.UpsertPicksAsync(roundPicks);

        // 3. Recalcular TotalPoints y TotalHits de los miembros en QuinielaMember
        var members = (await _unitOfWork.QuinielaMembers.GetMembersAsync(quinielaId)).ToList();
        var allQuinielaEventPicks = (await _unitOfWork.CustomEvents.GetAllEventPicksForQuinielaAsync(quinielaId)).ToList();
        var sportsPicks = (await _unitOfWork.Picks.GetAllPicksForQuinielaAsync(quinielaId)).ToList();

        foreach (var m in members)
        {
            var mEventPicks = allQuinielaEventPicks.Where(p => p.QuinielaMemberId == m.Id && p.IsHit.HasValue).ToList();
            var mSportsPicks = sportsPicks.Where(p => p.MemberId == m.Id && p.IsHit.HasValue).ToList();

            int eventHits = mEventPicks.Count(p => p.IsHit == true);
            int eventPoints = mEventPicks.Where(p => p.IsHit == true).Sum(p => p.PointsEarned);

            int sportsHits = mSportsPicks.Count(p => p.IsHit == true);
            int sportsPoints = sportsHits;

            m.TotalHits = eventHits + sportsHits;
            m.TotalPoints = eventPoints + sportsPoints;

            var (currentStreak, bestStreak) = _scoringEngine.CalculateMemberStreaks(mEventPicks);
            m.CurrentStreak = currentStreak;
            m.BestStreak = Math.Max(m.BestStreak, bestStreak);
            m.LastModified = DateTime.UtcNow;

            await _unitOfWork.QuinielaMembers.UpdateAsync(m);
        }

        await _unitOfWork.Save();

        var detailResponse = await GetRoundDetailAsync(quinielaId, roundId, userId);
        detailResponse.Message = "Ronda resuelta y puntos calculados exitosamente.";
        return detailResponse;
    }

    public async Task<Response<EventStandingsResponseDto>> GetStandingsAsync(int quinielaId, int? roundId)
    {
        var response = new Response<EventStandingsResponseDto>();

        var quiniela = await _unitOfWork.Quinielas.GetAsync(quinielaId);
        if (quiniela == null || !quiniela.IsActive)
        {
            response.isSuccess = false;
            response.Message = "La quiniela no existe o está inactiva.";
            return response;
        }

        var members = (await _unitOfWork.QuinielaMembers.GetMembersAsync(quinielaId)).ToList();
        var roundStandings = new List<EventStandingRowDto>();
        string? roundTitle = null;
        string? roundStatus = null;

        if (roundId.HasValue && roundId.Value > 0)
        {
            var round = await _unitOfWork.CustomEvents.GetRoundByIdWithDetailsAsync(roundId.Value);
            if (round != null && round.Active)
            {
                roundTitle = round.Title;
                roundStatus = round.Status;
                var roundPicks = (await _unitOfWork.CustomEvents.GetPicksByRoundIdAsync(round.Id)).ToList();
                roundStandings = _scoringEngine.CalculateRoundStandings(members, round.Questions, roundPicks);
            }
        }

        var allQuinielaEventPicks = (await _unitOfWork.CustomEvents.GetAllEventPicksForQuinielaAsync(quinielaId)).ToList();
        var allResolvedPicks = allQuinielaEventPicks.Where(p => p.IsHit.HasValue).ToList();

        var allRounds = (await _unitOfWork.CustomEvents.GetRoundsByQuinielaIdAsync(quinielaId)).ToList();
        int totalResolvedQuestions = allRounds
            .Where(r => r.Status == "Resolved")
            .SelectMany(r => r.Questions.Where(q => q.Active))
            .Count();

        var generalStandings = _scoringEngine.CalculateGeneralStandings(members, allResolvedPicks, totalResolvedQuestions);

        response.isSuccess = true;
        response.Message = "Clasificación de eventos consultada correctamente.";
        response.Data = new EventStandingsResponseDto
        {
            QuinielaId = quinielaId,
            RoundId = roundId,
            RoundTitle = roundTitle,
            RoundStatus = roundStatus,
            RoundStandings = roundStandings,
            GeneralStandings = generalStandings
        };

        return response;
    }
}
