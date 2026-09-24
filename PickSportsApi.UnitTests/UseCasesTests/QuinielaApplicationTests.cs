using Domain.Entities;
using DTO.Quiniela;
using Interface.Mapping;
using Interface.Persistence;
using Moq;
using UseCases.Common.Mapping;
using UseCases.Quinielas;
using Validator.Quiniela;

namespace PickSportsApi.UnitTests.UseCasesTests;

public class QuinielaApplicationTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly IAppMapper _mapper;
    private readonly CreateQuinielaDtoValidator _createValidator;
    private readonly JoinQuinielaDtoValidator _joinValidator;

    public QuinielaApplicationTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _mapper = new AppMapper();

        _createValidator = new CreateQuinielaDtoValidator();
        _joinValidator = new JoinQuinielaDtoValidator();
    }

    [Fact]
    public async Task CreateAsync_WithValidData_CreatesQuinielaWithInviteCodeAndOwnerMembership()
    {
        // Arrange (Escenario 2)
        var user = new User
        {
            Id = 1,
            Username = "alex",
            DisplayName = "Alex G",
            Active = true
        };

        var league = new League
        {
            Id = 10,
            Code = "mex.1",
            Name = "Liga MX",
            Active = true,
            Sport = new Sport { Id = 1, Name = "Fútbol" }
        };

        _unitOfWorkMock.Setup(u => u.Users.GetAsync(1)).ReturnsAsync(user);
        _unitOfWorkMock.Setup(u => u.Leagues.GetAsync(10)).ReturnsAsync(league);
        _unitOfWorkMock.Setup(u => u.Quinielas.GetByInviteCodeAsync(It.IsAny<string>()))
            .ReturnsAsync((Quiniela?)null);

        Quiniela? savedQuiniela = null;
        _unitOfWorkMock.Setup(u => u.Quinielas.InsertAsync(It.IsAny<Quiniela>()))
            .Callback<Quiniela>(q => { savedQuiniela = q; q.Id = 99; })
            .ReturnsAsync(true);

        QuinielaMember? savedMember = null;
        _unitOfWorkMock.Setup(u => u.QuinielaMembers.InsertAsync(It.IsAny<QuinielaMember>()))
            .Callback<QuinielaMember>(m => { savedMember = m; m.Id = 101; })
            .ReturnsAsync(true);

        _unitOfWorkMock.Setup(u => u.Quinielas.GetWithDetailsAsync(99))
            .ReturnsAsync(() => new Quiniela
            {
                Id = 99,
                Name = "Liga MX Clausura 2026",
                LeagueId = 10,
                League = league,
                OwnerId = 1,
                Owner = user,
                InviteCode = savedQuiniela!.InviteCode,
                EntryFee = 100m,
                FirstPlacePct = 70m,
                SecondPlacePct = 20m,
                ThirdPlacePct = 10m,
                IsActive = true,
                Members = new List<QuinielaMember> { savedMember! }
            });

        var app = new QuinielaApplication(_unitOfWorkMock.Object, _mapper, _createValidator, _joinValidator);

        // Act
        var result = await app.CreateAsync(1, new CreateQuinielaDto
        {
            Name = "Liga MX Clausura 2026",
            LeagueId = 10,
            EntryFee = 100m,
            FirstPlacePct = 70m,
            SecondPlacePct = 20m,
            ThirdPlacePct = 10m
        });

        // Assert
        Assert.True(result.isSuccess);
        Assert.NotNull(result.Data);
        Assert.StartsWith("MX-", result.Data.InviteCode);
        Assert.Equal("OWNER", result.Data.UserRole);
        Assert.Equal(1, result.Data.MembersCount);

        Assert.NotNull(savedMember);
        Assert.Equal(1, savedMember.UserId);
        Assert.Equal("OWNER", savedMember.Role);
        Assert.True(savedMember.PaidFee);
    }

    [Fact]
    public async Task JoinAsync_WithValidInviteCode_AddsMemberWithRoleMember()
    {
        // Arrange (Escenario 3)
        var quiniela = new Quiniela
        {
            Id = 50,
            InviteCode = "MX-2026-X9",
            IsActive = true,
            Active = true
        };

        var user = new User
        {
            Id = 2,
            Username = "gigi",
            DisplayName = "Gigi",
            Active = true
        };

        _unitOfWorkMock.Setup(u => u.Quinielas.GetByInviteCodeAsync("MX-2026-X9"))
            .ReturnsAsync(quiniela);
        _unitOfWorkMock.Setup(u => u.QuinielaMembers.GetMembershipAsync(50, 2))
            .ReturnsAsync((QuinielaMember?)null);
        _unitOfWorkMock.Setup(u => u.Users.GetAsync(2))
            .ReturnsAsync(user);

        QuinielaMember? joinedMember = null;
        _unitOfWorkMock.Setup(u => u.QuinielaMembers.InsertAsync(It.IsAny<QuinielaMember>()))
            .Callback<QuinielaMember>(m => { joinedMember = m; m.Id = 201; })
            .ReturnsAsync(true);

        var app = new QuinielaApplication(_unitOfWorkMock.Object, _mapper, _createValidator, _joinValidator);

        // Act
        var result = await app.JoinAsync(2, new JoinQuinielaDto
        {
            InviteCode = "MX-2026-X9",
            Alias = "Gigi"
        });

        // Assert
        Assert.True(result.isSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal("Gigi", result.Data.Alias);
        Assert.Equal("MEMBER", result.Data.Role);

        Assert.NotNull(joinedMember);
        Assert.Equal(50, joinedMember.QuinielaId);
        Assert.Equal(2, joinedMember.UserId);
        Assert.Equal("MEMBER", joinedMember.Role);
        Assert.False(joinedMember.PaidFee);
    }

    [Fact]
    public async Task JoinAsync_WhenAlreadyMember_RejectsRequest()
    {
        // Arrange (Escenario 3: rechazo si ya es miembro)
        var quiniela = new Quiniela
        {
            Id = 50,
            InviteCode = "MX-2026-X9",
            IsActive = true,
            Active = true
        };

        _unitOfWorkMock.Setup(u => u.Quinielas.GetByInviteCodeAsync("MX-2026-X9"))
            .ReturnsAsync(quiniela);
        _unitOfWorkMock.Setup(u => u.QuinielaMembers.GetMembershipAsync(50, 2))
            .ReturnsAsync(new QuinielaMember { Id = 201, QuinielaId = 50, UserId = 2, Role = "MEMBER", Active = true });

        var app = new QuinielaApplication(_unitOfWorkMock.Object, _mapper, _createValidator, _joinValidator);

        // Act
        var result = await app.JoinAsync(2, new JoinQuinielaDto
        {
            InviteCode = "MX-2026-X9",
            Alias = "Gigi"
        });

        // Assert
        Assert.False(result.isSuccess);
        Assert.Contains("Ya eres miembro de esta quiniela", result.Message);
    }

    [Fact]
    public async Task UpdatePaymentStatus_WhenRequesterIsOwner_UpdatesPayment()
    {
        // Arrange
        var targetMember = new QuinielaMember
        {
            Id = 301,
            QuinielaId = 50,
            UserId = 5,
            PaidFee = false,
            Active = true
        };

        var ownerMembership = new QuinielaMember
        {
            Id = 100,
            QuinielaId = 50,
            UserId = 1,
            Role = "OWNER",
            Active = true
        };

        _unitOfWorkMock.Setup(u => u.Users.GetAsync(1))
            .ReturnsAsync(new User { Id = 1, Role = "USER" });
        _unitOfWorkMock.Setup(u => u.QuinielaMembers.GetMembershipAsync(50, 1))
            .ReturnsAsync(ownerMembership);
        _unitOfWorkMock.Setup(u => u.QuinielaMembers.GetAsync(301))
            .ReturnsAsync(targetMember);
        _unitOfWorkMock.Setup(u => u.QuinielaMembers.UpdateAsync(targetMember))
            .ReturnsAsync(true);

        var app = new QuinielaApplication(_unitOfWorkMock.Object, _mapper, _createValidator, _joinValidator);

        // Act
        var result = await app.UpdatePaymentStatusAsync(50, 301, true, 1);

        // Assert
        Assert.True(result.isSuccess);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.PaidFee);
        Assert.True(targetMember.PaidFee);
    }

    [Fact]
    public async Task UpdatePaymentStatus_WhenRequesterIsRegularMember_RejectsUnauthorized()
    {
        // Arrange
        var regularMember = new QuinielaMember
        {
            Id = 100,
            QuinielaId = 50,
            UserId = 2,
            Role = "MEMBER",
            Active = true
        };

        _unitOfWorkMock.Setup(u => u.Users.GetAsync(2))
            .ReturnsAsync(new User { Id = 2, Role = "USER" });
        _unitOfWorkMock.Setup(u => u.QuinielaMembers.GetMembershipAsync(50, 2))
            .ReturnsAsync(regularMember);

        var app = new QuinielaApplication(_unitOfWorkMock.Object, _mapper, _createValidator, _joinValidator);

        // Act
        var result = await app.UpdatePaymentStatusAsync(50, 301, true, 2);

        // Assert
        Assert.False(result.isSuccess);
        Assert.Contains("No tienes permisos", result.Message);
    }

    [Fact]
    public async Task GetActiveLeaguesAsync_ReturnsActiveLeaguesWithSportName()
    {
        // Arrange
        var mockLeagues = new List<League>
        {
            new League
            {
                Id = 1,
                Code = "mex.1",
                Name = "Liga MX",
                SportId = 1,
                Sport = new Sport { Id = 1, Name = "Fútbol" },
                Active = true
            },
            new League
            {
                Id = 4,
                Code = "nfl",
                Name = "NFL",
                SportId = 2,
                Sport = new Sport { Id = 2, Name = "Fútbol Americano" },
                Active = true
            }
        };

        _unitOfWorkMock.Setup(u => u.Leagues.GetActiveLeaguesWithSportAsync())
            .ReturnsAsync(mockLeagues);

        var app = new QuinielaApplication(_unitOfWorkMock.Object, _mapper, _createValidator, _joinValidator);

        // Act
        var result = await app.GetActiveLeaguesAsync();

        // Assert
        Assert.True(result.isSuccess);
        Assert.NotNull(result.Data);
        var list = result.Data.ToList();
        Assert.Equal(2, list.Count);
        Assert.Equal("Liga MX", list[0].Name);
        Assert.Equal("Fútbol", list[0].SportName);
        Assert.Equal("NFL", list[1].Name);
        Assert.Equal("Fútbol Americano", list[1].SportName);
    }
}
