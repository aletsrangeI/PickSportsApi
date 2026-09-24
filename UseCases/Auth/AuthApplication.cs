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

    public AuthApplication(
        IUnitOfWork unitOfWork,
        IJwtTokenGenerator jwtTokenGenerator,
        PasswordHasher passwordHasher,
        IAppMapper mapper,
        RegisterRequestDtoValidator registerValidator,
        LoginRequestDtoValidator loginValidator)
    {
        _unitOfWork = unitOfWork;
        _jwtTokenGenerator = jwtTokenGenerator;
        _passwordHasher = passwordHasher;
        _mapper = mapper;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
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
}
