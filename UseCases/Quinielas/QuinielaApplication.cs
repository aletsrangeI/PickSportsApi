using System.Security.Cryptography;
using Common;
using Domain.Entities;
using DTO.League;
using DTO.Quiniela;
using Interface.Mapping;
using Interface.Persistence;
using Interface.UseCases;
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

        var league = await _unitOfWork.Leagues.GetAsync(request.LeagueId);
        if (league == null || !league.Active)
        {
            response.isSuccess = false;
            response.Message = "La liga seleccionada no existe o no está activa.";
            return response;
        }

        // Generar código de invitación único alfanumérico (ej: MX-2026-X9)
        var inviteCode = await GenerateUniqueInviteCodeAsync(league.Code);

        var quiniela = new Quiniela
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            LeagueId = request.LeagueId,
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

    public async Task<Response<IEnumerable<QuinielaResponseDto>>> GetUserQuinielasAsync(int userId)
    {
        var response = new Response<IEnumerable<QuinielaResponseDto>>();
        var quinielas = await _unitOfWork.Quinielas.GetUserQuinielasAsync(userId);

        var resultList = new List<QuinielaResponseDto>();
        foreach (var q in quinielas)
        {
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

        if (quiniela == null || !quiniela.IsActive)
        {
            response.isSuccess = false;
            response.Message = "La quiniela no existe o está inactiva.";
            return response;
        }

        var detailDto = _mapper.Map<QuinielaDetailDto>(quiniela);
        var myMembership = quiniela.Members.FirstOrDefault(m => m.UserId == userId && m.Active);
        detailDto.UserRole = myMembership?.Role ?? (quiniela.OwnerId == userId ? "OWNER" : null);

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
