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

            var nextDelay = hasActiveLiveMatches ? TimeSpan.FromMinutes(2) : TimeSpan.FromMinutes(15);
            _logger.LogInformation("[EspnLiveWorker] Ciclo finalizado. Próximo sondeo en {0} minutos.", nextDelay.TotalMinutes);

            await Task.Delay(nextDelay, stoppingToken);
        }

        _logger.LogInformation("[EspnLiveWorker] Deteniendo servicio de automatización.");
    }

    /// <summary>
    /// Ejecuta una iteración del ciclo de vida: bloqueo, sincronización, notificaciones y cierre de jornada.
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

        // 1. Obtener temporadas activas
        var seasons = (await unitOfWork.Seasons.GetAllAsync()).Where(s => s.Active).ToList();
        if (seasons.Count == 0) return false;

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
                    var matchesBeforeDict = matchesBefore.ToDictionary(m => m.Id);

                    // A. Verificar arranque del 1er partido (PUBLISHED -> LOCKED + Autofill)
                    var firstMatchDate = matchesBefore.OrderBy(m => m.DateUtc).FirstOrDefault()?.DateUtc;
                    var effectiveFirstGameUtc = week.FirstGameUtc ?? firstMatchDate;

                    var shouldLock = week.Status == "PUBLISHED" && (
                        (effectiveFirstGameUtc.HasValue && DateTime.UtcNow >= effectiveFirstGameUtc.Value) ||
                        matchesBefore.Any(m => string.Equals(m.StatusState, "in", StringComparison.OrdinalIgnoreCase) ||
                                               string.Equals(m.StatusState, "post", StringComparison.OrdinalIgnoreCase))
                    );

                    if (shouldLock)
                    {
                        await LockWeekAndAutofillAsync(week, season, unitOfWork, webPush, ct);
                    }

                    // B. Si la jornada está bloqueada o hay partidos en vivo hoy, sincronizar ESPN
                    var hasMatchesToday = matchesBefore.Any(m => m.DateUtc.Date == DateTime.UtcNow.Date ||
                                                                 string.Equals(m.StatusState, "in", StringComparison.OrdinalIgnoreCase));

                    if (week.Status == "LOCKED" || hasMatchesToday)
                    {
                        _logger.LogInformation("[EspnLiveWorker] Sincronizando jornada {0} (ID={1}) desde ESPN...", week.WeekNumber, week.Id);
                        await espnSync.SyncWeekAsync(week.Id, ct);

                        // Re-leer partidos después del sync
                        var matchesAfter = (await unitOfWork.Matches.GetByWeekIdAsync(week.Id)).ToList();

                        // Detectar si hay partidos actualmente en juego o en ventana de juego activa
                        var hasLiveOrImminent = matchesAfter.Any(m =>
                            string.Equals(m.StatusState, "in", StringComparison.OrdinalIgnoreCase) ||
                            (string.Equals(m.StatusState, "pre", StringComparison.OrdinalIgnoreCase) &&
                             DateTime.UtcNow >= m.DateUtc.AddMinutes(-20) &&
                             DateTime.UtcNow <= m.DateUtc.AddHours(3))
                        );

                        if (!hasLiveOrImminent && week.Status == "LOCKED")
                        {
                            var hasPendingSoon = matchesAfter.Any(m =>
                                !string.Equals(m.StatusState, "post", StringComparison.OrdinalIgnoreCase) &&
                                !string.Equals(m.StatusState, "postponed", StringComparison.OrdinalIgnoreCase) &&
                                (m.DateUtc.Date == DateTime.UtcNow.Date || m.DateUtc <= DateTime.UtcNow.AddHours(4))
                            );
                            if (hasPendingSoon)
                            {
                                hasLiveOrImminent = true;
                            }
                        }

                        if (hasLiveOrImminent)
                        {
                            hasActiveMatches = true;
                        }

                        // C. Detectar partidos que recién finalizaron (StatusState cambió a "post")
                        var newlyFinished = matchesAfter.Where(m =>
                            string.Equals(m.StatusState, "post", StringComparison.OrdinalIgnoreCase) &&
                            (!matchesBeforeDict.TryGetValue(m.Id, out var before) ||
                             !string.Equals(before.StatusState, "post", StringComparison.OrdinalIgnoreCase))
                        ).ToList();

                        if (newlyFinished.Count > 0)
                        {
                            _logger.LogInformation("[EspnLiveWorker] Se detectaron {0} partidos recién concluidos en jornada {1}.", newlyFinished.Count, week.WeekNumber);
                            await NotifyFinishedMatchesAsync(newlyFinished, week, season, unitOfWork, webPush, ct);
                        }

                        // D. Verificar si la jornada concluyó por completo (todos los partidos en "post" o "postponed")
                        var allFinished = matchesAfter.Count > 0 && matchesAfter.All(m =>
                            string.Equals(m.StatusState, "post", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(m.StatusState, "postponed", StringComparison.OrdinalIgnoreCase)
                        );

                        if (allFinished && week.Status == "LOCKED")
                        {
                            await CloseAndScoreWeekAsync(week, season, unitOfWork, scoringApp, webPush, ct);
                        }
                    }
                }
                catch (Exception weekEx)
                {
                    _logger.LogError("[EspnLiveWorker] Error procesando jornada {0} (ID={1}): {2}", week.WeekNumber, week.Id, weekEx.Message);
                }
            }
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
        List<Match> newlyFinishedMatches,
        Week week,
        Season season,
        IUnitOfWork unitOfWork,
        IWebPushNotificationService webPush,
        CancellationToken ct)
    {
        var quinielas = (await unitOfWork.Quinielas.GetByLeagueIdAsync(season.LeagueId)).Where(q => q.IsActive).ToList();

        foreach (var match in newlyFinishedMatches)
        {
            var homeAbbr = match.HomeTeam?.Abbreviation ?? "LOC";
            var awayAbbr = match.AwayTeam?.Abbreviation ?? "VIS";
            var homeScore = match.HomeScore;
            var awayScore = match.AwayScore;
            var winnerAbbr = match.WinnerAbbr ?? (homeScore > awayScore ? homeAbbr : (awayScore > homeScore ? awayAbbr : "EMPATE"));

            var scoreDesc = $"{homeAbbr} {homeScore} - {awayScore} {awayAbbr}";

            foreach (var quiniela in quinielas)
            {
                var members = (await unitOfWork.QuinielaMembers.GetMembersAsync(quiniela.Id)).ToDictionary(m => m.Id);
                var picks = (await unitOfWork.Picks.GetAllPicksForWeekAsync(quiniela.Id, week.Id))
                    .Where(p => p.MatchId == match.Id)
                    .ToList();

                foreach (var pick in picks)
                {
                    if (!members.TryGetValue(pick.MemberId, out var member)) continue;

                    var isHit = string.Equals(pick.PickAbbr, winnerAbbr, StringComparison.OrdinalIgnoreCase);

                    // Formateo según Escenario 2 de SPEC-006:
                    // A quien acertó: "🎉 ¡Acertaste tu pick! América 2 - 1 Chivas."
                    // A quien falló:   "❌ Fallaste tu pick. Ganó América 2 - 1." (o marcador)
                    string title;
                    string message;

                    if (isHit)
                    {
                        title = "🎉 ¡Acertaste tu pick!";
                        message = $"¡Excelente pronóstico! {scoreDesc}.";
                    }
                    else
                    {
                        title = "❌ Fallaste tu pick";
                        var winnerText = winnerAbbr == "EMPATE" ? "Empate" : $"Ganó {winnerAbbr}";
                        message = $"{winnerText} {scoreDesc}.";
                    }

                    var payload = new PushNotificationPayload(
                        Title: title,
                        Message: message,
                        Url: $"/fixtures?weekId={week.Id}",
                        Data: new
                        {
                            type = "match_finished",
                            matchId = match.Id,
                            isHit,
                            winner = winnerAbbr,
                            score = scoreDesc
                        }
                    );

                    await webPush.SendNotificationToUserAsync(member.UserId, payload, ct);
                }
            }
        }
    }

    private async Task CloseAndScoreWeekAsync(
        Week week,
        Season season,
        IUnitOfWork unitOfWork,
        IScoringApplication scoringApp,
        IWebPushNotificationService webPush,
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
                _logger.LogWarning("[EspnLiveWorker] Quiniela {0}: No se pudo calificar automáticamente: {1}", quiniela.Name, scoreResult.Message);
            }
        }

        week.Status = "SCORED";
        week.ScoredAt = DateTime.UtcNow;
        unitOfWork.Weeks.Update(week);
        await unitOfWork.Save(ct);
    }
}
