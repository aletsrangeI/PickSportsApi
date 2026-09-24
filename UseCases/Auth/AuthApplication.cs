using Common;
using Common.Security;
using Domain.Entities;
using DTO.Auth;
using Interface.Mapping;
using Interface.Persistence;
using Interface.Security;
using Interface.UseCases;
using Validator.Auth;

namespace UseCases.Auth;

public class AuthApplication : IAuthApplication
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly PasswordHasher _passwordHasher;
    private readonly IAppMapper _mapper;
    private readonly RegisterRequestDtoValidator _registerValidator;
    private readonly LoginRequestDtoValidator _loginValidator;
    private readonly ClaimAccountRequestDtoValidator _claimValidator;

    public AuthApplication(
        IUnitOfWork unitOfWork,
        IJwtTokenGenerator jwtTokenGenerator,
        PasswordHasher passwordHasher,
        IAppMapper mapper,
        RegisterRequestDtoValidator registerValidator,
        LoginRequestDtoValidator loginValidator,
        ClaimAccountRequestDtoValidator? claimValidator = null)
    {
        _unitOfWork = unitOfWork;
        _jwtTokenGenerator = jwtTokenGenerator;
        _passwordHasher = passwordHasher;
        _mapper = mapper;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
        _claimValidator = claimValidator ?? new ClaimAccountRequestDtoValidator();
    }

    public async Task<Response<AuthResponseDto>> RegisterAsync(RegisterRequestDto request)
    {
        var response = new Response<AuthResponseDto>();
        var validation = await _registerValidator.ValidateAsync(request);

        if (!validation.IsValid)
        {
            response.isSuccess = false;
            response.Message = "Errores de validación en el registro";
            response.Errors = validation.Errors;
            return response;
        }

        var normalizedEmail = request.Email.Trim().ToLower();
        var normalizedUsername = request.Username.Trim().ToLower();

        var existingEmail = await _unitOfWork.Users.GetByEmailAsync(normalizedEmail);
        if (existingEmail != null)
        {
            response.isSuccess = false;
            response.Message = "El correo electrónico ya se encuentra registrado.";
            return response;
        }

        var existingUsername = await _unitOfWork.Users.GetByUsernameAsync(normalizedUsername);
        if (existingUsername != null)
        {
            response.isSuccess = false;
            response.Message = "El nombre de usuario ya está en uso.";
            return response;
        }

        var hashedPassword = _passwordHasher.Hash(request.Password);
        var user = new User
        {
            Username = request.Username.Trim(),
            Email = normalizedEmail,
            PasswordHash = hashedPassword,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? request.Username.Trim() : request.DisplayName.Trim(),
            Role = "USER",
            Active = true,
            Created = DateTime.UtcNow
        };

        var inserted = await _unitOfWork.Users.InsertAsync(user);
        if (!inserted)
        {
            response.isSuccess = false;
            response.Message = "Error al guardar el usuario en la base de datos.";
            return response;
        }

        var token = _jwtTokenGenerator.GenerateToken(user);
        var userProfile = _mapper.Map<UserProfileDto>(user);

        response.isSuccess = true;
        response.Message = "Usuario registrado exitosamente.";
        response.Data = new AuthResponseDto
        {
            Token = token,
            User = userProfile
        };

        return response;
    }

    public async Task<Response<AuthResponseDto>> LoginAsync(LoginRequestDto request)
    {
        var response = new Response<AuthResponseDto>();
        var validation = await _loginValidator.ValidateAsync(request);

        if (!validation.IsValid)
        {
            response.isSuccess = false;
            response.Message = "Datos de inicio de sesión incompletos.";
            response.Errors = validation.Errors;
            return response;
        }

        var identifier = request.Email.Trim().ToLower();
        var user = await _unitOfWork.Users.GetByEmailAsync(identifier) 
                   ?? await _unitOfWork.Users.GetByUsernameAsync(identifier);

        if (user == null || !_passwordHasher.Check(request.Password, user.PasswordHash))
        {
            response.isSuccess = false;
            response.Message = "Credenciales incorrectas. Verifique su correo/usuario y contraseña.";
            return response;
        }

        var token = _jwtTokenGenerator.GenerateToken(user);
        var userProfile = _mapper.Map<UserProfileDto>(user);

        response.isSuccess = true;
        response.Message = "Inicio de sesión exitoso.";
        response.Data = new AuthResponseDto
        {
            Token = token,
            User = userProfile
        };

        return response;
    }

    public async Task<Response<UserProfileDto>> GetCurrentUserAsync(int userId)
    {
        var response = new Response<UserProfileDto>();
        var user = await _unitOfWork.Users.GetAsync(userId);

        if (user == null || !user.Active)
        {
            response.isSuccess = false;
            response.Message = "Usuario no encontrado.";
            return response;
        }

        response.isSuccess = true;
        response.Message = "Perfil obtenido con éxito.";
        response.Data = _mapper.Map<UserProfileDto>(user);
        return response;
    }

    public async Task<Response<ClaimInfoDto>> GetClaimInfoAsync(string token)
    {
        var response = new Response<ClaimInfoDto>();
        if (string.IsNullOrWhiteSpace(token))
        {
            response.isSuccess = false;
            response.Message = "Token de activación no proporcionado.";
            return response;
        }

        var user = await _unitOfWork.Users.GetByTokenAsync(token.Trim());
        if (user == null)
        {
            response.isSuccess = false;
            response.Message = "El enlace de activación es inválido o ya ha sido utilizado.";
            return response;
        }

        bool isMigratedPlaceholder = user.Email.EndsWith("@quiniela.local", StringComparison.OrdinalIgnoreCase);
        if (!isMigratedPlaceholder)
        {
            response.isSuccess = false;
            response.Message = "Esta cuenta ya fue activada previamente. Por favor inicia sesión con tu correo.";
            response.Data = new ClaimInfoDto
            {
                Alias = user.DisplayName ?? user.Username,
                EmailPlaceholder = user.Email,
                IsAlreadyClaimed = true
            };
            return response;
        }

        var memberships = await _unitOfWork.QuinielaMembers.GetByUserIdAsync(user.Id);
        var primaryMembership = memberships.FirstOrDefault();

        response.isSuccess = true;
        response.Message = "Información de activación obtenida exitosamente.";
        response.Data = new ClaimInfoDto
        {
            Alias = primaryMembership?.Alias ?? user.DisplayName ?? user.Username,
            QuinielaName = primaryMembership?.Quiniela?.Name ?? "Quiniela PickSports",
            QuinielaId = primaryMembership?.QuinielaId ?? 0,
            TotalHits = primaryMembership?.TotalHits ?? 0,
            TotalUpsets = primaryMembership?.TotalUpsets ?? 0,
            CurrentStreak = primaryMembership?.CurrentStreak ?? 0,
            EmailPlaceholder = user.Email,
            IsAlreadyClaimed = false
        };

        return response;
    }

    public async Task<Response<AuthResponseDto>> ClaimAccountAsync(ClaimAccountRequestDto request)
    {
        var response = new Response<AuthResponseDto>();
        var validation = await _claimValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            response.isSuccess = false;
            response.Message = "Errores de validación en la activación de cuenta.";
            response.Errors = validation.Errors;
            return response;
        }

        var user = await _unitOfWork.Users.GetByTokenAsync(request.Token.Trim());
        if (user == null)
        {
            response.isSuccess = false;
            response.Message = "El enlace de activación es inválido o ya expiró.";
            return response;
        }

        bool isMigratedPlaceholder = user.Email.EndsWith("@quiniela.local", StringComparison.OrdinalIgnoreCase);
        if (!isMigratedPlaceholder)
        {
            response.isSuccess = false;
            response.Message = "Esta cuenta ya ha sido activada previamente. Inicia sesión directamente.";
            return response;
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var existingUserWithEmail = await _unitOfWork.Users.GetByEmailAsync(normalizedEmail);

        if (existingUserWithEmail != null && existingUserWithEmail.Id != user.Id)
        {
            // El usuario ya había creado una cuenta previamente con este email:
            // Verificamos contraseña de esa cuenta para vincularle su historial
            if (!_passwordHasher.Check(request.Password, existingUserWithEmail.PasswordHash))
            {
                response.isSuccess = false;
                response.Message = "El correo ya está registrado con otra cuenta. Ingresa la contraseña correcta de dicha cuenta para vincular tu historial.";
                return response;
            }

            // Transferir membresías del usuario temporal al usuario existente
            var tempMemberships = await _unitOfWork.QuinielaMembers.GetByUserIdAsync(user.Id);
            foreach (var mem in tempMemberships)
            {
                var existingMembership = await _unitOfWork.QuinielaMembers.GetMembershipAsync(mem.QuinielaId, existingUserWithEmail.Id);
                if (existingMembership == null)
                {
                    mem.UserId = existingUserWithEmail.Id;
                    await _unitOfWork.QuinielaMembers.UpdateAsync(mem);
                }
            }

            // Desactivar usuario temporal
            user.Active = false;
            user.Token = null;
            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.Save();

            var jwtToken = _jwtTokenGenerator.GenerateToken(existingUserWithEmail);
            var userProfile = _mapper.Map<UserProfileDto>(existingUserWithEmail);

            response.isSuccess = true;
            response.Message = "¡Historial vinculado exitosamente a tu cuenta existente!";
            response.Data = new AuthResponseDto
            {
                Token = jwtToken,
                User = userProfile
            };
            return response;
        }

        // Caso normal: nueva cuenta
        var desiredUsername = !string.IsNullOrWhiteSpace(request.Username) 
            ? request.Username.Trim().ToLowerInvariant() 
            : user.Username;

        var userWithSameUsername = await _unitOfWork.Users.GetByUsernameAsync(desiredUsername);
        if (userWithSameUsername != null && userWithSameUsername.Id != user.Id)
        {
            desiredUsername = normalizedEmail.Split('@')[0];
            var stillTaken = await _unitOfWork.Users.GetByUsernameAsync(desiredUsername);
            if (stillTaken != null && stillTaken.Id != user.Id)
            {
                desiredUsername = $"{desiredUsername}{new Random().Next(10, 99)}";
            }
        }

        user.Username = desiredUsername;
        user.Email = normalizedEmail;
        user.Password = _passwordHasher.Hash(request.Password);
        if (!string.IsNullOrWhiteSpace(request.DisplayName))
        {
            user.DisplayName = request.DisplayName.Trim();
        }
        user.Token = null; // Token consumido

        var updated = await _unitOfWork.Users.UpdateAsync(user);
        if (!updated)
        {
            response.isSuccess = false;
            response.Message = "Error al actualizar la cuenta en la base de datos.";
            return response;
        }
        await _unitOfWork.Save();

        var token = _jwtTokenGenerator.GenerateToken(user);
        var profile = _mapper.Map<UserProfileDto>(user);

        response.isSuccess = true;
        response.Message = "¡Cuenta activada exitosamente! Bienvenido a la quiniela.";
        response.Data = new AuthResponseDto
        {
            Token = token,
            User = profile
        };

        return response;
    }

    public async Task<Response<UnclaimedQuinielaMembersDto>> GetUnclaimedMembersAsync(int? quinielaId = null)
    {
        var response = new Response<UnclaimedQuinielaMembersDto>();

        Quiniela? quiniela = null;
        if (quinielaId.HasValue && quinielaId.Value > 0)
        {
            quiniela = await _unitOfWork.Quinielas.GetAsync(quinielaId.Value);
        }
        else
        {
            var quinielas = (await _unitOfWork.Quinielas.GetAllAsync()).Where(q => q.Active && q.IsActive).ToList();
            quiniela = quinielas.OrderByDescending(q => q.Id).FirstOrDefault();
        }

        if (quiniela == null)
        {
            response.isSuccess = false;
            response.Message = "No se encontró una quiniela activa disponible.";
            return response;
        }

        var members = await _unitOfWork.QuinielaMembers.GetMembersAsync(quiniela.Id);
        bool needsSave = false;
        var unclaimedList = new List<UnclaimedMemberItemDto>();

        foreach (var m in members)
        {
            if (m.User == null) continue;
            bool isMigrated = m.User.Email.EndsWith("@quiniela.local", StringComparison.OrdinalIgnoreCase);
            if (!isMigrated) continue;

            if (string.IsNullOrWhiteSpace(m.User.Token))
            {
                m.User.Token = Guid.NewGuid().ToString("N");
                await _unitOfWork.Users.UpdateAsync(m.User);
                needsSave = true;
            }

            unclaimedList.Add(new UnclaimedMemberItemDto
            {
                MemberId = m.Id,
                UserId = m.UserId,
                Alias = m.Alias,
                TotalHits = m.TotalHits,
                TotalUpsets = m.TotalUpsets,
                CurrentStreak = m.CurrentStreak,
                ClaimToken = m.User.Token
            });
        }

        if (needsSave)
        {
            await _unitOfWork.Save();
        }

        response.isSuccess = true;
        response.Message = "Participantes pendientes de vinculación obtenidos con éxito.";
        response.Data = new UnclaimedQuinielaMembersDto
        {
            QuinielaId = quiniela.Id,
            QuinielaName = quiniela.Name,
            Members = unclaimedList.OrderBy(u => u.Alias).ToList()
        };

        return response;
    }

    public async Task<Response<AuthResponseDto>> LinkClaimedMemberForCurrentUserAsync(int currentUserId, string token)
    {
        var response = new Response<AuthResponseDto>();
        if (string.IsNullOrWhiteSpace(token))
        {
            response.isSuccess = false;
            response.Message = "Token de participante no proporcionado.";
            return response;
        }

        var tempUser = await _unitOfWork.Users.GetByTokenAsync(token.Trim());
        if (tempUser == null || !tempUser.Email.EndsWith("@quiniela.local", StringComparison.OrdinalIgnoreCase))
        {
            response.isSuccess = false;
            response.Message = "El participante seleccionado ya fue vinculado o el enlace es inválido.";
            return response;
        }

        var currentUser = await _unitOfWork.Users.GetAsync(currentUserId);
        if (currentUser == null || !currentUser.Active)
        {
            response.isSuccess = false;
            response.Message = "Usuario actual no encontrado o inactivo.";
            return response;
        }

        var tempMemberships = await _unitOfWork.QuinielaMembers.GetByUserIdAsync(tempUser.Id);
        foreach (var mem in tempMemberships)
        {
            var existingMembership = await _unitOfWork.QuinielaMembers.GetMembershipAsync(mem.QuinielaId, currentUser.Id);
            if (existingMembership == null)
            {
                mem.UserId = currentUser.Id;
                await _unitOfWork.QuinielaMembers.UpdateAsync(mem);
            }
            else
            {
                if (existingMembership.TotalHits == 0 && mem.TotalHits > 0)
                {
                    existingMembership.TotalHits = mem.TotalHits;
                    existingMembership.TotalUpsets = mem.TotalUpsets;
                    existingMembership.TotalHumillaciones = mem.TotalHumillaciones;
                    existingMembership.CurrentStreak = mem.CurrentStreak;
                    existingMembership.BestStreak = mem.BestStreak;
                    existingMembership.Alias = mem.Alias;
                    await _unitOfWork.QuinielaMembers.UpdateAsync(existingMembership);
                }
                mem.Active = false;
                await _unitOfWork.QuinielaMembers.UpdateAsync(mem);
            }
        }

        tempUser.Active = false;
        tempUser.Token = null;
        await _unitOfWork.Users.UpdateAsync(tempUser);
        await _unitOfWork.Save();

        var jwtToken = _jwtTokenGenerator.GenerateToken(currentUser);
        var profile = _mapper.Map<UserProfileDto>(currentUser);

        response.isSuccess = true;
        response.Message = "¡Historial de la quiniela vinculado exitosamente con tu cuenta!";
        response.Data = new AuthResponseDto
        {
            Token = jwtToken,
            User = profile
        };

        return response;
    }
}
