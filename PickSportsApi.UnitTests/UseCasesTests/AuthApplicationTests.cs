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
}
