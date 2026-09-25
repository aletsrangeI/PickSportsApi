using System.Globalization;
using Common;
using Domain.Entities;
using DTO.Notifications;
using Interface.Persistence;
using Interface.UseCases;

namespace UseCases.Notifications;

public class QuinielaReminderApplication : IQuinielaReminderApplication
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IWebPushNotificationService _webPushService;
    private readonly IAppLogger<QuinielaReminderApplication> _logger;

    public QuinielaReminderApplication(
        IUnitOfWork unitOfWork,
        IWebPushNotificationService webPushService,
        IAppLogger<QuinielaReminderApplication> logger)
    {
        _unitOfWork = unitOfWork;
        _webPushService = webPushService;
        _logger = logger;
    }

    public static TimeZoneInfo GetMexicoTimeZone()
    {
        try
        {
            var tzId = OperatingSystem.IsWindows() ? "Central Standard Time (Mexico)" : "America/Mexico_City";
            return TimeZoneInfo.FindSystemTimeZoneById(tzId);
        }
        catch
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");
            }
            catch
            {
                return TimeZoneInfo.CreateCustomTimeZone("Mexico_CST", TimeSpan.FromHours(-6), "Mexico CST", "Mexico CST");
            }
        }
    }

    public async Task<int> ProcessDailyRemindersAsync(DateTime? simulatedNowUtc = null, CancellationToken ct = default)
    {
        var nowUtc = simulatedNowUtc ?? DateTime.UtcNow;
        var tz = GetMexicoTimeZone();
        var localTime = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);
        var todayDateStr = localTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var dayOfWeek = localTime.DayOfWeek;
        var timeOfDay = localTime.TimeOfDay;

        _logger.LogInformation("[QuinielaReminder] Iniciando ciclo de recordatorios para {0} (Local: {1:yyyy-MM-dd HH:mm:ss} {2}).",
            todayDateStr, localTime, dayOfWeek);

        var totalNotificationsSent = 0;

        // 1. Obtener ligas y quinielas activas
        var quinielas = (await _unitOfWork.Quinielas.GetAllAsync()).Where(q => q.IsActive).ToList();
        if (!quinielas.Any())
        {
            _logger.LogInformation("[QuinielaReminder] No hay quinielas activas para procesar.");
            return 0;
        }

        var seasons = (await _unitOfWork.Seasons.GetAllAsync()).Where(s => s.Active).ToList();
        var seasonsDict = seasons.ToDictionary(s => s.Id);

        foreach (var quiniela in quinielas)
        {
            // Obtener semanas asociadas a la liga de la quiniela
            var leagueSeasons = seasons.Where(s => s.LeagueId == quiniela.LeagueId).Select(s => s.Id).ToList();
            if (!leagueSeasons.Any()) continue;

            foreach (var seasonId in leagueSeasons)
            {
                var weeks = (await _unitOfWork.Weeks.GetBySeasonIdAsync(seasonId)).Where(w => w.Active).ToList();

                // -------------------------------------------------------------
                // REGLA 1: LUNES (12:00 PM CST) - Apertura de Jornada Siguiente
                // -------------------------------------------------------------
                if (dayOfWeek == DayOfWeek.Monday && timeOfDay >= new TimeSpan(12, 0, 0))
                {
                    totalNotificationsSent += await ProcessMondayOpeningAsync(quiniela, weeks, todayDateStr, nowUtc, ct);
                }

                // -----------------------------------------------------------------------------------
                // REGLA 2: MARTES A VIERNES (11:00 AM CST) - Recordatorio a Rezagados con Picks Pendientes
                // -----------------------------------------------------------------------------------
                var isWeekdayReminder = dayOfWeek is DayOfWeek.Tuesday or DayOfWeek.Wednesday or DayOfWeek.Thursday or DayOfWeek.Friday;
                if (isWeekdayReminder && timeOfDay >= new TimeSpan(11, 0, 0))
                {
                    totalNotificationsSent += await ProcessIncompletePicksReminderAsync(quiniela, weeks, todayDateStr, nowUtc, ct);
                }

                // -------------------------------------------------------------------------
                // REGLA 3: DÍAS DE PARTIDO (10:30 AM CST) - Cartelera Matutina de Partidos
                // -------------------------------------------------------------------------
                if (timeOfDay >= new TimeSpan(10, 30, 0))
                {
                    totalNotificationsSent += await ProcessMatchdayMorningRoundupAsync(quiniela, weeks, tz, todayDateStr, nowUtc, ct);
                }
            }
        }

        if (totalNotificationsSent > 0)
        {
            await _unitOfWork.Save(ct);
            _logger.LogInformation("[QuinielaReminder] Ciclo completado: {0} notificaciones registradas/despachadas.", totalNotificationsSent);
        }

        return totalNotificationsSent;
    }

    private async Task<int> ProcessMondayOpeningAsync(
        Quiniela quiniela,
        List<Week> weeks,
        string todayDateStr,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var sentCount = 0;
        // La semana disponible para pronósticos es aquella en estado PUBLISHED
        var publishedWeeks = weeks.Where(w => w.Status == "PUBLISHED").OrderBy(w => w.WeekNumber).ToList();

        foreach (var week in publishedWeeks)
        {
            var alreadySent = await _unitOfWork.PushNotificationLogs.HasWeekOpenedBeenSentAsync(week.Id, quiniela.Id, ct);
            if (alreadySent) continue;

            var payload = new PushNotificationPayload(
                Title: $"📋 ¡Jornada {week.WeekNumber} disponible!",
                Message: $"Ya puedes ingresar tus pronósticos para la Jornada {week.WeekNumber} en {quiniela.Name}. ¡Haz tus picks con tiempo!",
                Url: $"/fixtures?weekId={week.Id}",
                Data: new { type = "week_opened", weekId = week.Id, weekNumber = week.WeekNumber, quinielaId = quiniela.Id }
            );

            var delivered = await _webPushService.SendNotificationToQuinielaAsync(quiniela.Id, payload, ct);

            await _unitOfWork.PushNotificationLogs.InsertAsync(new PushNotificationLog
            {
                NotificationType = "WEEK_OPENED",
                WeekId = week.Id,
                QuinielaId = quiniela.Id,
                UserId = null,
                DateLocal = todayDateStr,
                Title = payload.Title,
                Message = payload.Message,
                DeliveredCount = delivered,
                SentAtUtc = nowUtc,
                Active = true
            });

            _logger.LogInformation("[QuinielaReminder] Notificación de apertura enviada para Jornada {0} en Quiniela '{1}' ({2} entregados).",
                week.WeekNumber, quiniela.Name, delivered);

            sentCount++;
        }

        return sentCount;
    }

    private async Task<int> ProcessIncompletePicksReminderAsync(
        Quiniela quiniela,
        List<Week> weeks,
        string todayDateStr,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var sentCount = 0;
        // Solo aplica a semanas PUBLISHED que aún no han arrancado
        var publishedWeeks = weeks
            .Where(w => w.Status == "PUBLISHED" && (!w.FirstGameUtc.HasValue || nowUtc < w.FirstGameUtc.Value))
            .OrderBy(w => w.WeekNumber)
            .ToList();

        foreach (var week in publishedWeeks)
        {
            var matches = (await _unitOfWork.Matches.GetByWeekIdAsync(week.Id)).ToList();
            if (matches.Count == 0) continue;

            var members = (await _unitOfWork.QuinielaMembers.GetMembersAsync(quiniela.Id)).Where(m => m.Active).ToList();
            var allPicks = (await _unitOfWork.Picks.GetAllPicksForWeekAsync(quiniela.Id, week.Id)).ToList();
            var picksByMember = allPicks.GroupBy(p => p.MemberId).ToDictionary(g => g.Key, g => g.Count());

            foreach (var member in members)
            {
                var userPicks = picksByMember.GetValueOrDefault(member.Id, 0);
                if (userPicks >= matches.Count)
                {
                    // Ya llenó todos sus picks, no lo molestamos
                    continue;
                }

                var alreadySentToday = await _unitOfWork.PushNotificationLogs.HasNotificationBeenSentTodayAsync(
                    "INCOMPLETE_PICKS_REMINDER",
                    week.Id,
                    quiniela.Id,
                    member.UserId,
                    todayDateStr,
                    ct);

                if (alreadySentToday) continue;

                var missingCount = matches.Count - userPicks;
                var missingDesc = userPicks == 0 ? "todos tus pronósticos" : $"{missingCount} pronóstico(s)";

                var payload = new PushNotificationPayload(
                    Title: $"⏰ ¡Completa tu quiniela!",
                    Message: $"Tienes {missingDesc} pendientes para la Jornada {week.WeekNumber} ({userPicks}/{matches.Count}) en {quiniela.Name}. ¡No dejes que se autollenen al azar!",
                    Url: $"/fixtures?weekId={week.Id}",
                    Data: new
                    {
                        type = "incomplete_picks_reminder",
                        weekId = week.Id,
                        weekNumber = week.WeekNumber,
                        quinielaId = quiniela.Id,
                        userPicks,
                        totalMatches = matches.Count
                    }
                );

                var delivered = await _webPushService.SendNotificationToUserAsync(member.UserId, payload, ct);

                await _unitOfWork.PushNotificationLogs.InsertAsync(new PushNotificationLog
                {
                    NotificationType = "INCOMPLETE_PICKS_REMINDER",
                    WeekId = week.Id,
                    QuinielaId = quiniela.Id,
                    UserId = member.UserId,
                    DateLocal = todayDateStr,
                    Title = payload.Title,
                    Message = payload.Message,
                    DeliveredCount = delivered,
                    SentAtUtc = nowUtc,
                    Active = true
                });

                _logger.LogInformation("[QuinielaReminder] Recordatorio de picks incompletos enviado a Usuario ID={0} ({1}/{2} picks) en Quiniela '{3}'.",
                    member.UserId, userPicks, matches.Count, quiniela.Name);

                sentCount++;
            }
        }

        return sentCount;
    }

    private async Task<int> ProcessMatchdayMorningRoundupAsync(
        Quiniela quiniela,
        List<Week> weeks,
        TimeZoneInfo tz,
        string todayDateStr,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var sentCount = 0;
        // Aplica a jornadas abiertas (PUBLISHED) o en juego (LOCKED)
        var activeWeeks = weeks.Where(w => w.Status is "PUBLISHED" or "LOCKED").ToList();

        foreach (var week in activeWeeks)
        {
            var matches = (await _unitOfWork.Matches.GetByWeekIdAsync(week.Id)).ToList();
            if (!matches.Any()) continue;

            // Filtrar partidos que se juegan HOY en hora de México
            var matchesToday = matches.Where(m =>
            {
                var matchLocal = TimeZoneInfo.ConvertTimeFromUtc(m.DateUtc, tz);
                return matchLocal.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) == todayDateStr;
            }).OrderBy(m => m.DateUtc).ToList();

            if (!matchesToday.Any()) continue;

            // Verificar si ya se envió hoy el resumen matutino para esta jornada y quiniela
            var alreadySentToday = await _unitOfWork.PushNotificationLogs.HasNotificationBeenSentTodayAsync(
                "MATCHDAY_MORNING_ROUNDUP",
                week.Id,
                quiniela.Id,
                null,
                todayDateStr,
                ct);

            if (alreadySentToday) continue;

            // Construir lista amigable de partidos
            var ci = new CultureInfo("es-MX");
            var matchSummaries = matchesToday.Select(m =>
            {
                var home = m.HomeTeam?.Abbreviation ?? "LOC";
                var away = m.AwayTeam?.Abbreviation ?? "VIS";
                var matchLocal = TimeZoneInfo.ConvertTimeFromUtc(m.DateUtc, tz);
                return $"{home} vs {away} ({matchLocal.ToString("HH:mm", ci)})";
            }).ToList();

            var summaryText = string.Join(", ", matchSummaries.Take(3));
            if (matchesToday.Count > 3)
            {
                summaryText += $" y {matchesToday.Count - 3} más";
            }

            string title;
            string message;

            if (week.Status == "PUBLISHED")
            {
                title = $"⚽ ¡Hoy arranca la Jornada {week.WeekNumber}!";
                message = $"Partidos de hoy: {summaryText}. Recuerda que tus picks se bloquean al silbatazo del primer juego.";
            }
            else
            {
                title = $"⚽ ¡Partidos de hoy en la Quiniela!";
                message = $"{summaryText}. ¡Sigue los marcadores en vivo y suma puntos en la Jornada {week.WeekNumber}!";
            }

            var payload = new PushNotificationPayload(
                Title: title,
                Message: message,
                Url: $"/fixtures?weekId={week.Id}",
                Data: new
                {
                    type = "matchday_morning_roundup",
                    weekId = week.Id,
                    weekNumber = week.WeekNumber,
                    quinielaId = quiniela.Id,
                    matchCount = matchesToday.Count
                }
            );

            var delivered = await _webPushService.SendNotificationToQuinielaAsync(quiniela.Id, payload, ct);

            await _unitOfWork.PushNotificationLogs.InsertAsync(new PushNotificationLog
            {
                NotificationType = "MATCHDAY_MORNING_ROUNDUP",
                WeekId = week.Id,
                QuinielaId = quiniela.Id,
                UserId = null,
                DateLocal = todayDateStr,
                Title = payload.Title,
                Message = payload.Message,
                DeliveredCount = delivered,
                SentAtUtc = nowUtc,
                Active = true
            });

            _logger.LogInformation("[QuinielaReminder] Cartelera matutina de partidos enviada para Jornada {0} en Quiniela '{1}' ({2} entregados).",
                week.WeekNumber, quiniela.Name, delivered);

            sentCount++;
        }

        return sentCount;
    }
}
