using System.Globalization;
using System.Text;
using Common;
using DTO.Scoring;
using Interface.Persistence;
using Interface.UseCases;
using UseCases.Scoring;

namespace UseCases.Reports;

public class WhatsAppReportService : IWhatsAppReportService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ScoringEngine _scoringEngine;

    public WhatsAppReportService(IUnitOfWork unitOfWork, ScoringEngine scoringEngine)
    {
        _unitOfWork = unitOfWork;
        _scoringEngine = scoringEngine;
    }

    public async Task<Response<WhatsAppTextReportDto>> GenerateReminderReportAsync(int quinielaId, int weekId, string? appBaseUrl = null)
    {
        var response = new Response<WhatsAppTextReportDto>();

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

        var matches = (await _unitOfWork.Matches.GetByWeekIdAsync(weekId))
            .OrderBy(m => m.DateUtc)
            .ToList();

        if (!matches.Any())
        {
            response.isSuccess = false;
            response.Message = "No hay partidos en esta jornada.";
            return response;
        }

        var members = (await _unitOfWork.QuinielaMembers.GetMembersAsync(quinielaId)).ToList();
        var picks = (await _unitOfWork.Picks.GetAllPicksForWeekAsync(quinielaId, weekId)).ToList();

        // 1. Primer partido y cuenta regresiva
        var primerPartido = matches.FirstOrDefault(m => m.DateUtc != default);
        string cuentaRegresiva = "";
        string fechaLimite = "antes del primer partido";

        if (primerPartido != null)
        {
            var tzInfo = TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "Central Standard Time (Mexico)" : "America/Mexico_City");
            var localTime = TimeZoneInfo.ConvertTimeFromUtc(primerPartido.DateUtc, tzInfo);
            var ci = new CultureInfo("es-MX");
            fechaLimite = $"antes del {localTime.ToString("dddd d 'de' MMMM, hh:mm tt", ci)}";

            var now = DateTime.UtcNow;
            var diff = primerPartido.DateUtc - now;

            if (diff.TotalMinutes > 0)
            {
                int diffDias = (int)diff.TotalDays;
                int diffHoras = (int)diff.TotalHours;
                int diffMinutos = (int)diff.TotalMinutes;

                if (diffDias > 1)
                    cuentaRegresiva = $"⏳ *¡La jornada inicia en {diffDias} días!*\n\n";
                else if (diffDias == 1)
                    cuentaRegresiva = "⏳ *¡La jornada inicia en 1 día!*\n\n";
                else if (diffHoras > 1)
                    cuentaRegresiva = $"⏳ *¡La jornada inicia en {diffHoras} horas!*\n\n";
                else if (diffHoras == 1)
                    cuentaRegresiva = "⏳ *¡La jornada inicia en 1 hora!*\n\n";
                else
                    cuentaRegresiva = $"⏳ *¡La jornada inicia en {diffMinutos} minutos!*\n\n";
            }
        }

        // 2. Faltantes de picks
        var matchCount = matches.Count;
        var picksByMember = picks.GroupBy(p => p.MemberId).ToDictionary(g => g.Key, g => g.Count());

        var faltantes = members
            .Where(m => picksByMember.GetValueOrDefault(m.Id, 0) < matchCount)
            .Select(m => m.Alias)
            .ToList();

        var faltantesText = faltantes.Any()
            ? string.Join("\n", faltantes.Select(f => $"❌ {f}"))
            : "✅ ¡Todos los participantes han completado sus pronósticos!";

        var url = !string.IsNullOrWhiteSpace(appBaseUrl) ? appBaseUrl : "https://quiniela.orionsys.com";

        var sb = new StringBuilder();
        sb.AppendLine($"🚨 *¡ALERTA QUINIELA - JORNADA {week.WeekNumber}!* 🚨\n");
        if (!string.IsNullOrEmpty(cuentaRegresiva)) sb.Append(cuentaRegresiva);
        sb.AppendLine("Estamos a punto de arrancar la jornada y faltan de meter sus picks:\n");
        sb.AppendLine(faltantesText);
        sb.AppendLine($"\nRecuerden que si no la llenan {fechaLimite}, se autollenará al azar.\n");
        sb.AppendLine($"👉 *Link para llenar:* {url}");

        response.isSuccess = true;
        response.Message = "Alerta de WhatsApp generada.";
        response.Data = new WhatsAppTextReportDto
        {
            ReportType = "REMINDER",
            Title = $"Alerta Cierre - Jornada {week.WeekNumber}",
            Content = sb.ToString().TrimEnd()
        };

        return response;
    }

    public async Task<Response<WhatsAppTextReportDto>> GenerateSummaryReportAsync(int quinielaId, int weekId)
    {
        var response = new Response<WhatsAppTextReportDto>();

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

        var matches = (await _unitOfWork.Matches.GetByWeekIdAsync(weekId)).OrderBy(m => m.DateUtc).ToList();
        var members = (await _unitOfWork.QuinielaMembers.GetMembersAsync(quinielaId)).ToList();
        var weekPicks = (await _unitOfWork.Picks.GetAllPicksForWeekAsync(quinielaId, weekId)).ToList();

        var weeklyStandings = _scoringEngine.CalculateWeeklyStandings(members, weekPicks, matches);
        var finishedMatches = matches.Where(m => string.Equals(m.StatusState, "post", StringComparison.OrdinalIgnoreCase)).ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"🏆 *REPORTE OFICIAL QUINIELA - JORNADA {week.WeekNumber}* 🏆\n");

        sb.AppendLine("⚽ *RESUMEN DE MARCADORES*");
        foreach (var m in matches)
        {
            if (string.Equals(m.StatusState, "post", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(m.WinnerAbbr))
            {
                var desc = m.WinnerAbbr == "EMPATE" ? "(EMPATE)" : $"(Gana {m.WinnerAbbr})";
                var upset = m.IsUpset ? " 🚨" : "";
                sb.AppendLine($"▪️ {m.HomeTeam?.Name ?? "Local"} {m.HomeScore} - {m.AwayScore} {m.AwayTeam?.Name ?? "Visita"} {desc}{upset}");
            }
        }

        sb.AppendLine("\n📊 *TABLA DE POSICIONES*");
        for (int i = 0; i < weeklyStandings.Count; i++)
        {
            var p = weeklyStandings[i];
            int rank = i + 1;
            string medal = rank == 1 ? "🥇" : rank == 2 ? "🥈" : rank == 3 ? "🥉" : "🏅";

            string extra = "";
            if (p.UpsetHits > 0) extra += " 🔮";
            if (p.Humillaciones > 0) extra += " 🤡";

            sb.AppendLine($"{medal} *{rank}. {p.Alias}* - {p.Hits} aciertos ({p.AccuracyPct}%){extra}");
        }

        // Galardones
        string GetWinners(Func<MemberStandingDto, int> selector)
        {
            int max = weeklyStandings.Any() ? weeklyStandings.Max(selector) : 0;
            if (max > 0)
            {
                return string.Join(", ", weeklyStandings.Where(s => selector(s) == max).Select(s => s.Alias));
            }
            return "Nadie";
        }

        string mvp = weeklyStandings.Any() && weeklyStandings[0].Hits > 0 ? weeklyStandings[0].Alias : "Nadie";
        string reyesSorpresa = GetWinners(s => s.UpsetHits);
        string humillados = GetWinners(s => s.Humillaciones);
        string dormidos = GetWinners(s => s.Somniferos);
        string reyesEmpate = GetWinners(s => s.EmpatesFallidos);

        sb.AppendLine("\n🎭 *LOS GALARDONES DE LA SEMANA*");
        sb.AppendLine($"🏆 *MVP:* *{mvp}*");
        sb.AppendLine($"🔮 *Rey de las Sorpresas:* *{reyesSorpresa}*");
        sb.AppendLine($"🤡 *El Humillado:* *{humillados}*");
        sb.AppendLine($"😴 *Víctima del Somnífero:* *{dormidos}*");
        sb.AppendLine($"🤷‍♂️ *Rey del Empate Fallido:* *{reyesEmpate}*");
        sb.AppendLine("\n¡Felicidades a los que sumaron y suerte en la próxima jornada! ⚽🍻");

        response.isSuccess = true;
        response.Message = "Periódico del Lunes generado.";
        response.Data = new WhatsAppTextReportDto
        {
            ReportType = "SUMMARY",
            Title = $"Periódico del Lunes - Jornada {week.WeekNumber}",
            Content = sb.ToString().TrimEnd()
        };

        return response;
    }

    public async Task<Response<WhatsAppTextReportDto>> GeneratePrizePoolReportAsync(int quinielaId)
    {
        var response = new Response<WhatsAppTextReportDto>();

        var quiniela = await _unitOfWork.Quinielas.GetAsync(quinielaId);
        if (quiniela == null)
        {
            response.isSuccess = false;
            response.Message = "Quiniela no encontrada.";
            return response;
        }

        var members = (await _unitOfWork.QuinielaMembers.GetMembersAsync(quinielaId)).ToList();
        int totalJugadores = members.Count;
        decimal cuota = quiniela.EntryFee;
        decimal bolsaTotal = totalJugadores * cuota;

        decimal pct1 = quiniela.FirstPlacePct > 0 ? quiniela.FirstPlacePct : 70m;
        decimal pct2 = quiniela.SecondPlacePct > 0 ? quiniela.SecondPlacePct : 20m;
        decimal pct3 = quiniela.ThirdPlacePct > 0 ? quiniela.ThirdPlacePct : 10m;

        decimal premio1 = bolsaTotal * (pct1 / 100m);
        decimal premio2 = bolsaTotal * (pct2 / 100m);
        decimal premio3 = bolsaTotal * (pct3 / 100m);

        var sb = new StringBuilder();
        sb.AppendLine("💰 *ESTADO DE LA BOLSA (PRIZE POOL)* 💰\n");
        sb.AppendLine($"👥 Jugadores registrados: {totalJugadores}");
        sb.AppendLine($"💵 Cuota de entrada: ${cuota:N0} MXN");
        sb.AppendLine($"🏦 *BOLSA TOTAL: ${bolsaTotal:N0} MXN*\n");
        sb.AppendLine("🏆 *REPARTICIÓN DE PREMIOS (Al final del torneo):*");
        sb.AppendLine($"🥇 1er Lugar ({pct1}%): *${premio1:N0} MXN*");
        sb.AppendLine($"🥈 2do Lugar ({pct2}%): *${premio2:N0} MXN*");
        sb.AppendLine($"🥉 3er Lugar ({pct3}%): *${premio3:N0} MXN*\n");
        sb.AppendLine("*(Recuerden que para tener derecho a premio, la cuota debe estar cubierta).*");

        response.isSuccess = true;
        response.Message = "Reporte de Prize Pool generado.";
        response.Data = new WhatsAppTextReportDto
        {
            ReportType = "PRIZE_POOL",
            Title = "Bolsa de Premios (Prize Pool)",
            Content = sb.ToString().TrimEnd()
        };

        return response;
    }

    public async Task<Response<WhatsAppTextReportDto>> GeneratePlayerReportAsync(int quinielaId, int weekId, int memberId)
    {
        var response = new Response<WhatsAppTextReportDto>();

        var week = await _unitOfWork.Weeks.GetAsync(weekId);
        if (week == null)
        {
            response.isSuccess = false;
            response.Message = "Jornada no encontrada.";
            return response;
        }

        var member = await _unitOfWork.QuinielaMembers.GetAsync(memberId);
        if (member == null)
        {
            response.isSuccess = false;
            response.Message = "Participante no encontrado.";
            return response;
        }

        var matches = (await _unitOfWork.Matches.GetByWeekIdAsync(weekId)).OrderBy(m => m.DateUtc).ToList();
        var memberPicks = (await _unitOfWork.Picks.GetMemberPicksForWeekAsync(quinielaId, memberId, weekId)).ToList();
        var pickByGame = memberPicks.ToDictionary(p => p.MatchId, p => p.PickAbbr?.Trim().ToUpperInvariant() ?? "SIN PICK");

        var sb = new StringBuilder();
        sb.AppendLine($"📊 *Reporte Individual - Jornada {week.WeekNumber}*");
        sb.AppendLine($"👤 *Jugador:* {member.Alias}\n");

        int hits = 0;
        int finishedGames = 0;

        foreach (var m in matches)
        {
            var pick = pickByGame.GetValueOrDefault(m.Id, "SIN PICK");
            string status = m.StatusState.ToLowerInvariant();
            string winner = (m.WinnerAbbr ?? "").Trim().ToUpperInvariant();
            int hs = m.HomeScore;
            int as_ = m.AwayScore;

            string icon = "⏳";
            string matchResultText = "";

            if (status == "post")
            {
                finishedGames++;
                matchResultText = $"(Res: {winner} {hs}-{as_})";

                if (pick == winner)
                {
                    icon = "✅";
                    hits++;
                }
                else
                {
                    icon = "❌";
                }
            }
            else if (status == "in")
            {
                icon = "🔥";
                matchResultText = $"(En vivo: {hs}-{as_})";
            }

            var homeName = m.HomeTeam?.Name ?? "Local";
            var awayName = m.AwayTeam?.Name ?? "Visita";
            sb.AppendLine($"{icon} {homeName} vs {awayName}");
            sb.AppendLine($"   Tu pick: *{pick}* {matchResultText}");
        }

        decimal accuracy = finishedGames > 0 ? Math.Round((decimal)hits / finishedGames * 100m, 1) : 0m;
        sb.AppendLine($"\n🎯 *Aciertos:* {hits}/{finishedGames} ({accuracy}%)");

        response.isSuccess = true;
        response.Message = "Reporte individual generado.";
        response.Data = new WhatsAppTextReportDto
        {
            ReportType = "PLAYER",
            Title = $"Reporte Individual - {member.Alias}",
            Content = sb.ToString().TrimEnd()
        };

        return response;
    }
}
