using System.Security.Claims;
using Common;
using DTO.Liguilla;
using Interface.Persistence;
using Interface.UseCases;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Moq;
using WebApi.Controllers;
using Xunit;

namespace PickSportsApi.UnitTests.UseCasesTests;

/// <summary>SPEC-031 escenario 6: el controlador traduce el rechazo de membresía en HTTP 400 con <c>Response&lt;T&gt;</c>.</summary>
public class LiguillaControllerMembershipTests
{
    private const string NotMemberMessage = "No perteneces a esta quiniela.";
    private const int QuinielaId = 10;
    private const int UserId = 5;

    private sealed class FakeEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "PickSportsApi";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static (LiguillaController Controller, Mock<ILiguillaApplication> App) BuildAsNonMember()
    {
        var app = new Mock<ILiguillaApplication>();
        // Moq recursivo: IsUserAdminAsync consulta QuinielaMembers.GetMembershipAsync, que devuelve null (no miembro).
        var unitOfWork = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
        unitOfWork.Setup(u => u.QuinielaMembers.GetMembershipAsync(QuinielaId, UserId))
            .ReturnsAsync((Domain.Entities.QuinielaMember?)null);

        var controller = new LiguillaController(app.Object, unitOfWork.Object, new FakeEnvironment());
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, UserId.ToString()),
            new Claim(ClaimTypes.Role, "USER")
        }, "Test");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return (controller, app);
    }

    [Fact]
    public async Task GetStandings_WhenApplicationRejectsNonMember_Returns400WithNotMemberMessage()
    {
        // Arrange
        var (controller, app) = BuildAsNonMember();
        app.Setup(a => a.GetStandingsAsync(QuinielaId, UserId, false, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(NotMemberMessage));

        // Act
        var result = await controller.GetStandings(QuinielaId);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<Response<LiguillaStandingsDto>>(badRequest.Value);
        Assert.False(response.isSuccess);
        Assert.Equal(NotMemberMessage, response.Message);
    }

    [Fact]
    public async Task GetOverview_WhenApplicationRejectsNonMember_Returns400WithNotMemberMessage()
    {
        // Arrange
        var (controller, app) = BuildAsNonMember();
        app.Setup(a => a.GetOverviewAsync(QuinielaId, UserId, false, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(NotMemberMessage));

        // Act
        var result = await controller.GetOverview(QuinielaId);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<Response<LiguillaOverviewDto>>(badRequest.Value);
        Assert.False(response.isSuccess);
        Assert.Equal(NotMemberMessage, response.Message);
    }

    [Fact]
    public async Task GetStandings_AsNonAdminUser_ForwardsCurrentUserIdAndNonAdminFlag()
    {
        // Arrange
        var (controller, app) = BuildAsNonMember();
        app.Setup(a => a.GetStandingsAsync(QuinielaId, UserId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LiguillaStandingsDto { QuinielaId = QuinielaId });

        // Act
        var result = await controller.GetStandings(QuinielaId);

        // Assert
        Assert.IsType<OkObjectResult>(result);
        app.Verify(a => a.GetStandingsAsync(QuinielaId, UserId, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetStandings_WithAdminRoleClaim_ForwardsAdminFlag()
    {
        // Arrange
        var (controller, app) = BuildAsNonMember();
        var adminIdentity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, UserId.ToString()),
            new Claim(ClaimTypes.Role, "ADMIN")
        }, "Test");
        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(adminIdentity);
        app.Setup(a => a.GetStandingsAsync(QuinielaId, UserId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LiguillaStandingsDto { QuinielaId = QuinielaId });

        // Act
        var result = await controller.GetStandings(QuinielaId);

        // Assert
        Assert.IsType<OkObjectResult>(result);
        app.Verify(a => a.GetStandingsAsync(QuinielaId, UserId, true, It.IsAny<CancellationToken>()), Times.Once);
    }
}