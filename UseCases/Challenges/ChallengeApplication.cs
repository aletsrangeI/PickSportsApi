using System.Text.Json;
using Domain.Entities;
using DTO.Challenges;
using DTO.Notifications;
using DTO.Scoring;
using Interface.Persistence;
using Interface.UseCases;
using UseCases.Scoring;

namespace UseCases.Challenges;

public class ChallengeApplication : IChallengeApplication
{
    private static readonly HashSet<string> ValidChallengeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "WEEKLY_RANK",
        "GENERAL_RANK",
        "MATCH_WINNER",
        "EXACT_SCORE",
        "SUDDEN_DEATH",
        "TOTAL_GOALS"
    };

    private static readonly HashSet<string> ValidPayoutModes = new(StringComparer.OrdinalIgnoreCase)
    {
        "WINNER_TAKES_ALL",
        "LAST_PLACE_PAYS"
    };

    private readonly IUnitOfWork _unitOfWork;
    private readonly ChallengeScoringEngine _challengeScoringEngine;
    private readonly ScoringEngine _scoringEngine;
    private readonly IWebPushNotificationService? _webPushService;
    // Notificaciones Web Push activas para retos y duelos
    private readonly bool _enableChallengePushNotifications = true;

    public ChallengeApplication(
        IUnitOfWork unitOfWork,
        ChallengeScoringEngine? challengeScoringEngine = null,
        ScoringEngine? scoringEngine = null,
        IWebPushNotificationService? webPushService = null)
    {
        _unitOfWork = unitOfWork;
        _challengeScoringEngine = challengeScoringEngine ?? new ChallengeScoringEngine();
        _scoringEngine = scoringEngine ?? new ScoringEngine();
        _webPushService = webPushService;
    }

    public async Task<List<ChallengeDto>> GetChallengesByWeekAsync(
        int quinielaId,
        int weekId,
        int currentUserId,
        CancellationToken ct = default)
    {
        var week = await _unitOfWork.Weeks.GetAsync(weekId);
        if (week == null)
        {
            throw new KeyNotFoundException("La jornada especificada no existe.");
        }

        if (_unitOfWork.Challenges == null)
        {
            return new List<ChallengeDto>();
        }

        var matches = (await _unitOfWork.Matches.GetByWeekIdAsync(weekId))?.ToList() ?? new List<Match>();
        if (IsWeekLocked(week, matches))
        {
            await ExpireUnacceptedChallengesAsync(quinielaId, weekId, ct);
        }

        var challenges = await _unitOfWork.Challenges.GetChallengesByWeekAsync(quinielaId, weekId, ct)
                         ?? new List<QuinielaChallenge>();

        if (!challenges.Any())
        {
            return new List<ChallengeDto>();
        }

        var members = (await _unitOfWork.QuinielaMembers.GetMembersAsync(quinielaId))?.ToList() ?? new List<QuinielaMember>();
        var weekPicks = (await _unitOfWork.Picks.GetAllPicksForWeekAsync(quinielaId, weekId))?.ToList() ?? new List<Pick>();
        var allPicks = (await _unitOfWork.Picks.GetAllPicksForQuinielaAsync(quinielaId))?.ToList() ?? new List<Pick>();
        var awards = (await _unitOfWork.WeeklyAwards.GetAllAwardsForQuinielaAsync(quinielaId))?.ToList() ?? new List<WeeklyAward>();

        _scoringEngine.EvaluatePicksAndMatches(matches, weekPicks, members.Count);
        var weeklyStandings = _scoringEngine.CalculateWeeklyStandings(members, weekPicks, matches);
        var generalStandings = _scoringEngine.CalculateGeneralStandings(members, allPicks, week.SeasonId, week.WeekNumber, awards);

        var memberMap = members.ToDictionary(m => m.Id);
        var matchMap = matches.ToDictionary(m => m.Id);

        var result = new List<ChallengeDto>();
        foreach (var challenge in challenges.Where(c => c.Status != "CANCELLED"))
        {
            var dto = MapToChallengeDto(challenge, week, memberMap, matchMap, matches, weekPicks, weeklyStandings, generalStandings);
            result.Add(dto);
        }

        return result;
    }

    public async Task<List<ChallengeDto>> GetPendingInvitationsAsync(
        int quinielaId,
        int currentUserId,
        CancellationToken ct = default)
    {
        var membership = await _unitOfWork.QuinielaMembers.GetMembershipAsync(quinielaId, currentUserId);
        if (membership == null)
        {
            return new List<ChallengeDto>();
        }

        var pendingChallenges = await _unitOfWork.Challenges.GetPendingInvitationsByMemberAsync(quinielaId, membership.Id, ct)
                                ?? new List<QuinielaChallenge>();

        if (!pendingChallenges.Any())
        {
            return new List<ChallengeDto>();
        }

        var members = (await _unitOfWork.QuinielaMembers.GetMembersAsync(quinielaId))?.ToList() ?? new List<QuinielaMember>();
        var memberMap = members.ToDictionary(m => m.Id);
        var result = new List<ChallengeDto>();

        foreach (var challenge in pendingChallenges)
        {
            var week = challenge.Week ?? await _unitOfWork.Weeks.GetAsync(challenge.WeekId);
            if (week == null) continue;

            var matches = (await _unitOfWork.Matches.GetByWeekIdAsync(week.Id))?.ToList() ?? new List<Match>();
            if (IsWeekLocked(week, matches))
            {
                await ExpireUnacceptedChallengesAsync(quinielaId, week.Id, ct);
                continue;
            }

            var matchMap = matches.ToDictionary(m => m.Id);
            var weekPicks = (await _unitOfWork.Picks.GetAllPicksForWeekAsync(quinielaId, week.Id))?.ToList() ?? new List<Pick>();
            var dto = MapToChallengeDto(challenge, week, memberMap, matchMap, matches, weekPicks, null, null);
            result.Add(dto);
        }

        return result;
    }

    public async Task<ChallengeDto> CreateChallengeAsync(
        int quinielaId,
        int weekId,
        int currentUserId,
        CreateChallengeDto dto,
        CancellationToken ct = default)
    {
        if (dto == null)
        {
            throw new ArgumentNullException(nameof(dto));
        }

        var quiniela = await _unitOfWork.Quinielas.GetAsync(quinielaId);
        if (quiniela == null)
        {
            throw new KeyNotFoundException("La quiniela especificada no existe.");
        }

        var creator = await _unitOfWork.QuinielaMembers.GetMembershipAsync(quinielaId, currentUserId);
        if (creator == null)
        {
            throw new UnauthorizedAccessException("No eres miembro de esta quiniela.");
        }

        var week = await _unitOfWork.Weeks.GetAsync(weekId);
        if (week == null)
        {
            throw new KeyNotFoundException("La jornada especificada no existe.");
        }

        var matches = (await _unitOfWork.Matches.GetByWeekIdAsync(weekId))?.ToList() ?? new List<Match>();
        if (IsWeekLocked(week, matches))
        {
            await ExpireUnacceptedChallengesAsync(quinielaId, weekId, ct);
            throw new InvalidOperationException("La jornada está bloqueada. No se pueden lanzar nuevos retos después del cierre.");
        }

        var challengeType = (dto.ChallengeType ?? string.Empty).Trim().ToUpperInvariant();
        if (!ValidChallengeTypes.Contains(challengeType))
        {
            throw new ArgumentException($"El tipo de reto '{dto.ChallengeType}' no es válido.");
        }

        var payoutMode = string.IsNullOrWhiteSpace(dto.PayoutMode)
            ? "WINNER_TAKES_ALL"
            : dto.PayoutMode.Trim().ToUpperInvariant();

        if (!ValidPayoutModes.Contains(payoutMode))
        {
            throw new ArgumentException($"La modalidad de pago '{dto.PayoutMode}' no es válida.");
        }

        var stakes = (dto.StakesDescription ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(stakes) || stakes.Length > 200)
        {
            throw new ArgumentException("Debes especificar la apuesta del reto (máximo 200 caracteres).");
        }

        var invitedIds = (dto.InvitedMemberIds ?? new List<int>())
            .Distinct()
            .ToList();

        if (invitedIds.Contains(creator.Id))
        {
            throw new ArgumentException("No puedes invitarte a ti mismo al reto.");
        }

        if (invitedIds.Count == 0)
        {
            throw new ArgumentException("Debes invitar al menos a 1 participante al reto.");
        }

        // MATCH_WINNER: máximo 3 participantes en total (creador + hasta 2 invitados)
        if (challengeType == "MATCH_WINNER" && invitedIds.Count > 2)
        {
            throw new ArgumentException("Los retos de ganador de partido (MATCH_WINNER) permiten máximo 3 participantes en total.");
        }

        var members = (await _unitOfWork.QuinielaMembers.GetMembersAsync(quinielaId))?.ToList() ?? new List<QuinielaMember>();
        var memberMap = members.ToDictionary(m => m.Id);

        foreach (var invitedId in invitedIds)
        {
            if (!memberMap.ContainsKey(invitedId))
            {
                throw new ArgumentException($"El participante invitado (ID {invitedId}) no pertenece a esta quiniela.");
            }
        }

        // Validaciones específicas por tipo de reto para el creador
        string? creatorWinnerAbbr = null;
        int? creatorHomeScore = null;
        int? creatorAwayScore = null;
        int? creatorTotalGoals = null;

        if (challengeType == "MATCH_WINNER")
        {
            if (!dto.TargetMatchId.HasValue || !matches.Any(m => m.Id == dto.TargetMatchId.Value))
            {
                throw new ArgumentException("Debes seleccionar un partido válido de la jornada para este reto.");
            }

            if (string.IsNullOrWhiteSpace(dto.CreatorPredictedWinnerAbbr))
            {
                throw new ArgumentException("Debes elegir tu pronóstico para el partido seleccionado.");
            }

            creatorWinnerAbbr = dto.CreatorPredictedWinnerAbbr.Trim().ToUpperInvariant();
        }
        else if (challengeType == "EXACT_SCORE")
        {
            if (!dto.TargetMatchId.HasValue || !matches.Any(m => m.Id == dto.TargetMatchId.Value))
            {
                throw new ArgumentException("Debes seleccionar un partido válido de la jornada para este reto.");
            }

            if (!dto.CreatorPredictedHomeScore.HasValue || !dto.CreatorPredictedAwayScore.HasValue
                || dto.CreatorPredictedHomeScore.Value < 0 || dto.CreatorPredictedAwayScore.Value < 0)
            {
                throw new ArgumentException("Debes ingresar un marcador exacto válido (goles >= 0).");
            }

            creatorHomeScore = dto.CreatorPredictedHomeScore.Value;
            creatorAwayScore = dto.CreatorPredictedAwayScore.Value;
        }
        else if (challengeType == "TOTAL_GOALS")
        {
            if (!dto.CreatorPredictedTotalGoals.HasValue || dto.CreatorPredictedTotalGoals.Value < 0)
            {
                throw new ArgumentException("Debes ingresar una predicción válida para el total de goles de la jornada.");
            }

            creatorTotalGoals = dto.CreatorPredictedTotalGoals.Value;
        }

        var now = DateTime.UtcNow;
        var challenge = new QuinielaChallenge
        {
            QuinielaId = quinielaId,
            WeekId = weekId,
            CreatorMemberId = creator.Id,
            ChallengeType = challengeType,
            PayoutMode = payoutMode,
            StakesDescription = stakes,
            TargetMatchId = (challengeType is "MATCH_WINNER" or "EXACT_SCORE") ? dto.TargetMatchId : null,
            Status = "PENDING_ACCEPTANCE",
            CreatedAtUtc = now,
            Created = now,
            Active = true,
            Participants = new List<QuinielaChallengeParticipant>()
        };

        challenge.Participants.Add(new QuinielaChallengeParticipant
        {
            MemberId = creator.Id,
            Status = "ACCEPTED",
            PredictedWinnerAbbr = creatorWinnerAbbr,
            PredictedHomeScore = creatorHomeScore,
            PredictedAwayScore = creatorAwayScore,
            PredictedTotalGoals = creatorTotalGoals,
            Outcome = "NONE",
            RespondedAtUtc = now,
            Created = now,
            Active = true
        });

        foreach (var invitedId in invitedIds)
        {
            challenge.Participants.Add(new QuinielaChallengeParticipant
            {
                MemberId = invitedId,
                Status = "PENDING",
                Outcome = "NONE",
                Created = now,
                Active = true
            });
        }

        await _unitOfWork.Challenges.InsertAsync(challenge);
        await _unitOfWork.Save(ct);

        // Enviar notificación Web Push a los miembros invitados (desactivado en pre-lanzamiento)
        if (_webPushService != null && _enableChallengePushNotifications)
        {
            try
            {
                var invitedUserIds = invitedIds
                    .Where(id => memberMap.ContainsKey(id))
                    .Select(id => memberMap[id].UserId)
                    .Where(uId => uId > 0)
                    .Distinct()
                    .ToList();

                if (invitedUserIds.Any())
                {
                    var payload = new PushNotificationPayload(
                        Title: $"⚔️ ¡Nuevo Reto en {quiniela.Name}!",
                        Message: $"{creator.Alias} te lanzó un reto en la {week.Name}: \"{challenge.StakesDescription}\". Toca para aceptar o pasar antes del cierre.",
                        Url: $"/standings?tab=challenges&challengeId={challenge.Id}",
                        Data: new
                        {
                            type = "challenge_invited",
                            quinielaId,
                            weekId,
                            challengeId = challenge.Id
                        }
                    );

                    await _webPushService.SendNotificationToUsersAsync(invitedUserIds, payload, ct);
                }
            }
            catch
            {
                // Ignorar errores de Web Push para no revertir la creación del reto
            }
        }

        var matchMap = matches.ToDictionary(m => m.Id);
        var weekPicks = (await _unitOfWork.Picks.GetAllPicksForWeekAsync(quinielaId, weekId))?.ToList() ?? new List<Pick>();
        return MapToChallengeDto(challenge, week, memberMap, matchMap, matches, weekPicks, null, null);
    }

    public async Task<ChallengeDto> RespondChallengeAsync(
        int quinielaId,
        int challengeId,
        int currentUserId,
        RespondChallengeDto dto,
        CancellationToken ct = default)
    {
        if (dto == null)
        {
            throw new ArgumentNullException(nameof(dto));
        }

        var challenge = await _unitOfWork.Challenges.GetByIdWithParticipantsAsync(challengeId, ct)
                        ?? await _unitOfWork.Challenges.GetAsync(challengeId);

        if (challenge == null || challenge.QuinielaId != quinielaId)
        {
            throw new KeyNotFoundException("El reto especificado no existe.");
        }

        var membership = await _unitOfWork.QuinielaMembers.GetMembershipAsync(quinielaId, currentUserId);
        if (membership == null)
        {
            throw new UnauthorizedAccessException("No eres miembro de esta quiniela.");
        }

        var week = challenge.Week ?? await _unitOfWork.Weeks.GetAsync(challenge.WeekId);
        if (week == null)
        {
            throw new KeyNotFoundException("La jornada asociada al reto no existe.");
        }

        var matches = (await _unitOfWork.Matches.GetByWeekIdAsync(week.Id))?.ToList() ?? new List<Match>();
        if (IsWeekLocked(week, matches))
        {
            await ExpireUnacceptedChallengesAsync(quinielaId, week.Id, ct);
            throw new InvalidOperationException("La jornada está bloqueada. Ya no es posible responder al reto.");
        }

        if (challenge.Status is not ("PENDING_ACCEPTANCE" or "ACTIVE"))
        {
            throw new InvalidOperationException("Este reto ya no admite respuestas.");
        }

        var participant = challenge.Participants.FirstOrDefault(p => p.MemberId == membership.Id)
                          ?? await _unitOfWork.Challenges.GetParticipantAsync(challengeId, membership.Id, ct);

        if (participant == null)
        {
            throw new UnauthorizedAccessException("No tienes una invitación para este reto.");
        }

        if (!string.Equals(participant.Status, "PENDING", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Ya has respondido previamente a este reto.");
        }

        var action = (dto.Action ?? string.Empty).Trim().ToUpperInvariant();
        if (action is not ("ACCEPT" or "DECLINE"))
        {
            throw new ArgumentException("La acción debe ser 'ACCEPT' o 'DECLINE'.");
        }

        var now = DateTime.UtcNow;

        if (action == "DECLINE")
        {
            participant.Status = "DECLINED";
            participant.RespondedAtUtc = now;
            participant.LastModified = now;
        }
        else // ACCEPT
        {
            var acceptedParticipants = challenge.Participants
                .Where(p => p.Status == "ACCEPTED" && p.MemberId != membership.Id)
                .ToList();

            if (challenge.ChallengeType == "MATCH_WINNER")
            {
                if (acceptedParticipants.Count >= 3)
                {
                    throw new InvalidOperationException("Este reto ya alcanzó el máximo de 3 participantes.");
                }

                if (string.IsNullOrWhiteSpace(dto.PredictedWinnerAbbr))
                {
                    throw new ArgumentException("Debes elegir un pronóstico para aceptar este reto.");
                }

                var normalizedPick = dto.PredictedWinnerAbbr.Trim().ToUpperInvariant();
                bool isTaken = acceptedParticipants.Any(p =>
                    string.Equals(p.PredictedWinnerAbbr?.Trim(), normalizedPick, StringComparison.OrdinalIgnoreCase));

                if (isTaken)
                {
                    throw new InvalidOperationException($"La opción '{normalizedPick}' ya fue elegida por otro participante en este reto.");
                }

                participant.PredictedWinnerAbbr = normalizedPick;
            }
            else if (challenge.ChallengeType == "EXACT_SCORE")
            {
                if (!dto.PredictedHomeScore.HasValue || !dto.PredictedAwayScore.HasValue
                    || dto.PredictedHomeScore.Value < 0 || dto.PredictedAwayScore.Value < 0)
                {
                    throw new ArgumentException("Debes ingresar un marcador exacto válido para aceptar este reto.");
                }

                bool isTaken = acceptedParticipants.Any(p =>
                    p.PredictedHomeScore == dto.PredictedHomeScore.Value &&
                    p.PredictedAwayScore == dto.PredictedAwayScore.Value);

                if (isTaken)
                {
                    throw new InvalidOperationException($"El marcador {dto.PredictedHomeScore.Value}-{dto.PredictedAwayScore.Value} ya fue elegido por otro participante en este reto.");
                }

                participant.PredictedHomeScore = dto.PredictedHomeScore.Value;
                participant.PredictedAwayScore = dto.PredictedAwayScore.Value;
            }
            else if (challenge.ChallengeType == "TOTAL_GOALS")
            {
                if (!dto.PredictedTotalGoals.HasValue || dto.PredictedTotalGoals.Value < 0)
                {
                    throw new ArgumentException("Debes ingresar tu predicción de total de goles para aceptar este reto.");
                }

                bool isTaken = acceptedParticipants.Any(p =>
                    p.PredictedTotalGoals == dto.PredictedTotalGoals.Value);

                if (isTaken)
                {
                    throw new InvalidOperationException($"La predicción de {dto.PredictedTotalGoals.Value} goles ya fue elegida por otro participante en este reto.");
                }

                participant.PredictedTotalGoals = dto.PredictedTotalGoals.Value;
            }

            participant.Status = "ACCEPTED";
            participant.RespondedAtUtc = now;
            participant.LastModified = now;

            if (challenge.Participants.Count(p => p.Status == "ACCEPTED") >= 2)
            {
                challenge.Status = "ACTIVE";
                challenge.LastModified = now;
            }
        }

        await _unitOfWork.Challenges.UpdateAsync(challenge);
        await _unitOfWork.Save(ct);

        var members = (await _unitOfWork.QuinielaMembers.GetMembersAsync(quinielaId))?.ToList() ?? new List<QuinielaMember>();
        var memberMap = members.ToDictionary(m => m.Id);

        // Enviar notificación Web Push al creador del reto (desactivado en pre-lanzamiento)
        if (_webPushService != null && _enableChallengePushNotifications)
        {
            try
            {
                var creatorMember = challenge.CreatorMember ?? memberMap.GetValueOrDefault(challenge.CreatorMemberId);
                if (creatorMember != null && creatorMember.UserId > 0)
                {
                    var pushMessage = action == "ACCEPT"
                        ? $"🔥 {membership.Alias} aceptó tu reto para la {week.Name}."
                        : $"👀 {membership.Alias} prefirió pasar de tu reto para la {week.Name}.";

                    var payload = new PushNotificationPayload(
                        Title: action == "ACCEPT" ? "⚔️ ¡Reto Aceptado!" : "👀 Respuesta a tu Reto",
                        Message: pushMessage,
                        Url: $"/standings?tab=challenges&challengeId={challenge.Id}",
                        Data: new
                        {
                            type = action == "ACCEPT" ? "challenge_accepted" : "challenge_declined",
                            quinielaId,
                            weekId = week.Id,
                            challengeId = challenge.Id
                        }
                    );

                    await _webPushService.SendNotificationToUserAsync(creatorMember.UserId, payload, ct);
                }
            }
            catch
            {
                // Ignorar fallo de Web Push
            }
        }

        var matchMap = matches.ToDictionary(m => m.Id);
        var weekPicks = (await _unitOfWork.Picks.GetAllPicksForWeekAsync(quinielaId, week.Id))?.ToList() ?? new List<Pick>();
        return MapToChallengeDto(challenge, week, memberMap, matchMap, matches, weekPicks, null, null);
    }

    public async Task<ChallengeDto> SettleDebtAsync(
        int quinielaId,
        int challengeId,
        int targetMemberId,
        int currentUserId,
        bool isAdmin,
        CancellationToken ct = default)
    {
        var challenge = await _unitOfWork.Challenges.GetByIdWithParticipantsAsync(challengeId, ct)
                        ?? await _unitOfWork.Challenges.GetAsync(challengeId);

        if (challenge == null || challenge.QuinielaId != quinielaId)
        {
            throw new KeyNotFoundException("El reto especificado no existe.");
        }

        if (!string.Equals(challenge.Status, "RESOLVED_WITH_WINNER", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Solo se pueden confirmar pagos en retos finalizados con ganador.");
        }

        var currentMember = await _unitOfWork.QuinielaMembers.GetMembershipAsync(quinielaId, currentUserId);
        bool isQuinielaAdmin = isAdmin || (currentMember != null &&
            (string.Equals(currentMember.Role, "ADMIN", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(currentMember.Role, "OWNER", StringComparison.OrdinalIgnoreCase)));

        bool isChallengeWinner = currentMember != null &&
            challenge.Participants.Any(p => p.MemberId == currentMember.Id &&
                                            string.Equals(p.Outcome, "WINNER", StringComparison.OrdinalIgnoreCase));

        if (!isQuinielaAdmin && !isChallengeWinner)
        {
            throw new UnauthorizedAccessException("Solo el ganador del reto o un administrador puede confirmar el pago de la apuesta.");
        }

        var debtor = challenge.Participants.FirstOrDefault(p => p.MemberId == targetMemberId);
        if (debtor == null)
        {
            throw new KeyNotFoundException("El participante especificado no forma parte de este reto.");
        }

        if (!string.Equals(debtor.Outcome, "LOSER", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Solo se puede marcar como pagada la deuda de un participante perdedor.");
        }

        var now = DateTime.UtcNow;
        debtor.IsDebtSettled = true;
        debtor.SettledAtUtc = now;
        debtor.LastModified = now;

        await _unitOfWork.Challenges.UpdateAsync(challenge);
        await _unitOfWork.Save(ct);

        var week = challenge.Week ?? await _unitOfWork.Weeks.GetAsync(challenge.WeekId) ?? new Week { Id = challenge.WeekId };
        var matches = (await _unitOfWork.Matches.GetByWeekIdAsync(week.Id))?.ToList() ?? new List<Match>();
        var members = (await _unitOfWork.QuinielaMembers.GetMembersAsync(quinielaId))?.ToList() ?? new List<QuinielaMember>();
        var weekPicks = (await _unitOfWork.Picks.GetAllPicksForWeekAsync(quinielaId, week.Id))?.ToList() ?? new List<Pick>();

        return MapToChallengeDto(
            challenge,
            week,
            members.ToDictionary(m => m.Id),
            matches.ToDictionary(m => m.Id),
            matches,
            weekPicks,
            null,
            null);
    }

    public async Task<bool> CancelChallengeAsync(
        int quinielaId,
        int challengeId,
        int currentUserId,
        CancellationToken ct = default)
    {
        var challenge = await _unitOfWork.Challenges.GetByIdWithParticipantsAsync(challengeId, ct)
                        ?? await _unitOfWork.Challenges.GetAsync(challengeId);

        if (challenge == null || challenge.QuinielaId != quinielaId)
        {
            throw new KeyNotFoundException("El reto especificado no existe.");
        }

        var membership = await _unitOfWork.QuinielaMembers.GetMembershipAsync(quinielaId, currentUserId);
        if (membership == null)
        {
            throw new UnauthorizedAccessException("No eres miembro de esta quiniela.");
        }

        bool isCreator = challenge.CreatorMemberId == membership.Id;
        bool isQuinielaAdmin = string.Equals(membership.Role, "ADMIN", StringComparison.OrdinalIgnoreCase)
                               || string.Equals(membership.Role, "OWNER", StringComparison.OrdinalIgnoreCase);

        if (!isCreator && !isQuinielaAdmin)
        {
            throw new UnauthorizedAccessException("Solo el creador del reto o un administrador puede cancelarlo.");
        }

        var week = challenge.Week ?? await _unitOfWork.Weeks.GetAsync(challenge.WeekId);
        if (week != null)
        {
            var matches = (await _unitOfWork.Matches.GetByWeekIdAsync(week.Id))?.ToList() ?? new List<Match>();
            if (IsWeekLocked(week, matches))
            {
                await ExpireUnacceptedChallengesAsync(quinielaId, week.Id, ct);
                throw new InvalidOperationException("La jornada está bloqueada. No se puede cancelar un reto después del cierre.");
            }
        }

        if (!string.Equals(challenge.Status, "PENDING_ACCEPTANCE", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Solo se puede cancelar un reto mientras ningún invitado lo haya aceptado.");
        }

        challenge.Status = "CANCELLED";
        challenge.LastModified = DateTime.UtcNow;

        await _unitOfWork.Challenges.UpdateAsync(challenge);
        await _unitOfWork.Save(ct);

        return true;
    }

    public async Task ExpireUnacceptedChallengesAsync(
        int quinielaId,
        int weekId,
        CancellationToken ct = default)
    {
        if (_unitOfWork.Challenges == null)
        {
            return;
        }

        var challenges = await _unitOfWork.Challenges.GetActiveOrPendingChallengesByWeekAsync(quinielaId, weekId, ct);
        if (challenges == null || !challenges.Any())
        {
            var allWeekChallenges = await _unitOfWork.Challenges.GetChallengesByWeekAsync(quinielaId, weekId, ct);
            challenges = allWeekChallenges?
                .Where(c => c.Status is "PENDING_ACCEPTANCE" or "ACTIVE")
                .ToList() ?? new List<QuinielaChallenge>();
        }

        if (!challenges.Any())
        {
            return;
        }

        var now = DateTime.UtcNow;
        bool anyModified = false;

        foreach (var challenge in challenges)
        {
            var acceptedCount = challenge.Participants.Count(p => p.Status == "ACCEPTED");
            var pendingParticipants = challenge.Participants.Where(p => p.Status == "PENDING").ToList();

            foreach (var pending in pendingParticipants)
            {
                pending.Status = "EXPIRED";
                pending.RespondedAtUtc = now;
                pending.LastModified = now;
                anyModified = true;
            }

            if (challenge.Status == "PENDING_ACCEPTANCE" || acceptedCount < 2)
            {
                challenge.Status = "UNACCEPTED_EXPIRED";
                challenge.ResolutionSummary = "El reto expiró porque ningún invitado aceptó antes del cierre de la jornada.";
                challenge.LastModified = now;
                anyModified = true;
            }

            if (anyModified)
            {
                await _unitOfWork.Challenges.UpdateAsync(challenge);
            }
        }

        if (anyModified)
        {
            await _unitOfWork.Save(ct);
        }
    }

    public async Task ResolveWeekChallengesAsync(
        int quinielaId,
        int weekId,
        CancellationToken ct = default)
    {
        if (_unitOfWork.Challenges == null)
        {
            return;
        }

        await ExpireUnacceptedChallengesAsync(quinielaId, weekId, ct);

        var challenges = await _unitOfWork.Challenges.GetActiveOrPendingChallengesByWeekAsync(quinielaId, weekId, ct);
        if (challenges == null || !challenges.Any())
        {
            var allWeekChallenges = await _unitOfWork.Challenges.GetChallengesByWeekAsync(quinielaId, weekId, ct);
            challenges = allWeekChallenges?
                .Where(c => c.Status is "ACTIVE" or "RESOLVED_WITH_WINNER" or "RESOLVED_NO_WINNER")
                .ToList() ?? new List<QuinielaChallenge>();
        }

        var resolvable = challenges
            .Where(c => c.Status is "ACTIVE" or "RESOLVED_WITH_WINNER" or "RESOLVED_NO_WINNER")
            .ToList();

        if (!resolvable.Any())
        {
            return;
        }

        var week = await _unitOfWork.Weeks.GetAsync(weekId);
        var members = (await _unitOfWork.QuinielaMembers.GetMembersAsync(quinielaId))?.ToList() ?? new List<QuinielaMember>();
        var matches = (await _unitOfWork.Matches.GetByWeekIdAsync(weekId))?.ToList() ?? new List<Match>();
        var weekPicks = (await _unitOfWork.Picks.GetAllPicksForWeekAsync(quinielaId, weekId))?.ToList() ?? new List<Pick>();
        var allPicks = (await _unitOfWork.Picks.GetAllPicksForQuinielaAsync(quinielaId))?.ToList() ?? new List<Pick>();
        var awards = (await _unitOfWork.WeeklyAwards.GetAllAwardsForQuinielaAsync(quinielaId))?.ToList() ?? new List<WeeklyAward>();

        _scoringEngine.EvaluatePicksAndMatches(matches, weekPicks, members.Count);
        var weeklyStandings = _scoringEngine.CalculateWeeklyStandings(members, weekPicks, matches);
        var generalStandings = week != null
            ? _scoringEngine.CalculateGeneralStandings(members, allPicks, week.SeasonId, week.WeekNumber, awards)
            : new List<MemberStandingDto>();

        var memberMap = members.ToDictionary(m => m.Id);
        var now = DateTime.UtcNow;

        foreach (var challenge in resolvable)
        {
            foreach (var p in challenge.Participants)
            {
                if (p.Member == null && memberMap.TryGetValue(p.MemberId, out var m))
                {
                    p.Member = m;
                }
            }

            var resolution = _challengeScoringEngine.ResolveChallenge(
                challenge,
                matches,
                weekPicks,
                weeklyStandings,
                generalStandings);

            challenge.Status = resolution.FinalStatus;
            challenge.ResolutionSummary = resolution.ResolutionSummary;
            challenge.DoubleLifeEventsJson = resolution.DoubleLifeEvents.Any()
                ? JsonSerializer.Serialize(resolution.DoubleLifeEvents)
                : null;
            challenge.ResolvedAtUtc = now;
            challenge.LastModified = now;

            foreach (var p in challenge.Participants)
            {
                if (resolution.ParticipantOutcomes.TryGetValue(p.MemberId, out var outcome))
                {
                    p.Outcome = outcome;
                }

                if (resolution.EliminatedAtMatchIds.TryGetValue(p.MemberId, out var elimMatchId))
                {
                    p.EliminatedAtMatchId = elimMatchId;
                }

                p.LastModified = now;
            }

            await _unitOfWork.Challenges.UpdateAsync(challenge);
        }

        await _unitOfWork.Save(ct);
    }

    private static bool IsWeekLocked(Week week, List<Match> matches)
    {
        if (!string.Equals(week.Status, "PUBLISHED", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var earliestMatchUtc = matches
            .Where(m => m.DateUtc != default)
            .OrderBy(m => m.DateUtc)
            .Select(m => (DateTime?)m.DateUtc)
            .FirstOrDefault();

        var firstMatchDateUtc = week.FirstGameUtc ?? earliestMatchUtc;
        if (firstMatchDateUtc.HasValue && DateTime.UtcNow >= firstMatchDateUtc.Value)
        {
            return true;
        }

        if (matches.Any(m => string.Equals(m.StatusState, "in", StringComparison.OrdinalIgnoreCase)
                             || string.Equals(m.StatusState, "post", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }

    private ChallengeDto MapToChallengeDto(
        QuinielaChallenge challenge,
        Week week,
        Dictionary<int, QuinielaMember> memberMap,
        Dictionary<int, Match> matchMap,
        List<Match> matches,
        List<Pick> weekPicks,
        List<MemberStandingDto>? weeklyStandings,
        List<MemberStandingDto>? generalStandings)
    {
        var creatorMember = challenge.CreatorMember ?? memberMap.GetValueOrDefault(challenge.CreatorMemberId);
        Match? targetMatch = challenge.TargetMatch;
        if (targetMatch == null && challenge.TargetMatchId.HasValue)
        {
            matchMap.TryGetValue(challenge.TargetMatchId.Value, out targetMatch);
        }

        var persistedDoubleLife = new List<string>();
        if (!string.IsNullOrWhiteSpace(challenge.DoubleLifeEventsJson))
        {
            try
            {
                persistedDoubleLife = JsonSerializer.Deserialize<List<string>>(challenge.DoubleLifeEventsJson) ?? new List<string>();
            }
            catch
            {
                persistedDoubleLife = new List<string>();
            }
        }

        var participantDtos = challenge.Participants
            .Select(p =>
            {
                var member = p.Member ?? memberMap.GetValueOrDefault(p.MemberId);
                Match? elimMatch = p.EliminatedAtMatch;
                if (elimMatch == null && p.EliminatedAtMatchId.HasValue)
                {
                    matchMap.TryGetValue(p.EliminatedAtMatchId.Value, out elimMatch);
                }

                return new ChallengeParticipantDto
                {
                    Id = p.Id,
                    ChallengeId = challenge.Id,
                    MemberId = p.MemberId,
                    Alias = member?.Alias ?? $"Jugador {p.MemberId}",
                    DisplayName = member?.User?.DisplayName ?? member?.Alias ?? $"Jugador {p.MemberId}",
                    AvatarUrl = member?.User?.AvatarUrl,
                    IsCreator = p.MemberId == challenge.CreatorMemberId,
                    Status = p.Status,
                    PredictedWinnerAbbr = p.PredictedWinnerAbbr,
                    PredictedHomeScore = p.PredictedHomeScore,
                    PredictedAwayScore = p.PredictedAwayScore,
                    PredictedTotalGoals = p.PredictedTotalGoals,
                    Outcome = p.Outcome,
                    EliminatedAtMatchId = p.EliminatedAtMatchId,
                    EliminatedAtMatchLabel = elimMatch != null
                        ? $"{elimMatch.HomeTeam?.Abbreviation ?? "LOC"} vs {elimMatch.AwayTeam?.Abbreviation ?? "VIS"}"
                        : null,
                    IsDebtSettled = p.IsDebtSettled,
                    SettledAtUtc = p.SettledAtUtc,
                    RespondedAtUtc = p.RespondedAtUtc
                };
            })
            .OrderByDescending(p => p.IsCreator)
            .ThenBy(p => p.Id)
            .ToList();

        var dto = new ChallengeDto
        {
            Id = challenge.Id,
            QuinielaId = challenge.QuinielaId,
            WeekId = challenge.WeekId,
            WeekName = week.Name,
            CreatorMemberId = challenge.CreatorMemberId,
            CreatorAlias = creatorMember?.Alias ?? $"Jugador {challenge.CreatorMemberId}",
            CreatorAvatarUrl = creatorMember?.User?.AvatarUrl,
            ChallengeType = challenge.ChallengeType,
            PayoutMode = challenge.PayoutMode,
            StakesDescription = challenge.StakesDescription,
            TargetMatchId = challenge.TargetMatchId,
            TargetMatchLabel = targetMatch != null
                ? $"{targetMatch.HomeTeam?.Abbreviation ?? "LOC"} vs {targetMatch.AwayTeam?.Abbreviation ?? "VIS"}"
                : null,
            TargetMatchScore = targetMatch != null && !string.Equals(targetMatch.StatusState, "pre", StringComparison.OrdinalIgnoreCase)
                ? $"{targetMatch.HomeScore} - {targetMatch.AwayScore}"
                : null,
            Status = challenge.Status,
            ResolutionSummary = challenge.ResolutionSummary,
            DoubleLifeEvents = persistedDoubleLife,
            CreatedAtUtc = challenge.CreatedAtUtc,
            ResolvedAtUtc = challenge.ResolvedAtUtc,
            Participants = participantDtos
        };

        _challengeScoringEngine.EvaluateLiveState(dto, matches, weekPicks, weeklyStandings, generalStandings);

        if (!dto.DoubleLifeEvents.Any() && persistedDoubleLife.Any())
        {
            dto.DoubleLifeEvents = persistedDoubleLife;
        }

        return dto;
    }
}
