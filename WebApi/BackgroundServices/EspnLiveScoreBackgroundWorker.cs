using Common;
using Domain.Entities;
using DTO.Notifications;
using Interface.Persistence;
using Interface.UseCases;

namespace WebApi.BackgroundServices;

/// <summary>
/// Worker en segundo plano para automatización del ciclo de vida de los partidos:
/// - Monitorea partidos en vivo desde ESPN con sondeo adaptativo (2 min activo / 15 min idle).
/// - Transición PUBLISHED -> LOCKED al arrancar el primer partido + Autollenado automático.
/// - Calificación inmediata y despacho de notificaciones Push (acierto vs fallo) al terminar cada partido.
/// - Transición LOCKED -> SCORED y asignación de galardones al concluir todos los partidos de la jornada.
/// </summary>
public class EspnLiveScoreBackgroundWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAppLogger<EspnLiveScoreBackgroundWorker> _logger;

    public TimeSpan NextSuggestedDelay { get; internal set; } = TimeSpan.FromMinutes(15);

    public EspnLiveScoreBackgroundWorker(
        IServiceScopeFactory scopeFactory,
        IAppLogger<EspnLiveScoreBackgroundWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[EspnLiveWorker] Iniciando servicio en segundo plano de monitoreo y Web Push.");

        // Pequeño retardo inicial para asegurar que la app y BD hayan completado su arranque
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var hasActiveLiveMatches = false;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var espnSync = scope.ServiceProvider.GetRequiredService<IEspnSyncService>();
                var webPush = scope.ServiceProvider.GetRequiredService<IWebPushNotificationService>();
                var scoringApp = scope.ServiceProvider.GetRequiredService<IScoringApplication>();

                hasActiveLiveMatches = await ProcessAutomationCycleAsync(unitOfWork, espnSync, webPush, scoringApp, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError("[EspnLiveWorker] Error durante el ciclo de automatización: {0}\n{1}", ex.Message, ex.StackTrace ?? "");
            }

            var nextDelay = hasActiveLiveMatches ? TimeSpan.FromMinutes(2) : NextSuggestedDelay;
            _logger.LogInformation("[EspnLiveWorker] Ciclo finalizado. Próximo sondeo en {0:N1} minutos.", nextDelay.TotalMinutes);

            await Task.Delay(nextDelay, stoppingToken);
        }

        _logger.LogInformation("[EspnLiveWorker] Deteniendo servicio de automatización.");
    }

    /// <summary>
    /// Ejecuta una iteración del ciclo de vida: bloqueo, sincronización inteligente, notificaciones y cierre de jornada.
    /// Retorna true si hay partidos activos en juego (para usar sondeo rápido de 2 min).
    /// </summary>
    public async Task<bool> ProcessAutomationCycleAsync(
        IUnitOfWork unitOfWork,
        IEspnSyncService espnSync,
        IWebPushNotificationService webPush,
        IScoringApplication scoringApp,
        CancellationToken ct)
    {
        var hasActiveMatches = false;
        var allPendingMatches = new List<Match>();
        var nowUtc = DateTime.UtcNow;

        // 1. Obtener temporadas activas
        var seasons = (await unitOfWork.Seasons.GetAllAsync()).Where(s => s.Active).ToList();
        if (seasons.Count == 0)
        {
            NextSuggestedDelay = TimeSpan.FromHours(4);
            return false;
        }

        foreach (var season in seasons)
        {
            var weeks = (await unitOfWork.Weeks.GetBySeasonIdAsync(season.Id))
                .Where(w => w.Active && (w.Status == "PUBLISHED" || w.Status == "LOCKED"))
                .ToList();

            foreach (var week in weeks)
            {
                try
                {
                    var matchesBefore = (await unitOfWork.Matches.GetByWeekIdAsync(week.Id)).ToList();

                    // A. Verificar arranque del 1er partido (PUBLISHED -> LOCKED + Autofill)
                    var firstMatchDate = matchesBefore.OrderBy(m => m.DateUtc).FirstOrDefault()?.DateUtc;
                    var effectiveFirstGameUtc = week.FirstGameUtc ?? firstMatchDate;

                    var shouldLock = week.Status == "PUBLISHED" && (
                        (effectiveFirstGameUtc.HasValue && nowUtc >= effectiveFirstGameUtc.Value) ||
                        matchesBefore.Any(m => string.Equals(m.StatusState, "in", StringComparison.OrdinalIgnoreCase) ||
                                               string.Equals(m.StatusState, "post", StringComparison.OrdinalIgnoreCase))
                    );

                    if (shouldLock)
                    {
                        await LockWeekAndAutofillAsync(week, season, unitOfWork, webPush, ct);
                    }

                    // B. Sincronización inteligente con ESPN:
                    // 1) Ventana activa de juego: partidos en vivo ("in") o que arrancan en <= 20 min o en curso (hasta 3.5h sin concluir)
                    var isMatchWindowActive = matchesBefore.Any(m =>
                        string.Equals(m.StatusState, "in", StringComparison.OrdinalIgnoreCase) ||
                        (!string.Equals(m.StatusState, "post", StringComparison.OrdinalIgnoreCase) &&
                         !string.Equals(m.StatusState, "postponed", StringComparison.OrdinalIgnoreCase) &&
                         nowUtc >= m.DateUtc.AddMinutes(-20) &&
                         nowUtc <= m.DateUtc.AddHours(3.5))
                    );

                    // 2) Sincronización de mantenimiento fuera de ventana: máximo 2-3 veces al día (cada 6+ horas)
                    var oldestSync = matchesBefore.Any() ? matchesBefore.Min(m => m.LastSyncUtc) : DateTime.MinValue;
                    var isMaintenanceSyncDue = (nowUtc - oldestSync) >= TimeSpan.FromHours(6);

                    var shouldSyncWithEspn = isMatchWindowActive || isMaintenanceSyncDue;

                    if (shouldSyncWithEspn)
                    {
                        _logger.LogInformation("[EspnLiveWorker] Sincronizando jornada {0} (ID={1}) desde ESPN (VentanaActiva={2}, Mantenimiento={3})...",
                            week.WeekNumber, week.Id, isMatchWindowActive, isMaintenanceSyncDue);
                        await espnSync.SyncWeekAsync(week.Id, ct);
                    }

                    // Trabajar con el estado más actualizado de los partidos
                    var matchesCurrent = shouldSyncWithEspn
                        ? (await unitOfWork.Matches.GetByWeekIdAsync(week.Id)).ToList()
                        : matchesBefore;

                    if (isMatchWindowActive)
                    {
                        hasActiveMatches = true;
                    }

                    // C. Detectar partidos concluidos (StatusState == "post") y notificar de forma idempotente vía logs persistentes
                    var finishedMatches = matchesCurrent.Where(m =>
                        string.Equals(m.StatusState, "post", StringComparison.OrdinalIgnoreCase)
                    ).ToList();

                    if (finishedMatches.Count > 0)
                    {
                        await NotifyFinishedMatchesAsync(finishedMatches, week, season, unitOfWork, webPush, ct);
                    }

                    // D. Verificar si la jornada concluyó por completo (todos los partidos en "post" o "postponed")
                    var allFinished = matchesCurrent.Count > 0 && matchesCurrent.All(m =>
                        string.Equals(m.StatusState, "post", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(m.StatusState, "postponed", StringComparison.OrdinalIgnoreCase)
                    );

                    if (allFinished && week.Status == "LOCKED")
                    {
                        await CloseAndScoreWeekAsync(week, season, unitOfWork, scoringApp, webPush, espnSync, ct);
                    }

                    // Coleccionar partidos pendientes para calcular el tiempo óptimo de sueño
                    allPendingMatches.AddRange(matchesCurrent.Where(m =>
                        !string.Equals(m.StatusState, "post", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(m.StatusState, "postponed", StringComparison.OrdinalIgnoreCase)
                    ));
                }
                catch (Exception weekEx)
                {
                    _logger.LogError("[EspnLiveWorker] Error procesando jornada {0} (ID={1}): {2}", week.WeekNumber, week.Id, weekEx.Message);
                }
            }
        }

        // 2. Calcular retardo óptimo para el siguiente ciclo cuando no hay partidos en juego
        if (allPendingMatches.Count > 0)
        {
            var nextMatch = allPendingMatches.OrderBy(m => m.DateUtc).First();
            var timeUntilKickoff = nextMatch.DateUtc - nowUtc;

            if (timeUntilKickoff <= TimeSpan.FromMinutes(20))
            {
                NextSuggestedDelay = TimeSpan.FromMinutes(2);
            }
            else if (timeUntilKickoff <= TimeSpan.FromHours(2))
            {
                var delay = timeUntilKickoff - TimeSpan.FromMinutes(20);
                NextSuggestedDelay = delay < TimeSpan.FromMinutes(2) ? TimeSpan.FromMinutes(2) : delay;
            }
            else
            {
                NextSuggestedDelay = TimeSpan.FromHours(2);
            }
        }
        else
        {
            NextSuggestedDelay = TimeSpan.FromHours(3);
        }

        return hasActiveMatches;
    }

    private async Task LockWeekAndAutofillAsync(
        Week week,
        Season season,
        IUnitOfWork unitOfWork,
        IWebPushNotificationService webPush,
        CancellationToken ct)
    {
        _logger.LogInformation("[EspnLiveWorker] Bloqueando jornada {0} (ID={1}) por inicio de partido.", week.WeekNumber, week.Id);
        week.Status = "LOCKED";
        week.LockedAt = DateTime.UtcNow;
        unitOfWork.Weeks.Update(week);
        await unitOfWork.Save(ct);

        // Quinielas asociadas a la temporada o liga
        var quinielas = (await unitOfWork.Quinielas.GetByLeagueIdAsync(season.LeagueId)).Where(q => q.IsActive).ToList();
        var matches = (await unitOfWork.Matches.GetByWeekIdAsync(week.Id)).ToList();
        var league = await unitOfWork.Leagues.GetAsync(season.LeagueId);
        var sport = league != null ? await unitOfWork.Sports.GetAsync(league.SportId) : null;
        bool allowsDraw = sport?.HasDraw ?? true;

        foreach (var quiniela in quinielas)
        {
            var members = (await unitOfWork.QuinielaMembers.GetMembersAsync(quiniela.Id)).ToList();
            var existingPicks = (await unitOfWork.Picks.GetAllPicksForWeekAsync(quiniela.Id, week.Id)).ToList();
            var existingLookup = existingPicks.Select(p => (p.MemberId, p.MatchId)).ToHashSet();

            var autofilledCount = 0;
            foreach (var member in members)
            {
                foreach (var match in matches)
                {
                    if (!existingLookup.Contains((member.Id, match.Id)))
                    {
                        var homeAbbr = match.HomeTeam?.Abbreviation ?? "LOC";
                        var awayAbbr = match.AwayTeam?.Abbreviation ?? "VIS";

                        string pickSelection;
                        if (allowsDraw)
                        {
                            var rnd = Random.Shared.Next(3);
                            pickSelection = rnd switch
                            {
                                0 => homeAbbr,
                                1 => awayAbbr,
                                _ => "EMPATE"
                            };
                        }
                        else
                        {
                            pickSelection = Random.Shared.Next(2) == 0 ? homeAbbr : awayAbbr;
                        }

                        await unitOfWork.Picks.InsertAsync(new Pick
                        {
                            QuinielaId = quiniela.Id,
                            MemberId = member.Id,
                            MatchId = match.Id,
                            PickAbbr = pickSelection,
                            IsAutoFilled = true,
                            Active = true,
                            Created = DateTime.UtcNow
                        });

                        autofilledCount++;
                    }
                }
            }

            if (autofilledCount > 0)
            {
                await unitOfWork.Save(ct);
                _logger.LogInformation("[EspnLiveWorker] Quiniela ID={0}: se generaron {1} pronósticos por autollenado.", quiniela.Id, autofilledCount);
            }

            // Notificación push a los miembros de la quiniela
            var payload = new PushNotificationPayload(
                Title: $"🔒 Jornada {week.WeekNumber} bloqueada",
                Message: "¡Ha comenzado el primer partido! Los pronósticos están bloqueados y han sido revelados.",
                Url: $"/fixtures?weekId={week.Id}",
                Data: new { type = "week_locked", weekId = week.Id, weekNumber = week.WeekNumber }
            );

            await webPush.SendNotificationToQuinielaAsync(quiniela.Id, payload, ct);
        }
    }

    private async Task NotifyFinishedMatchesAsync(
        List<Match> finishedMatches,
        Week week,
        Season season,
        IUnitOfWork unitOfWork,
        IWebPushNotificationService webPush,
        CancellationToken ct)
    {
        var quinielas = (await unitOfWork.Quinielas.GetByLeagueIdAsync(season.LeagueId)).Where(q => q.IsActive).ToList();
        var todayLocalStr = DateTime.UtcNow.AddHours(-6).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

        foreach (var match in finishedMatches)
        {
            var homeAbbr = match.HomeTeam?.Abbreviation ?? "LOC";
            var awayAbbr = match.AwayTeam?.Abbreviation ?? "VIS";
            var homeScore = match.HomeScore;
            var awayScore = match.AwayScore;
            var winnerAbbr = match.WinnerAbbr ?? (homeScore > awayScore ? homeAbbr : (awayScore > homeScore ? awayAbbr : "EMPATE"));

            var scoreDesc = $"{homeAbbr} {homeScore} - {awayScore} {awayAbbr}";

            foreach (var quiniela in quinielas)
            {
                // Idempotencia: Verificar si ya se notificó este partido para esta quiniela
                var alreadySent = await unitOfWork.PushNotificationLogs.HasMatchFinishedBeenSentAsync(match.Id, quiniela.Id, ct);
                if (alreadySent) continue;

                // Ventana de seguridad: Si el partido concluyó hace más de 12 horas, registrar log sin despachar push para evitar spam
                if (match.DateUtc < DateTime.UtcNow.AddHours(-12))
                {
                    await unitOfWork.PushNotificationLogs.InsertAsync(new PushNotificationLog
                    {
                        NotificationType = $"MATCH_FINISHED_{match.Id}",
                        WeekId = week.Id,
                        QuinielaId = quiniela.Id,
                        UserId = null,
                        DateLocal = todayLocalStr,
                        Title = $"Resultado en {quiniela.Name}: {scoreDesc}",
                        Message = $"Partido {homeAbbr} vs {awayAbbr} finalizado ({scoreDesc})",
                        DeliveredCount = 0,
                        SentAtUtc = DateTime.UtcNow,
                        Active = true
                    });
                    await unitOfWork.Save(ct);
                    continue;
                }

                var members = (await unitOfWork.QuinielaMembers.GetMembersAsync(quiniela.Id)).ToDictionary(m => m.Id);
                var picks = (await unitOfWork.Picks.GetAllPicksForWeekAsync(quiniela.Id, week.Id))
                    .Where(p => p.MatchId == match.Id)
                    .ToList();

                var deliveredTotal = 0;

                foreach (var pick in picks)
                {
                    if (!members.TryGetValue(pick.MemberId, out var member)) continue;

                    var isHit = string.Equals(pick.PickAbbr, winnerAbbr, StringComparison.OrdinalIgnoreCase);

                    // Formateo según SPEC-006 indicando claramente la quiniela y puntuación correcta:
                    // A quien acertó: "🎉 ¡Acertaste en {quiniela.Name}!" -> "¡Excelente pronóstico! América 2 - 1 Chivas."
                    // A quien falló:   "❌ Fallaste en {quiniela.Name}" -> "Ganó América: América 2 - 1 Chivas." o "Empate: ..."
                    string title;
                    string message;

                    if (isHit)
                    {
                        title = $"🎉 ¡Acertaste en {quiniela.Name}!";
                        message = $"¡Excelente pronóstico! {scoreDesc}.";
                    }
                    else
                    {
                        title = $"❌ Fallaste en {quiniela.Name}";
                        var winnerText = winnerAbbr == "EMPATE" ? "Empate" : $"Ganó {winnerAbbr}";
                        message = $"{winnerText}: {scoreDesc}.";
                    }

                    var payload = new PushNotificationPayload(
                        Title: title,
                        Message: message,
                        Url: $"/fixtures?weekId={week.Id}",
                        Data: new
                        {
                            type = "match_finished",
                            matchId = match.Id,
                            quinielaId = quiniela.Id,
                            isHit,
                            winner = winnerAbbr,
                            score = scoreDesc
                        }
                    );

                    var sent = await webPush.SendNotificationToUserAsync(member.UserId, payload, ct);
                    deliveredTotal += sent;
                }

                // Registrar en PushNotificationLogs para garantizar idempotencia persistente ante reinicios o syncs
                await unitOfWork.PushNotificationLogs.InsertAsync(new PushNotificationLog
                {
                    NotificationType = $"MATCH_FINISHED_{match.Id}",
                    WeekId = week.Id,
                    QuinielaId = quiniela.Id,
                    UserId = null,
                    DateLocal = todayLocalStr,
                    Title = $"Resultado en {quiniela.Name}: {scoreDesc}",
                    Message = $"Partido {homeAbbr} vs {awayAbbr} finalizado ({scoreDesc})",
                    DeliveredCount = deliveredTotal,
                    SentAtUtc = DateTime.UtcNow,
                    Active = true
                });

                await unitOfWork.Save(ct);

                _logger.LogInformation("[EspnLiveWorker] Notificaciones de partido finalizado enviadas para Match ID={0} ({1}) en Quiniela '{2}' ({3} entregados).",
                    match.Id, scoreDesc, quiniela.Name, deliveredTotal);
            }
        }
    }

    private async Task CloseAndScoreWeekAsync(
        Week week,
        Season season,
        IUnitOfWork unitOfWork,
        IScoringApplication scoringApp,
        IWebPushNotificationService webPush,
        IEspnSyncService espnSync,
        CancellationToken ct)
    {
        _logger.LogInformation("[EspnLiveWorker] Concluyendo y calificando jornada {0} (ID={1}).", week.WeekNumber, week.Id);

        var quinielas = (await unitOfWork.Quinielas.GetByLeagueIdAsync(season.LeagueId)).Where(q => q.IsActive).ToList();

        foreach (var quiniela in quinielas)
        {
            // Ejecutar el motor de puntuación y galardones
            var adminUserId = quiniela.OwnerId;
            var scoreResult = await scoringApp.ScoreWeekAsync(quiniela.Id, week.Id, adminUserId);

            if (scoreResult != null && scoreResult.isSuccess)
            {
                _logger.LogInformation("[EspnLiveWorker] Quiniela {0}: Calificación completada con éxito.", quiniela.Name);

                var payload = new PushNotificationPayload(
                    Title: $"🏆 ¡Jornada {week.WeekNumber} finalizada!",
                    Message: "Se han calculado los resultados, galardones y la tabla de posiciones.",
                    Url: "/standings",
                    Data: new { type = "week_scored", weekId = week.Id, weekNumber = week.WeekNumber }
                );

                await webPush.SendNotificationToQuinielaAsync(quiniela.Id, payload, ct);
            }
            else
            {
                _logger.LogWarning("[EspnLiveWorker] Quiniela {0}: No se pudo calificar automáticamente: {1}", quiniela.Name, scoreResult?.Message ?? "Error desconocido");
            }
        }

        week.Status = "SCORED";
        week.ScoredAt = DateTime.UtcNow;
        unitOfWork.Weeks.Update(week);
        await unitOfWork.Save(ct);

        // Auto-publicar la siguiente jornada si existe y está en DRAFT
        try
        {
            var nextWeekNumber = week.WeekNumber + 1;
            var seasonWeeks = (await unitOfWork.Weeks.GetBySeasonIdAsync(season.Id)).ToList();
            var nextWeek = seasonWeeks.FirstOrDefault(w => w.WeekNumber == nextWeekNumber);

            if (nextWeek != null && string.Equals(nextWeek.Status, "DRAFT", StringComparison.OrdinalIgnoreCase))
            {
                nextWeek.Status = "PUBLISHED";
                nextWeek.PublishedAt = DateTime.UtcNow;
                unitOfWork.Weeks.Update(nextWeek);
                await unitOfWork.Save(ct);
                _logger.LogInformation("[EspnLiveWorker] Siguiente jornada {0} (ID={1}) publicada automáticamente.", nextWeek.WeekNumber, nextWeek.Id);

                // Sincronizar partidos desde ESPN para la jornada recién publicada
                try
                {
                    await espnSync.SyncWeekAsync(nextWeek.Id, ct);
                }
                catch (Exception syncEx)
                {
                    _logger.LogWarning("[EspnLiveWorker] No se pudieron sincronizar partidos inmediatamente para la jornada {0}: {1}", nextWeek.WeekNumber, syncEx.Message);
                }

                // Notificar a las quinielas que la nueva jornada está disponible para pronósticos
                foreach (var quiniela in quinielas)
                {
                    var nextWeekPayload = new PushNotificationPayload(
                        Title: $"🟢 ¡Jornada {nextWeek.WeekNumber} disponible!",
                        Message: $"La jornada {nextWeek.WeekNumber} ya está abierta para ingresar pronósticos.",
                        Url: $"/fixtures?weekId={nextWeek.Id}",
                        Data: new { type = "week_published", weekId = nextWeek.Id, weekNumber = nextWeek.WeekNumber }
                    );

                    await webPush.SendNotificationToQuinielaAsync(quiniela.Id, nextWeekPayload, ct);
                }
            }
        }
        catch (Exception pubEx)
        {
            _logger.LogError("[EspnLiveWorker] Error al intentar auto-publicar la siguiente jornada: {0}", pubEx.Message);
        }
    }
}
