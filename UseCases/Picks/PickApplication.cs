using Common;
using Domain.Entities;
using DTO.Espn;
using DTO.Pick;
using Interface.Persistence;
using Interface.UseCases;
using Validator.Pick;

namespace UseCases.Picks;

public class PickApplication : IPickApplication
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly SubmitPickDtoValidator _submitValidator;

    public PickApplication(IUnitOfWork unitOfWork, SubmitPickDtoValidator submitValidator)
    {
        _unitOfWork = unitOfWork;
        _submitValidator = submitValidator;
    }

    public async Task<Response<PickDto>> SubmitPickAsync(int quinielaId, int userId, SubmitPickRequestDto request)
    {
        var response = new Response<PickDto>();

        // 1. Validación de entrada
        var validation = await _submitValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            response.isSuccess = false;
            response.Message = "Datos de pronóstico inválidos.";
            response.Errors = validation.Errors;
            return response;
        }

        // 2. Verificar membresía
        var membership = await _unitOfWork.QuinielaMembers.GetMembershipAsync(quinielaId, userId);
        if (membership == null)
        {
            response.isSuccess = false;
            response.Message = "No eres miembro de esta quiniela.";
            return response;
        }

        // 3. Verificar quiniela
        var quiniela = await _unitOfWork.Quinielas.GetAsync(quinielaId);
        if (quiniela == null || !quiniela.Active)
        {
            response.isSuccess = false;
            response.Message = "La quiniela no existe o no está activa.";
            return response;
        }

        // 4. Verificar partido
        var match = await _unitOfWork.Matches.GetAsync(request.MatchId);
        if (match == null)
        {
            response.isSuccess = false;
            response.Message = "El partido especificado no existe.";
            return response;
        }

        // 5. Verificar jornada y horario de bloqueo
        var week = await _unitOfWork.Weeks.GetAsync(match.WeekId);
        if (week == null)
        {
            response.isSuccess = false;
            response.Message = "La jornada asociada no existe.";
            return response;
        }

        // Regla: Jornada ya en LOCKED o SCORED
        if (week.Status == "LOCKED" || week.Status == "SCORED")
        {
            response.isSuccess = false;
            response.Message = "La jornada está bloqueada.";
            return response;
        }

        // Regla: Si la hora actual UTC alcanza o supera FirstGameUtc (o la hora del partido)
        var deadlineUtc = week.FirstGameUtc ?? match.DateUtc;
        if (DateTime.UtcNow >= deadlineUtc)
        {
            week.Status = "LOCKED";
            week.LockedAt = DateTime.UtcNow;
            _unitOfWork.Weeks.Update(week);
            await _unitOfWork.Save();

            response.isSuccess = false;
            response.Message = "La jornada está bloqueada.";
            return response;
        }

        // Regla: Partido individual ya en juego o finalizado
        if (DateTime.UtcNow >= match.DateUtc || match.StatusState != "pre")
        {
            response.isSuccess = false;
            response.Message = "El partido ya ha comenzado.";
            return response;
        }

        // 6. Validar opción de selección según deporte (HasDraw)
        var league = await _unitOfWork.Leagues.GetAsync(quiniela.LeagueId);
        var sport = league != null ? await _unitOfWork.Sports.GetAsync(league.SportId) : null;
        bool allowsDraw = sport?.HasDraw ?? true;

        var homeTeam = match.HomeTeam ?? await _unitOfWork.Teams.GetAsync(match.HomeTeamId);
        var awayTeam = match.AwayTeam ?? await _unitOfWork.Teams.GetAsync(match.AwayTeamId);

        var homeAbbr = homeTeam?.Abbreviation ?? string.Empty;
        var awayAbbr = awayTeam?.Abbreviation ?? string.Empty;

        bool isHome = string.Equals(request.PickAbbr, homeAbbr, StringComparison.OrdinalIgnoreCase);
        bool isAway = string.Equals(request.PickAbbr, awayAbbr, StringComparison.OrdinalIgnoreCase);
        bool isDraw = string.Equals(request.PickAbbr, "EMPATE", StringComparison.OrdinalIgnoreCase);

        if (!isHome && !isAway && (!isDraw || !allowsDraw))
        {
            response.isSuccess = false;
            response.Message = "La opción seleccionada no es válida para este partido.";
            return response;
        }

        string canonicalPick = isHome ? homeAbbr : (isAway ? awayAbbr : "EMPATE");

        // 7. Upsert Pick (Actualizar o Insertar)
        var existingPick = await _unitOfWork.Picks.GetPickAsync(quinielaId, membership.Id, request.MatchId);
        string auditAction;
        Pick savedPick;

        if (existingPick != null)
        {
            existingPick.PickAbbr = canonicalPick;
            existingPick.IsAutoFilled = false;
            existingPick.LastModified = DateTime.UtcNow;
            _unitOfWork.Picks.Update(existingPick);
            savedPick = existingPick;
            auditAction = "updated";
        }
        else
        {
            savedPick = new Pick
            {
                QuinielaId = quinielaId,
                MemberId = membership.Id,
                MatchId = request.MatchId,
                PickAbbr = canonicalPick,
                IsAutoFilled = false,
                Created = DateTime.UtcNow
            };
            await _unitOfWork.Picks.InsertAsync(savedPick);
            auditAction = "created";
        }

        // 8. Bitácora de auditoría inmutable
        var auditLog = new PickAuditLog
        {
            QuinielaId = quinielaId,
            WeekId = week.Id,
            MemberId = membership.Id,
            MatchId = request.MatchId,
            PickAbbr = canonicalPick,
            Source = "MANUAL",
            Action = auditAction,
            Timestamp = DateTime.UtcNow
        };
        await _unitOfWork.PickAuditLogs.InsertAsync(auditLog);

        await _unitOfWork.Save();

        response.isSuccess = true;
        response.Message = auditAction == "created" ? "Pronóstico guardado correctamente." : "Pronóstico actualizado correctamente.";
        response.Data = new PickDto
        {
            Id = savedPick.Id,
            QuinielaId = savedPick.QuinielaId,
            MemberId = savedPick.MemberId,
            MemberAlias = membership.Alias,
            MatchId = savedPick.MatchId,
            PickAbbr = savedPick.PickAbbr,
            IsAutoFilled = savedPick.IsAutoFilled,
            IsHit = savedPick.IsHit,
            IsUpsetHit = savedPick.IsUpsetHit,
            IsHumillacion = savedPick.IsHumillacion,
            IsSomnifero = savedPick.IsSomnifero,
            IsEmpateFallido = savedPick.IsEmpateFallido,
            Created = savedPick.Created,
            LastModified = savedPick.LastModified
        };

        return response;
    }

    public async Task<Response<QuinielaPicksResponseDto>> GetPicksAsync(int quinielaId, int weekId, int userId)
    {
        var response = new Response<QuinielaPicksResponseDto>();

        // 1. Validar membresía
        var membership = await _unitOfWork.QuinielaMembers.GetMembershipAsync(quinielaId, userId);
        if (membership == null)
        {
            response.isSuccess = false;
            response.Message = "No eres miembro de esta quiniela.";
            return response;
        }

        // 2. Obtener Quiniela y Jornada
        var quiniela = await _unitOfWork.Quinielas.GetAsync(quinielaId);
        if (quiniela == null)
        {
            response.isSuccess = false;
            response.Message = "La quiniela no existe.";
            return response;
        }

        var week = await _unitOfWork.Weeks.GetAsync(weekId);
        if (week == null)
        {
            response.isSuccess = false;
            response.Message = "La jornada no existe.";
            return response;
        }

        // 3. Obtener partidos de la jornada
        var matches = (await _unitOfWork.Matches.GetByWeekIdAsync(weekId)).ToList();

        // 4. Lazy Locking si expiró el tiempo del primer partido o si algún partido ya comenzó
        if (week.Status == "PUBLISHED")
        {
            var earliestMatch = matches.OrderBy(m => m.DateUtc).FirstOrDefault()?.DateUtc;
            var effectiveDeadline = week.FirstGameUtc ?? earliestMatch;
            var anyMatchStarted = matches.Any(m => m.StatusState == "in" || m.StatusState == "post");

            if ((effectiveDeadline.HasValue && DateTime.UtcNow >= effectiveDeadline.Value) || anyMatchStarted)
            {
                week.Status = "LOCKED";
                week.LockedAt = DateTime.UtcNow;
                if (!week.FirstGameUtc.HasValue && earliestMatch.HasValue)
                {
                    week.FirstGameUtc = earliestMatch;
                }
                _unitOfWork.Weeks.Update(week);
                await _unitOfWork.Save();
            }
        }

        bool isLocked = week.Status == "LOCKED" || week.Status == "SCORED";
        bool isRevealed = isLocked;
        var matchDtos = matches.Select(m => new MatchDto
        {
            Id = m.Id,
            WeekId = m.WeekId,
            EspnGameId = m.EspnGameId,
            DateUtc = DateTime.SpecifyKind(m.DateUtc, DateTimeKind.Utc),
            StatusState = m.StatusState,
            StatusDesc = m.StatusDesc,
            HomeScore = m.HomeScore,
            AwayScore = m.AwayScore,
            WinnerAbbr = m.WinnerAbbr,
            PostponedToDate = m.PostponedToDate.HasValue ? DateTime.SpecifyKind(m.PostponedToDate.Value, DateTimeKind.Utc) : null,
            Venue = m.Venue,
            City = m.City,
            LastSyncUtc = DateTime.SpecifyKind(m.LastSyncUtc, DateTimeKind.Utc),
            HomeTeam = new TeamDto
            {
                Id = m.HomeTeam.Id,
                EspnTeamId = m.HomeTeam.EspnTeamId,
                Name = m.HomeTeam.Name,
                Abbreviation = m.HomeTeam.Abbreviation,
                DisplayName = m.HomeTeam.DisplayName,
                LogoUrl = m.HomeTeam.LogoUrl,
                PrimaryColor = m.HomeTeam.PrimaryColor
            },
            AwayTeam = new TeamDto
            {
                Id = m.AwayTeam.Id,
                EspnTeamId = m.AwayTeam.EspnTeamId,
                Name = m.AwayTeam.Name,
                Abbreviation = m.AwayTeam.Abbreviation,
                DisplayName = m.AwayTeam.DisplayName,
                LogoUrl = m.AwayTeam.LogoUrl,
                PrimaryColor = m.AwayTeam.PrimaryColor
            }
        });

        // 5. Obtener miembros de la quiniela
        var members = (await _unitOfWork.QuinielaMembers.GetMembersAsync(quinielaId)).ToList();
        var memberDtos = members.Select(m => new QuinielaMemberPickDto
        {
            MemberId = m.Id,
            Alias = m.Alias,
            DisplayName = m.User?.DisplayName ?? m.Alias,
            Role = m.Role,
            TotalHits = m.TotalHits,
            CurrentStreak = m.CurrentStreak,
            AvatarUrl = m.User?.AvatarUrl
        });

        // 6. Obtener Picks (privacidad respetada: solo propios si está en PUBLISHED; todos si LOCKED/SCORED)
        IEnumerable<Pick> picks;
        if (isRevealed)
        {
            picks = await _unitOfWork.Picks.GetAllPicksForWeekAsync(quinielaId, weekId);
        }
        else
        {
            picks = await _unitOfWork.Picks.GetMemberPicksForWeekAsync(quinielaId, membership.Id, weekId);
        }

        var memberMap = members.ToDictionary(m => m.Id, m => m.Alias);

        var pickDtos = picks.Select(p => new PickDto
        {
            Id = p.Id,
            QuinielaId = p.QuinielaId,
            MemberId = p.MemberId,
            MemberAlias = memberMap.TryGetValue(p.MemberId, out var alias) ? alias : null,
            MatchId = p.MatchId,
            PickAbbr = p.PickAbbr,
            IsAutoFilled = p.IsAutoFilled,
            IsHit = p.IsHit,
            IsUpsetHit = p.IsUpsetHit,
            IsHumillacion = p.IsHumillacion,
            IsSomnifero = p.IsSomnifero,
            IsEmpateFallido = p.IsEmpateFallido,
            Created = p.Created,
            LastModified = p.LastModified
        });

        // 7. Deporte y Empate
        var league = await _unitOfWork.Leagues.GetAsync(quiniela.LeagueId);
        var sport = league != null ? await _unitOfWork.Sports.GetAsync(league.SportId) : null;
        bool allowsDraw = sport?.HasDraw ?? true;

        response.isSuccess = true;
        response.Message = "Pronósticos obtenidos con éxito.";
        response.Data = new QuinielaPicksResponseDto
        {
            QuinielaId = quinielaId,
            WeekId = week.Id,
            WeekNumber = week.WeekNumber,
            WeekName = week.Name,
            Status = week.Status,
            FirstGameUtc = week.FirstGameUtc.HasValue ? DateTime.SpecifyKind(week.FirstGameUtc.Value, DateTimeKind.Utc) : null,
            LockedAt = week.LockedAt.HasValue ? DateTime.SpecifyKind(week.LockedAt.Value, DateTimeKind.Utc) : null,
            IsLocked = isLocked,
            IsRevealed = isRevealed,
            AllowsDraw = allowsDraw,
            CurrentUserMemberId = membership.Id,
            Matches = matchDtos,
            Members = memberDtos,
            Picks = pickDtos
        };

        return response;
    }

    public async Task<Response<LockAndAutofillResultDto>> LockAndAutofillAsync(int quinielaId, int weekId, int userId)
    {
        var response = new Response<LockAndAutofillResultDto>();

        // 1. Validar permisos: Requiere ser OWNER/ADMIN de la quiniela o ADMIN del sistema
        var membership = await _unitOfWork.QuinielaMembers.GetMembershipAsync(quinielaId, userId);
        var user = await _unitOfWork.Users.GetAsync(userId);
        bool isGlobalAdmin = user?.Role == "ADMIN";
        bool isQuinielaAdmin = membership != null && (membership.Role == "OWNER" || membership.Role == "ADMIN");

        if (!isGlobalAdmin && !isQuinielaAdmin)
        {
            response.isSuccess = false;
            response.Message = "No tienes permisos de administrador para cerrar la jornada o autollenar.";
            return response;
        }

        // 2. Validar existencia de quiniela y jornada
        var quiniela = await _unitOfWork.Quinielas.GetAsync(quinielaId);
        if (quiniela == null)
        {
            response.isSuccess = false;
            response.Message = "La quiniela no existe.";
            return response;
        }

        var week = await _unitOfWork.Weeks.GetAsync(weekId);
        if (week == null)
        {
            response.isSuccess = false;
            response.Message = "La jornada no existe.";
            return response;
        }

        // 3. Forzar bloqueo si aún no estaba bloqueada
        if (week.Status != "LOCKED" && week.Status != "SCORED")
        {
            week.Status = "LOCKED";
            week.LockedAt = DateTime.UtcNow;
            _unitOfWork.Weeks.Update(week);
            await _unitOfWork.Save();
        }

        // 4. Reglas deportivas
        var league = await _unitOfWork.Leagues.GetAsync(quiniela.LeagueId);
        var sport = league != null ? await _unitOfWork.Sports.GetAsync(league.SportId) : null;
        bool allowsDraw = sport?.HasDraw ?? true;

        // 5. Obtener miembros activos y partidos de la jornada
        var members = (await _unitOfWork.QuinielaMembers.GetMembersAsync(quinielaId)).ToList();
        var matches = (await _unitOfWork.Matches.GetByWeekIdAsync(weekId)).ToList();

        if (!matches.Any() || !members.Any())
        {
            response.isSuccess = true;
            response.Message = "Jornada bloqueada. No hay partidos o miembros para autollenar.";
            response.Data = new LockAndAutofillResultDto
            {
                QuinielaId = quinielaId,
                WeekId = weekId,
                WeekStatus = week.Status,
                AutofilledPicksCount = 0,
                MembersAffectedCount = 0,
                Message = response.Message
            };
            return response;
        }

        // 6. Identificar picks faltantes existentes
        var existingPicks = (await _unitOfWork.Picks.GetAllPicksForWeekAsync(quinielaId, weekId)).ToList();
        var existingPicksLookup = existingPicks
            .Select(p => (p.MemberId, p.MatchId))
            .ToHashSet();

        var newAutoPicks = new List<Pick>();
        var auditLogs = new List<PickAuditLog>();
        var affectedMembers = new HashSet<int>();

        foreach (var member in members)
        {
            foreach (var match in matches)
            {
                if (!existingPicksLookup.Contains((member.Id, match.Id)))
                {
                    var homeAbbr = match.HomeTeam?.Abbreviation ?? "LOC";
                    var awayAbbr = match.AwayTeam?.Abbreviation ?? "VIS";

                    string randomPick;
                    if (allowsDraw)
                    {
                        int choice = Random.Shared.Next(3);
                        randomPick = choice switch
                        {
                            0 => homeAbbr,
                            1 => awayAbbr,
                            _ => "EMPATE"
                        };
                    }
                    else
                    {
                        randomPick = Random.Shared.Next(2) == 0 ? homeAbbr : awayAbbr;
                    }

                    newAutoPicks.Add(new Pick
                    {
                        QuinielaId = quinielaId,
                        MemberId = member.Id,
                        MatchId = match.Id,
                        PickAbbr = randomPick,
                        IsAutoFilled = true,
                        Created = DateTime.UtcNow
                    });

                    affectedMembers.Add(member.Id);

                    auditLogs.Add(new PickAuditLog
                    {
                        QuinielaId = quinielaId,
                        WeekId = weekId,
                        MemberId = member.Id,
                        MatchId = match.Id,
                        PickAbbr = randomPick,
                        Source = "AUTOFILL",
                        Action = "missing-autofilled",
                        Timestamp = DateTime.UtcNow
                    });
                }
            }
        }

        // 7. Inserción segura garantizando ON CONFLICT DO NOTHING (no sobreescribir picks humanos)
        int insertedCount = 0;
        if (newAutoPicks.Any())
        {
            insertedCount = await _unitOfWork.Picks.InsertMissingAutoFilledPicksAsync(newAutoPicks);

            // Registrar auditoría para cada pick autollenado insertado
            foreach (var log in auditLogs)
            {
                await _unitOfWork.PickAuditLogs.InsertAsync(log);
            }
            await _unitOfWork.Save();
        }

        response.isSuccess = true;
        response.Message = $"Jornada bloqueada exitosamente. Se autollenaron {insertedCount} pronósticos faltantes para {affectedMembers.Count} participantes.";
        response.Data = new LockAndAutofillResultDto
        {
            QuinielaId = quinielaId,
            WeekId = weekId,
            WeekStatus = week.Status,
            AutofilledPicksCount = insertedCount,
            MembersAffectedCount = affectedMembers.Count,
            Message = response.Message
        };

        return response;
    }
}
