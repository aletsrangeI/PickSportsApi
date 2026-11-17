using Common.Security;
using Domain.Entities;
using DTO.Auth;
using Interface.Mapping;
using Interface.Persistence;
using Interface.Security;
using Moq;
using UseCases.Auth;
using UseCases.Common.Mapping;
using Validator.Auth;

namespace PickSportsApi.UnitTests.UseCasesTests;

public class AuthApplicationTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IJwtTokenGenerator> _jwtMock;
    private readonly PasswordHasher _passwordHasher;
    private readonly IAppMapper _mapper;
    private readonly RegisterRequestDtoValidator _registerValidator;
    private readonly LoginRequestDtoValidator _loginValidator;

    public AuthApplicationTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _jwtMock = new Mock<IJwtTokenGenerator>();
        _jwtMock.Setup(j => j.GenerateRefreshToken()).Returns("generated-refresh-token");
        // Cada login/registro/claim crea una sesion por dispositivo.
        _unitOfWorkMock.Setup(u => u.UserSessions.InsertAsync(It.IsAny<UserSession>())).ReturnsAsync(true);
        _passwordHasher = new PasswordHasher();

        _mapper = new AppMapper();

        _registerValidator = new RegisterRequestDtoValidator();
        _loginValidator = new LoginRequestDtoValidator();
    }

    [Fact]
    public async Task RegisterAsync_WithValidData_CreatesUserWithSaltedHashAndReturnsToken()
    {
        // Arrange
        var request = new RegisterRequestDto
        {
            Email = "alex@amigos.com",
            Username = "alex",
            Password = "Secreto123!",
            DisplayName = "Alex"
        };

        _unitOfWorkMock.Setup(u => u.Users.GetByEmailAsync("alex@amigos.com"))
            .ReturnsAsync((User?)null);
        _unitOfWorkMock.Setup(u => u.Users.GetByUsernameAsync("alex"))
            .ReturnsAsync((User?)null);

        User? capturedUser = null;
        _unitOfWorkMock.Setup(u => u.Users.InsertAsync(It.IsAny<User>()))
            .Callback<User>(u => { capturedUser = u; u.Id = 1; })
            .ReturnsAsync(true);

        _jwtMock.Setup(j => j.GenerateToken(It.IsAny<User>()))
            .Returns("fake-jwt-token-alex");

        var app = new AuthApplication(
            _unitOfWorkMock.Object,
            _jwtMock.Object,
            _passwordHasher,
            _mapper,
            _registerValidator,
            _loginValidator);

        // Act
        var result = await app.RegisterAsync(request);

        // Assert
        Assert.True(result.isSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal("fake-jwt-token-alex", result.Data.Token);
        Assert.Equal("alex@amigos.com", result.Data.User.Email);
        Assert.Equal("alex", result.Data.User.Username);

        // Verify password hash contains salt (format: salt:hash)
        Assert.NotNull(capturedUser);
        Assert.Contains(":", capturedUser.PasswordHash);
        Assert.True(_passwordHasher.Check("Secreto123!", capturedUser.PasswordHash));
    }

    [Fact]
    public async Task RegisterAsync_WithDuplicateEmail_ReturnsError()
    {
        // Arrange
        var request = new RegisterRequestDto
        {
            Email = "alex@amigos.com",
            Username = "alex2",
            Password = "Secreto123!"
        };

        _unitOfWorkMock.Setup(u => u.Users.GetByEmailAsync("alex@amigos.com"))
            .ReturnsAsync(new User { Id = 1, Email = "alex@amigos.com", Username = "alex1" });

        var app = new AuthApplication(
            _unitOfWorkMock.Object,
            _jwtMock.Object,
            _passwordHasher,
            _mapper,
            _registerValidator,
            _loginValidator);

        // Act
        var result = await app.RegisterAsync(request);

        // Assert
        Assert.False(result.isSuccess);
        Assert.Contains("correo electrónico ya se encuentra registrado", result.Message);
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsSuccessAndToken()
    {
        // Arrange
        var hashedPassword = _passwordHasher.Hash("Secreto123!");
        var user = new User
        {
            Id = 1,
            Email = "alex@amigos.com",
            Username = "alex",
            PasswordHash = hashedPassword,
            DisplayName = "Alex",
            Active = true
        };

        _unitOfWorkMock.Setup(u => u.Users.GetByEmailAsync("alex@amigos.com"))
            .ReturnsAsync(user);

        _jwtMock.Setup(j => j.GenerateToken(user))
            .Returns("valid-jwt-token");

        var app = new AuthApplication(
            _unitOfWorkMock.Object,
            _jwtMock.Object,
            _passwordHasher,
            _mapper,
            _registerValidator,
            _loginValidator);

        // Act
        var result = await app.LoginAsync(new LoginRequestDto
        {
            Email = "alex@amigos.com",
            Password = "Secreto123!"
        });

        // Assert
        Assert.True(result.isSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal("valid-jwt-token", result.Data.Token);
    }

    [Fact]
    public async Task LoginAsync_WithInvalidPassword_ReturnsError()
    {
        // Arrange
        var hashedPassword = _passwordHasher.Hash("Secreto123!");
        var user = new User
        {
            Id = 1,
            Email = "alex@amigos.com",
            PasswordHash = hashedPassword,
            Active = true
        };

        _unitOfWorkMock.Setup(u => u.Users.GetByEmailAsync("alex@amigos.com"))
            .ReturnsAsync(user);

        var app = new AuthApplication(
            _unitOfWorkMock.Object,
            _jwtMock.Object,
            _passwordHasher,
            _mapper,
            _registerValidator,
            _loginValidator);

        // Act
        var result = await app.LoginAsync(new LoginRequestDto
        {
            Email = "alex@amigos.com",
            Password = "WrongPassword!"
        });

        // Assert
        Assert.False(result.isSuccess);
        Assert.Contains("Credenciales incorrectas", result.Message);
    }

    [Fact]
    public async Task GetClaimInfoAsync_WithValidToken_ReturnsClaimInfo()
    {
        // Arrange
        var token = "token-carlos-123";
        var user = new User
        {
            Id = 10,
            Username = "carlos",
            Email = "carlos@quiniela.local",
            DisplayName = "Carlos",
            Token = token,
            Active = true
        };
        var quiniela = new Quiniela { Id = 4, Name = "Liga MX Apertura 2026" };
        var member = new QuinielaMember
        {
            Id = 100,
            QuinielaId = 4,
            UserId = 10,
            Alias = "Carlos",
            TotalHits = 48,
            Quiniela = quiniela,
            Active = true
        };

        _unitOfWorkMock.Setup(u => u.Users.GetByTokenAsync(token)).ReturnsAsync(user);
        _unitOfWorkMock.Setup(u => u.QuinielaMembers.GetByUserIdAsync(10)).ReturnsAsync(new List<QuinielaMember> { member });

        var app = new AuthApplication(
            _unitOfWorkMock.Object,
            _jwtMock.Object,
            _passwordHasher,
            _mapper,
            _registerValidator,
            _loginValidator);

        // Act
        var result = await app.GetClaimInfoAsync(token);

        // Assert
        Assert.True(result.isSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal("Carlos", result.Data.Alias);
        Assert.Equal("Liga MX Apertura 2026", result.Data.QuinielaName);
        Assert.Equal(48, result.Data.TotalHits);
        Assert.False(result.Data.IsAlreadyClaimed);
    }

    [Fact]
    public async Task ClaimAccountAsync_WithValidNewAccount_UpdatesUserAndReturnsToken()
    {
        // Arrange
        var token = "token-carlos-123";
        var user = new User
        {
            Id = 10,
            Username = "carlos",
            Email = "carlos@quiniela.local",
            DisplayName = "Carlos",
            Token = token,
            Active = true
        };

        _unitOfWorkMock.Setup(u => u.Users.GetByTokenAsync(token)).ReturnsAsync(user);
        _unitOfWorkMock.Setup(u => u.Users.GetByEmailAsync("carlos.nuevo@gmail.com")).ReturnsAsync((User?)null);
        _unitOfWorkMock.Setup(u => u.Users.GetByUsernameAsync("carlos")).ReturnsAsync(user);
        _unitOfWorkMock.Setup(u => u.Users.UpdateAsync(It.IsAny<User>())).ReturnsAsync(true);
        _unitOfWorkMock.Setup(u => u.Save(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _jwtMock.Setup(j => j.GenerateToken(It.IsAny<User>())).Returns("claimed-jwt-token");

        var app = new AuthApplication(
            _unitOfWorkMock.Object,
            _jwtMock.Object,
            _passwordHasher,
            _mapper,
            _registerValidator,
            _loginValidator);

        // Act
        var result = await app.ClaimAccountAsync(new ClaimAccountRequestDto
        {
            Token = token,
            Email = "carlos.nuevo@gmail.com",
            Password = "Password123!",
            DisplayName = "Carlos G"
        });

        // Assert
        Assert.True(result.isSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal("claimed-jwt-token", result.Data.Token);
        Assert.Equal("carlos.nuevo@gmail.com", user.Email);
        Assert.Null(user.Token); // Token consumido
    }

    [Fact]
    public async Task UploadAvatarAsync_WithValidImage_UpdatesAvatarUrlAndReturnsUpdatedProfile()
    {
        // Arrange
        var user = new User
        {
            Id = 10,
            Username = "alex",
            Email = "alex@test.com",
            DisplayName = "Alex",
            Active = true
        };

        _unitOfWorkMock.Setup(u => u.Users.GetAsync(10)).ReturnsAsync(user);
        _unitOfWorkMock.Setup(u => u.Users.UpdateAsync(It.IsAny<User>())).ReturnsAsync(true);
        _unitOfWorkMock.Setup(u => u.Save(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var avatarStorageMock = new Mock<Interface.UseCases.IAvatarStorageService>();
        avatarStorageMock.Setup(s => s.SaveAvatarAsync(
            10,
            It.IsAny<Stream>(),
            "alex.jpg",
            "image/jpeg",
            It.IsAny<CancellationToken>()))
            .ReturnsAsync("/api/auth/avatar/avatar_u10_abc.jpg");

        var app = new AuthApplication(
            _unitOfWorkMock.Object,
            _jwtMock.Object,
            _passwordHasher,
            _mapper,
            _registerValidator,
            _loginValidator,
            avatarStorageService: avatarStorageMock.Object);

        using var stream = new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF, 0x00 });

        // Act
        var result = await app.UploadAvatarAsync(10, stream, "alex.jpg", "image/jpeg", 1024);

        // Assert
        Assert.True(result.isSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal("/api/auth/avatar/avatar_u10_abc.jpg", result.Data.AvatarUrl);
        Assert.Equal("/api/auth/avatar/avatar_u10_abc.jpg", user.AvatarUrl);
        _unitOfWorkMock.Verify(u => u.Users.UpdateAsync(It.Is<User>(u => u.AvatarUrl == "/api/auth/avatar/avatar_u10_abc.jpg")), Times.Once);
    }

    [Fact]
    public async Task UploadAvatarAsync_WhenFileExceeds5MB_ReturnsErrorWithoutSaving()
    {
        // Arrange
        var avatarStorageMock = new Mock<Interface.UseCases.IAvatarStorageService>();
        var app = new AuthApplication(
            _unitOfWorkMock.Object,
            _jwtMock.Object,
            _passwordHasher,
            _mapper,
            _registerValidator,
            _loginValidator,
            avatarStorageService: avatarStorageMock.Object);

        using var stream = new MemoryStream(new byte[] { 0x01 });
        long size6MB = 6 * 1024 * 1024;

        // Act
        var result = await app.UploadAvatarAsync(10, stream, "huge.jpg", "image/jpeg", size6MB);

        // Assert
        Assert.False(result.isSuccess);
        Assert.Contains("5 MB", result.Message);
        avatarStorageMock.Verify(s => s.SaveAvatarAsync(It.IsAny<int>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RemoveAvatarAsync_WithExistingAvatar_DeletesStorageAndClearsAvatarUrl()
    {
        // Arrange
        var user = new User
        {
            Id = 15,
            Username = "omar",
            Email = "omar@test.com",
            DisplayName = "Omar",
            AvatarUrl = "/api/auth/avatar/avatar_u15_old.png",
            Active = true
        };

        _unitOfWorkMock.Setup(u => u.Users.GetAsync(15)).ReturnsAsync(user);
        _unitOfWorkMock.Setup(u => u.Users.UpdateAsync(It.IsAny<User>())).ReturnsAsync(true);
        _unitOfWorkMock.Setup(u => u.Save(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var avatarStorageMock = new Mock<Interface.UseCases.IAvatarStorageService>();
        avatarStorageMock.Setup(s => s.DeleteAvatarAsync("/api/auth/avatar/avatar_u15_old.png", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var app = new AuthApplication(
            _unitOfWorkMock.Object,
            _jwtMock.Object,
            _passwordHasher,
            _mapper,
            _registerValidator,
            _loginValidator,
            avatarStorageService: avatarStorageMock.Object);

        // Act
        var result = await app.RemoveAvatarAsync(15);

        // Assert
        Assert.True(result.isSuccess);
        Assert.NotNull(result.Data);
        Assert.Null(result.Data.AvatarUrl);
        Assert.Null(user.AvatarUrl);
        avatarStorageMock.Verify(s => s.DeleteAvatarAsync("/api/auth/avatar/avatar_u15_old.png", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RefreshAsync_WithExpiredJwtAndValidRefreshToken_SuccessfullyReturnsNewTokens()
    {
        // Arrange - Setup real configuration and JwtTokenGenerator
        var configMock = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
        configMock.Setup(c => c["Config:Secret"]).Returns("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");
        configMock.Setup(c => c["Config:Issuer"]).Returns("OrionSys");
        configMock.Setup(c => c["Config:Audience"]).Returns("OrionSys");

        var realJwtGenerator = new UseCases.Security.JwtTokenGenerator(configMock.Object);

        // Generate an already expired token (expired 5 minutes ago)
        var secret = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        var key = System.Text.Encoding.ASCII.GetBytes(secret);
        var tokenHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var claims = new List<System.Security.Claims.Claim>
        {
            new(System.Security.Claims.ClaimTypes.NameIdentifier, "42"),
            new(System.Security.Claims.ClaimTypes.Name, "42"),
            new("username", "testuser"),
            new(System.Security.Claims.ClaimTypes.Email, "test@test.com"),
            new(System.Security.Claims.ClaimTypes.Role, "USER"),
            new("displayName", "Test User")
        };
        var tokenDescriptor = new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor
        {
            Subject = new System.Security.Claims.ClaimsIdentity(claims),
            NotBefore = DateTime.UtcNow.AddMinutes(-15),
            IssuedAt = DateTime.UtcNow.AddMinutes(-15),
            Expires = DateTime.UtcNow.AddMinutes(-5), // Expired!
            SigningCredentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(
                new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(key),
                Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256Signature
            ),
            Issuer = "OrionSys",
            Audience = "OrionSys"
        };
        var expiredToken = tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));

        var refreshToken = "valid-refresh-token-xyz-123";
        var user = new User
        {
            Id = 42,
            Username = "testuser",
            Email = "test@test.com",
            DisplayName = "Test User",
            RefreshToken = refreshToken,
            RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7),
            Active = true
        };

        _unitOfWorkMock.Setup(u => u.Users.GetAsync(42)).ReturnsAsync(user);
        _unitOfWorkMock.Setup(u => u.Users.UpdateAsync(It.IsAny<User>())).ReturnsAsync(true);
        _unitOfWorkMock.Setup(u => u.Save(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var app = new AuthApplication(
            _unitOfWorkMock.Object,
            realJwtGenerator,
            _passwordHasher,
            _mapper,
            _registerValidator,
            _loginValidator);

        // Act
        var result = await app.RefreshAsync(new RefreshRequestDto
        {
            AccessToken = expiredToken,
            RefreshToken = refreshToken
        });

        // Assert
        Assert.True(result.isSuccess, $"Refresh failed with message: {result.Message}");
        Assert.NotNull(result.Data);
        Assert.False(string.IsNullOrWhiteSpace(result.Data.Token));
        Assert.False(string.IsNullOrWhiteSpace(result.Data.RefreshToken));
        Assert.NotEqual(refreshToken, result.Data.RefreshToken); // Rotated
        Assert.Equal(42, result.Data.User.Id);
    }

    private const string RefreshTestSecret = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private static string CreateExpiredAccessToken(int userId, string secret = RefreshTestSecret)
    {
        var key = System.Text.Encoding.ASCII.GetBytes(secret);
        var descriptor = new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor
        {
            Subject = new System.Security.Claims.ClaimsIdentity(new[]
            {
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, userId.ToString())
            }),
            NotBefore = DateTime.UtcNow.AddHours(-3),
            IssuedAt = DateTime.UtcNow.AddHours(-3),
            Expires = DateTime.UtcNow.AddHours(-2),
            SigningCredentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(
                new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(key),
                Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256Signature),
            Issuer = "OrionSys",
            Audience = "OrionSys"
        };
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }

    private static User CreateRefreshUser(int id = 42) => new()
    {
        Id = id,
        Username = $"user{id}",
        Email = $"user{id}@test.com",
        DisplayName = $"User {id}",
        Active = true
    };

    private static UserSession CreateSession(int userId, string refreshToken, int expiresInDays = 30) => new()
    {
        Id = userId * 100 + refreshToken.Length,
        UserId = userId,
        RefreshToken = refreshToken,
        ExpiresAt = DateTime.UtcNow.AddDays(expiresInDays),
        CreatedAt = DateTime.UtcNow.AddDays(-1),
        LastUsedAt = DateTime.UtcNow.AddDays(-1)
    };

    private AuthApplication BuildRefreshApp(List<User> users, List<UserSession> sessions)
    {
        var configMock = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
        configMock.Setup(c => c["Config:Secret"]).Returns(RefreshTestSecret);
        configMock.Setup(c => c["Config:Issuer"]).Returns("OrionSys");
        configMock.Setup(c => c["Config:Audience"]).Returns("OrionSys");

        _unitOfWorkMock.Setup(u => u.Users.GetAsync(It.IsAny<int>()))
            .ReturnsAsync((int id) => users.FirstOrDefault(x => x.Id == id)!);
        _unitOfWorkMock.Setup(u => u.Users.UpdateAsync(It.IsAny<User>())).ReturnsAsync(true);
        _unitOfWorkMock.Setup(u => u.UserSessions.GetByRefreshTokenAsync(It.IsAny<string>()))
            .ReturnsAsync((string token) => sessions.FirstOrDefault(s => s.RefreshToken == token || s.PreviousRefreshToken == token));
        _unitOfWorkMock.Setup(u => u.UserSessions.UpdateAsync(It.IsAny<UserSession>())).ReturnsAsync(true);
        _unitOfWorkMock.Setup(u => u.UserSessions.InsertAsync(It.IsAny<UserSession>()))
            .Callback<UserSession>(sessions.Add).ReturnsAsync(true);
        _unitOfWorkMock.Setup(u => u.Save(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        return new AuthApplication(
            _unitOfWorkMock.Object,
            new UseCases.Security.JwtTokenGenerator(configMock.Object),
            _passwordHasher,
            _mapper,
            _registerValidator,
            _loginValidator);
    }

    [Fact]
    public async Task RefreshAsync_TwoWindowsRefreshingWithSameToken_BothSucceedAndShareTheSameNewToken()
    {
        // Arrange: PWA abierta y ventana abierta por la notificacion renuevan a la vez con el mismo refresh token
        var session = CreateSession(42, "refresh-1");
        var app = BuildRefreshApp(new List<User> { CreateRefreshUser() }, new List<UserSession> { session });
        var request = new RefreshRequestDto { AccessToken = CreateExpiredAccessToken(42), RefreshToken = "refresh-1" };

        // Act
        var first = await app.RefreshAsync(request);
        var second = await app.RefreshAsync(request);

        // Assert
        Assert.True(first.isSuccess, first.Message);
        Assert.True(second.isSuccess, $"La segunda ventana no deberia perder la sesion: {second.Message}");
        Assert.Equal(first.Data.RefreshToken, second.Data.RefreshToken);
        Assert.Equal(first.Data.RefreshToken, session.RefreshToken);
        Assert.NotEqual("refresh-1", session.RefreshToken);
    }

    [Fact]
    public async Task RefreshAsync_PreviousTokenAfterGraceWindow_IsRejected()
    {
        // Arrange: el token anterior ya paso su ventana de gracia
        var session = CreateSession(42, "refresh-2");
        session.PreviousRefreshToken = "refresh-1";
        session.PreviousRefreshTokenValidUntil = DateTime.UtcNow.AddSeconds(-1);
        var app = BuildRefreshApp(new List<User> { CreateRefreshUser() }, new List<UserSession> { session });

        // Act
        var result = await app.RefreshAsync(new RefreshRequestDto
        {
            AccessToken = CreateExpiredAccessToken(42),
            RefreshToken = "refresh-1"
        });

        // Assert
        Assert.False(result.isSuccess);
        Assert.Equal("refresh-2", session.RefreshToken);
    }

    [Fact]
    public async Task RefreshAsync_ExtendsSessionLifetimeToSixtyDays()
    {
        // Arrange
        var session = CreateSession(42, "refresh-1", expiresInDays: 1);
        var app = BuildRefreshApp(new List<User> { CreateRefreshUser() }, new List<UserSession> { session });

        // Act
        var result = await app.RefreshAsync(new RefreshRequestDto
        {
            AccessToken = CreateExpiredAccessToken(42),
            RefreshToken = "refresh-1"
        });

        // Assert
        Assert.True(result.isSuccess, result.Message);
        Assert.True(session.ExpiresAt >= DateTime.UtcNow.AddDays(59),
            $"La sesion deberia durar ~60 dias y vence en {session.ExpiresAt:o}");
    }

    [Fact]
    public async Task RefreshAsync_RefreshingOneDevice_DoesNotInvalidateTheOtherDevice()
    {
        // Arrange: el mismo usuario con sesion en Android y en iPhone
        var android = CreateSession(42, "android-token");
        var iphone = CreateSession(42, "iphone-token");
        var app = BuildRefreshApp(new List<User> { CreateRefreshUser() }, new List<UserSession> { android, iphone });

        // Act
        var androidResult = await app.RefreshAsync(new RefreshRequestDto
        {
            AccessToken = CreateExpiredAccessToken(42), RefreshToken = "android-token"
        });
        var iphoneResult = await app.RefreshAsync(new RefreshRequestDto
        {
            AccessToken = CreateExpiredAccessToken(42), RefreshToken = "iphone-token"
        });

        // Assert
        Assert.True(androidResult.isSuccess, androidResult.Message);
        Assert.True(iphoneResult.isSuccess, $"El iPhone perdio la sesion: {iphoneResult.Message}");
        Assert.NotEqual(android.RefreshToken, iphone.RefreshToken);
    }

    [Fact]
    public async Task RefreshAsync_AccessTokenSignedWithAnotherKey_IsRejected()
    {
        // Arrange: token con el userId correcto pero firmado por un tercero
        var session = CreateSession(42, "refresh-1");
        var app = BuildRefreshApp(new List<User> { CreateRefreshUser() }, new List<UserSession> { session });
        var forged = CreateExpiredAccessToken(42, secret: "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff");

        // Act
        var result = await app.RefreshAsync(new RefreshRequestDto { AccessToken = forged, RefreshToken = "refresh-1" });

        // Assert
        Assert.False(result.isSuccess);
        Assert.Equal("refresh-1", session.RefreshToken);
    }

    [Fact]
    public async Task RefreshAsync_SessionBelongingToAnotherUser_IsRejected()
    {
        // Arrange: el access token es del usuario 42 pero el refresh token es de la sesion del usuario 7
        var othersSession = CreateSession(7, "refresh-of-7");
        var app = BuildRefreshApp(
            new List<User> { CreateRefreshUser(42), CreateRefreshUser(7) },
            new List<UserSession> { othersSession });

        // Act
        var result = await app.RefreshAsync(new RefreshRequestDto
        {
            AccessToken = CreateExpiredAccessToken(42), RefreshToken = "refresh-of-7"
        });

        // Assert
        Assert.False(result.isSuccess);
    }

    [Fact]
    public async Task RefreshAsync_LegacyUserRefreshToken_IsPromotedToASession()
    {
        // Arrange: sesion emitida antes de UserSessions (el token vive en la fila del usuario)
        var user = CreateRefreshUser();
        user.RefreshToken = "legacy-token";
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(3);
        var sessions = new List<UserSession>();
        var app = BuildRefreshApp(new List<User> { user }, sessions);

        // Act
        var result = await app.RefreshAsync(new RefreshRequestDto
        {
            AccessToken = CreateExpiredAccessToken(42), RefreshToken = "legacy-token"
        });

        // Assert
        Assert.True(result.isSuccess, result.Message);
        var created = Assert.Single(sessions);
        Assert.Equal(result.Data.RefreshToken, created.RefreshToken);
        Assert.NotEqual("legacy-token", created.RefreshToken);
        Assert.Null(user.RefreshToken);
    }
}
