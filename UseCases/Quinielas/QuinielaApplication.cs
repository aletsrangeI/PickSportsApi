using System.Security.Cryptography;
using Common;
using Domain.Entities;
using Domain.Enums;
using DTO.League;
using DTO.Quiniela;
using Interface.Mapping;
using Interface.Persistence;
using Interface.UseCases;
using UseCases.Scoring;
using Validator.Quiniela;

namespace UseCases.Quinielas;

public class QuinielaApplication : IQuinielaApplication
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAppMapper _mapper;
    private readonly CreateQuinielaDtoValidator _createValidator;
    private readonly JoinQuinielaDtoValidator _joinValidator;

    public QuinielaApplication(
        IUnitOfWork unitOfWork,
        IAppMapper mapper,
        CreateQuinielaDtoValidator createValidator,
        JoinQuinielaDtoValidator joinValidator)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _createValidator = createValidator;
        _joinValidator = joinValidator;
    }

    public async Task<Response<QuinielaResponseDto>> CreateAsync(int userId, CreateQuinielaDto request)
    {
        var response = new Response<QuinielaResponseDto>();
        var validation = await _createValidator.ValidateAsync(request);

        if (!validation.IsValid)
        {
            response.isSuccess = false;
            response.Message = "Errores de validación al crear la quiniela.";
            response.Errors = validation.Errors;
            return response;
        }

        var user = await _unitOfWork.Users.GetAsync(userId);
        if (user == null || !user.Active)
        {
            response.isSuccess = false;
            response.Message = "Usuario no autorizado o inexistente.";
            return response;
        }

        bool isCustomEvents = string.Equals(request.QuinielaType, "CustomEvents", StringComparison.OrdinalIgnoreCase);
        string inviteCode;
        int? leagueId = null;
        int? seasonId = null;

        if (isCustomEvents)
        {
            inviteCode = await GenerateUniqueInviteCodeAsync("EV");
        }
        else
        {
            if (!request.LeagueId.HasValue || request.LeagueId.Value <= 0)
            {
                response.isSuccess = false;
                response.Message = "Debe seleccionar una liga válida.";
                return response;
            }

            var league = await _unitOfWork.Leagues.GetAsync(request.LeagueId.Value);
            if (league == null || !league.Active)
            {
                response.isSuccess = false;
                response.Message = "La liga seleccionada no existe o no está activa.";
                return response;
            }

            leagueId = league.Id;
            seasonId = (await _unitOfWork.Seasons.GetCurrentSeasonAsync(league.Id))?.Id;
            inviteCode = await GenerateUniqueInviteCodeAsync(league.Code);
        }

        var quiniela = new Quiniela
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            QuinielaType = isCustomEvents ? QuinielaType.CustomEvents : QuinielaType.Sports,
            LeagueId = leagueId,
            SeasonId = seasonId,
            OwnerId = userId,
            InviteCode = inviteCode,
            EntryFee = request.EntryFee,
            FirstPlacePct = request.FirstPlacePct,
            SecondPlacePct = request.SecondPlacePct,
            ThirdPlacePct = request.ThirdPlacePct,
            IsActive = true,
            Active = true,
            Created = DateTime.UtcNow
        };

        var created = await _unitOfWork.Quinielas.InsertAsync(quiniela);
        if (!created)
        {
            response.isSuccess = false;
            response.Message = "Error al crear la quiniela en la base de datos.";
            return response;
        }

        if (isCustomEvents)
        {
            await _unitOfWork.CustomEvents.GetOrCreateEventGroupForQuinielaAsync(quiniela.Id, quiniela.Name);
        }

        // Agregar al creador automáticamente como miembro con rol OWNER
        var ownerMember = new QuinielaMember
        {
            QuinielaId = quiniela.Id,
            UserId = userId,
            Alias = user.DisplayName ?? user.Username,
            Role = "OWNER",
            PaidFee = true,
            JoinedAt = DateTime.UtcNow,
            Active = true,
            Created = DateTime.UtcNow
        };

        await _unitOfWork.QuinielaMembers.InsertAsync(ownerMember);

        // Obtener quiniela con detalles para respuesta
        var createdQuiniela = await _unitOfWork.Quinielas.GetWithDetailsAsync(quiniela.Id);
        var resultDto = _mapper.Map<QuinielaResponseDto>(createdQuiniela ?? quiniela);
        resultDto.UserRole = "OWNER";
        resultDto.MembersCount = 1;

        response.isSuccess = true;
        response.Message = "Quiniela creada exitosamente.";
        response.Data = resultDto;
        return response;
    }

    public async Task<Response<QuinielaResponseDto>> StartNextSeasonAsync(int quinielaId, int userId, StartNextSeasonDto request)
    {
        var response = new Response<QuinielaResponseDto>();
        request ??= new StartNextSeasonDto();

        Response<QuinielaResponseDto> Fail(string message)
        {
            response.isSuccess = false;
            response.Message = message;
            return response;
        }

        // 1. La quiniela debe existir
        var quiniela = await _unitOfWork.Quinielas.GetAsync(quinielaId);
        if (quiniela == null) return Fail("La quiniela no existe.");

        // 2. Eventos personalizados no tienen temporadas
        if (quiniela.QuinielaType == QuinielaType.CustomEvents)
            return Fail("Las quinielas de eventos personalizados no tienen temporadas.");

        // 3. Permiso: OWNER/ADMIN de la quiniela o ADMIN del sistema
        var membership = await _unitOfWork.QuinielaMembers.GetMembershipAsync(quinielaId, userId);
        var isOwnerOrAdmin = membership != null && (membership.Role == "OWNER" || membership.Role == "ADMIN");
        if (!isOwnerOrAdmin)
        {
            var user = await _unitOfWork.Users.GetAsync(userId);
            isOwnerOrAdmin = user != null && user.Role == "ADMIN";
        }
        if (!isOwnerOrAdmin) return Fail("Solo el Owner o un Admin puede abrir la nueva temporada.");

        // 4. No archivada
        if (!quiniela.IsActive) return Fail("Esta quiniela ya está archivada.");

        // 5. Debe tener temporada asignada
        if (quiniela.SeasonId == null || quiniela.LeagueId == null)
            return Fail("La quiniela no tiene temporada asignada.");

        // 6. Debe existir una temporada vigente distinta a la actual de la quiniela
        var leagueId = quiniela.LeagueId.Value;
        var next = await _unitOfWork.Seasons.GetCurrentSeasonAsync(leagueId);
        if (next == null || next.Id == quiniela.SeasonId)
            return Fail("Aún no existe la siguiente temporada de esta liga.");

        // 7. No duplicar
        var leagueQuinielas = await _unitOfWork.Quinielas.GetByLeagueIdAsync(leagueId);
        if (leagueQuinielas.Any(q => q.SeasonId == next.Id && q.OwnerId == quiniela.OwnerId))
            return Fail("Ya existe la quiniela de la siguiente temporada.");

        var league = await _unitOfWork.Leagues.GetAsync(leagueId);
        if (league == null) return Fail("La liga de la quiniela no existe.");

        var members = (await _unitOfWork.QuinielaMembers.GetMembersAsync(quinielaId))
            .Where(m => m.Active)
            .ToList();

        var inviteCode = await GenerateUniqueInviteCodeAsync(league.Code);
        var now = DateTime.UtcNow;

        var newQuiniela = new Quiniela
        {
            Name = string.IsNullOrWhiteSpace(request.Name) ? quiniela.Name : request.Name.Trim(),
            // La descripción no se copia: suele mencionar la temporada anterior.
            Description = null,
            QuinielaType = quiniela.QuinielaType,
            LeagueId = quiniela.LeagueId,
            OwnerId = quiniela.OwnerId,
            InviteCode = inviteCode,
            EntryFee = quiniela.EntryFee,
            FirstPlacePct = quiniela.FirstPlacePct,
            SecondPlacePct = quiniela.SecondPlacePct,
            ThirdPlacePct = quiniela.ThirdPlacePct,
            SeasonId = next.Id,
            GamificationEnabled = quiniela.GamificationEnabled,
            IsActive = true,
            Active = true,
            Created = now
        };

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            // 8. Crear la quiniela de la nueva temporada
            if (!await _unitOfWork.Quinielas.InsertAsync(newQuiniela))
                throw new InvalidOperationException("No se pudo crear la quiniela de la nueva temporada.");

            // 9. Copiar miembros activos con acumulados en cero
            foreach (var m in members)
            {
                var copied = await _unitOfWork.QuinielaMembers.InsertAsync(new QuinielaMember
                {
                    QuinielaId = newQuiniela.Id,
                    UserId = m.UserId,
                    Alias = m.Alias,
                    Role = m.Role,
                    PaidFee = false,
                    TotalPoints = 0,
                    TotalHits = 0,
                    TotalUpsets = 0,
                    TotalHumillaciones = 0,
                    CurrentStreak = 0,
                    BestStreak = 0,
                    JoinedAt = now,
                    Active = true,
                    Created = now
                });
                if (!copied)
                    throw new InvalidOperationException("No se pudo copiar a un miembro a la nueva temporada.");
            }

            // 10. Último paso: archivar la anterior
            quiniela.IsActive = false;
            quiniela.ArchivedAt = DateTime.UtcNow;
            if (!await _unitOfWork.Quinielas.UpdateAsync(quiniela))
                throw new InvalidOperationException("No se pudo archivar la quiniela anterior.");

            await _unitOfWork.CommitTransactionAsync();
        }
        catch (Exception)
        {
            await _unitOfWork.RollbackTransactionAsync();
            return Fail("Error al crear la nueva temporada. La quiniela anterior no fue modificada.");
        }

        // Recargar con liga, deporte y dueño para que la respuesta salga completa (sportName, ownerName).
        var reloaded = await _unitOfWork.Quinielas.GetWithDetailsAsync(newQuiniela.Id);
        var dto = _mapper.Map<QuinielaResponseDto>(reloaded ?? newQuiniela);
        dto.UserRole = "OWNER";
        dto.MembersCount = members.Count;
        dto.SeasonName ??= next.Name;

        response.isSuccess = true;
        response.Message = "Nueva temporada creada exitosamente.";
        response.Data = dto;
        return response;
    }

    public async Task<Response<QuinielaResponseDto>> SetGamificationAsync(int quinielaId, int userId, bool enabled)
    {
        var response = new Response<QuinielaResponseDto>();

        Response<QuinielaResponseDto> Fail(string message)
        {
            response.isSuccess = false;
            response.Message = message;
            return response;
        }

        var quiniela = await _unitOfWork.Quinielas.GetAsync(quinielaId);
        if (quiniela == null) return Fail("La quiniela no existe.");

        if (quiniela.QuinielaType == QuinielaType.CustomEvents)
            return Fail("La gamificación solo aplica a quinielas deportivas.");

        var membership = await _unitOfWork.QuinielaMembers.GetMembershipAsync(quinielaId, userId);
        var isOwnerOrAdmin = membership != null && (membership.Role == "OWNER" || membership.Role == "ADMIN");
        if (!isOwnerOrAdmin)
        {
            var user = await _unitOfWork.Users.GetAsync(userId);
            isOwnerOrAdmin = user != null && user.Role == "ADMIN";
        }
        if (!isOwnerOrAdmin) return Fail("Solo el Owner o un Admin puede cambiar la gamificación.");

        if (quiniela.SeasonId == null) return Fail("La quiniela no tiene temporada asignada.");

        if (!quiniela.IsActive) return Fail("La quiniela está archivada.");

        quiniela.GamificationEnabled = enabled;
        await _unitOfWork.Quinielas.UpdateAsync(quiniela);

        var dto = _mapper.Map<QuinielaResponseDto>(quiniela);
        dto.UserRole = membership?.Role ?? "ADMIN";

        response.isSuccess = true;
        response.Message = "Gamificación actualizada.";
        response.Data = dto;
        return response;
    }

    public async Task<Response<IEnumerable<QuinielaResponseDto>>> GetUserQuinielasAsync(int userId)
    {
        var response = new Response<IEnumerable<QuinielaResponseDto>>();
        var quinielas = await _unitOfWork.Quinielas.GetUserQuinielasAsync(userId);

        var resultList = new List<QuinielaResponseDto>();
        foreach (var q in quinielas)
        {
            await QuinielaSeasonResolver.EnsureSeasonAssignedAsync(_unitOfWork, q);
            var dto = _mapper.Map<QuinielaResponseDto>(q);
            var myMembership = q.Members.FirstOrDefault(m => m.UserId == userId && m.Active);
            dto.UserRole = myMembership?.Role ?? (q.OwnerId == userId ? "OWNER" : "MEMBER");
            dto.MembersCount = q.Members.Count(m => m.Active);
            resultList.Add(dto);
        }

        response.isSuccess = true;
        response.Message = "Quinielas consultadas exitosamente.";
        response.Data = resultList;
        return response;
    }

    public async Task<Response<QuinielaDetailDto>> GetByIdAsync(int quinielaId, int userId)
    {
        var response = new Response<QuinielaDetailDto>();
        var quiniela = await _unitOfWork.Quinielas.GetWithDetailsAsync(quinielaId);

        if (quiniela == null || !(quiniela.IsActive || quiniela.ArchivedAt != null))
        {
            response.isSuccess = false;
            response.Message = "La quiniela no existe o está inactiva.";
            return response;
        }

        var myMembership = quiniela.Members.FirstOrDefault(m => m.UserId == userId && m.Active);

        // SPEC-020: el historial de una quiniela archivada solo lo lee quien participó (o su owner).
        if (!quiniela.IsActive && myMembership == null && quiniela.OwnerId != userId)
        {
            response.isSuccess = false;
            response.Message = "La quiniela no existe o está inactiva.";
            return response;
        }

        var detailDto = _mapper.Map<QuinielaDetailDto>(quiniela);
        detailDto.UserRole = myMembership?.Role ?? (quiniela.OwnerId == userId ? "OWNER" : null);
        detailDto.CurrentUserMemberId = myMembership?.Id;

        response.isSuccess = true;
        response.Message = "Detalle de quiniela obtenido con éxito.";
        response.Data = detailDto;
        return response;
    }

    public async Task<Response<QuinielaMemberDto>> JoinAsync(int userId, JoinQuinielaDto request)
    {
        var response = new Response<QuinielaMemberDto>();
        var validation = await _joinValidator.ValidateAsync(request);

        if (!validation.IsValid)
        {
            response.isSuccess = false;
            response.Message = "Datos para unirse a la quiniela inválidos.";
            response.Errors = validation.Errors;
            return response;
        }

        var quiniela = await _unitOfWork.Quinielas.GetByInviteCodeAsync(request.InviteCode);
        if (quiniela == null || !quiniela.IsActive)
        {
            response.isSuccess = false;
            response.Message = "Código de invitación no válido o quiniela inactiva.";
            return response;
        }

        // Comprobar si ya es miembro
        var existingMember = await _unitOfWork.QuinielaMembers.GetMembershipAsync(quiniela.Id, userId);
        if (existingMember != null)
        {
            response.isSuccess = false;
            response.Message = "Ya eres miembro de esta quiniela.";
            return response;
        }

        var user = await _unitOfWork.Users.GetAsync(userId);
        if (user == null || !user.Active)
        {
            response.isSuccess = false;
            response.Message = "Usuario no autorizado o inactivo.";
            return response;
        }

        var newMember = new QuinielaMember
        {
            QuinielaId = quiniela.Id,
            UserId = userId,
            Alias = request.Alias.Trim(),
            Role = "MEMBER",
            PaidFee = false,
            JoinedAt = DateTime.UtcNow,
            Active = true,
            Created = DateTime.UtcNow
        };

        var inserted = await _unitOfWork.QuinielaMembers.InsertAsync(newMember);
        if (!inserted)
        {
            response.isSuccess = false;
            response.Message = "Error al unirse a la quiniela.";
            return response;
        }

        var memberDto = _mapper.Map<QuinielaMemberDto>(newMember);
        memberDto.DisplayName = user.DisplayName;
        memberDto.AvatarUrl = user.AvatarUrl;

        response.isSuccess = true;
        response.Message = "Te has unido exitosamente a la quiniela.";
        response.Data = memberDto;
        return response;
    }

    public async Task<Response<QuinielaMemberDto>> UpdatePaymentStatusAsync(
        int quinielaId,
        int memberId,
        bool paidFee,
        int requestingUserId)
    {
        var response = new Response<QuinielaMemberDto>();

        // Verificar permisos del solicitante (debe ser OWNER o ADMIN en la quiniela, o Admin del sistema)
        var requestingUser = await _unitOfWork.Users.GetAsync(requestingUserId);
        var requestingMembership = await _unitOfWork.QuinielaMembers.GetMembershipAsync(quinielaId, requestingUserId);

        var isOwnerOrAdmin = (requestingMembership != null && (requestingMembership.Role == "OWNER" || requestingMembership.Role == "ADMIN"))
                             || (requestingUser != null && requestingUser.Role == "ADMIN");

        if (!isOwnerOrAdmin)
        {
            response.isSuccess = false;
            response.Message = "No tienes permisos para modificar el pago de la cuota (solo Admin u Owner).";
            return response;
        }

        var targetMember = await _unitOfWork.QuinielaMembers.GetAsync(memberId);
        if (targetMember == null || targetMember.QuinielaId != quinielaId || !targetMember.Active)
        {
            response.isSuccess = false;
            response.Message = "Miembro no encontrado en esta quiniela.";
            return response;
        }

        targetMember.PaidFee = paidFee;
        var updated = await _unitOfWork.QuinielaMembers.UpdateAsync(targetMember);
        if (!updated)
        {
            response.isSuccess = false;
            response.Message = "Error al actualizar el estado de pago del miembro.";
            return response;
        }

        response.isSuccess = true;
        response.Message = "Estado de pago actualizado correctamente.";
        response.Data = _mapper.Map<QuinielaMemberDto>(targetMember);
        return response;
    }

    private async Task<string> GenerateUniqueInviteCodeAsync(string leagueCode)
    {
        var prefix = leagueCode.ToLower() switch
        {
            "mex.1" => "MX",
            "eng.1" => "PL",
            "nfl" => "NFL",
            "ev" or "events" or "customevents" => "EV",
            _ => "PQ"
        };

        var year = DateTime.UtcNow.Year;
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        while (true)
        {
            var randomBytes = new byte[2];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }

            var suffix = new string(new[]
            {
                chars[randomBytes[0] % chars.Length],
                chars[randomBytes[1] % chars.Length]
            });

            // Formato: "MX-2026-X9" (longitud 10 caracteres <= 12)
            var candidate = $"{prefix}-{year}-{suffix}";

            var existing = await _unitOfWork.Quinielas.GetByInviteCodeAsync(candidate);
            if (existing == null)
            {
                return candidate;
            }
        }
    }

    public async Task<Response<IEnumerable<LeagueDto>>> GetActiveLeaguesAsync()
    {
        var response = new Response<IEnumerable<LeagueDto>>();
        try
        {
            var leagues = await _unitOfWork.Leagues.GetActiveLeaguesWithSportAsync();
            response.Data = _mapper.ToLeagueDTOList(leagues);
            response.isSuccess = true;
            response.Message = "Ligas obtenidas exitosamente.";
        }
        catch (Exception ex)
        {
            response.isSuccess = false;
            response.Message = $"Error al obtener las ligas: {ex.Message}";
        }

        return response;
    }

    public async Task<Response<IEnumerable<MigratedMemberClaimLinkDto>>> GetClaimLinksAsync(int quinielaId, int requestingUserId, string? originUrl = null)
    {
        var response = new Response<IEnumerable<MigratedMemberClaimLinkDto>>();
        var quiniela = await _unitOfWork.Quinielas.GetAsync(quinielaId);
        if (quiniela == null || !quiniela.IsActive)
        {
            response.isSuccess = false;
            response.Message = "La quiniela no existe o está inactiva.";
            return response;
        }

        var requestingUser = await _unitOfWork.Users.GetAsync(requestingUserId);
        bool isOwnerOrAdmin = quiniela.OwnerId == requestingUserId || (requestingUser != null && requestingUser.Role == "ADMIN");
        if (!isOwnerOrAdmin)
        {
            response.isSuccess = false;
            response.Message = "Solo el creador o administrador de la quiniela puede consultar los enlaces de activación.";
            return response;
        }

        var members = await _unitOfWork.QuinielaMembers.GetMembersAsync(quinielaId);
        var baseOrigin = !string.IsNullOrWhiteSpace(originUrl) ? originUrl.TrimEnd('/') : "https://picksports.orionsys.net";

        var resultList = new List<MigratedMemberClaimLinkDto>();
        bool needsSave = false;

        foreach (var m in members)
        {
            if (m.User == null) continue;
            bool isMigrated = m.User.Email.EndsWith("@quiniela.local", StringComparison.OrdinalIgnoreCase);

            string? token = m.User.Token;
            if (isMigrated && string.IsNullOrWhiteSpace(token))
            {
                token = Guid.NewGuid().ToString("N");
                m.User.Token = token;
                await _unitOfWork.Users.UpdateAsync(m.User);
                needsSave = true;
            }

            string? claimUrl = isMigrated ? $"{baseOrigin}/activar?token={token}" : null;
            string? shareMessage = isMigrated 
                ? $"¡Hola {m.Alias}! Ya puedes entrar a la quiniela *{quiniela.Name}*. Tu historial con *{m.TotalHits} aciertos* ya está registrado. Activa tu cuenta aquí para ingresar tus pronósticos de la Jornada 10: {claimUrl}"
                : null;

            resultList.Add(new MigratedMemberClaimLinkDto
            {
                MemberId = m.Id,
                UserId = m.UserId,
                Alias = m.Alias,
                Email = m.User.Email,
                TotalHits = m.TotalHits,
                IsClaimed = !isMigrated,
                ClaimToken = token,
                ClaimUrl = claimUrl,
                ShareMessage = shareMessage
            });
        }

        if (needsSave)
        {
            await _unitOfWork.Save();
        }

        response.isSuccess = true;
        response.Message = "Enlaces de activación obtenidos exitosamente.";
        response.Data = resultList.OrderBy(x => x.IsClaimed).ThenBy(x => x.Alias);
        return response;
    }
}
