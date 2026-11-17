using System.Security.Claims;
using Interface.Persistence;
using Interface.UseCases;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Moq;
using WebApi.Controllers;
using Xunit;

namespace PickSportsApi.UnitTests.UseCasesTests;

public class LiguillaControllerSimulationGateTests
{
    private sealed class FakeEnvironment : IWebHostEnvironment
    {
        public FakeEnvironment(string name) => EnvironmentName = name;
        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "PickSportsApi";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static (LiguillaController Controller, Mock<ILiguillaApplication> App) Build(string environmentName)
    {
        var app = new Mock<ILiguillaApplication>();
        app.Setup(a => a.SeedSimulationNodesAsync(It.IsAny<int>())).ReturnsAsync(true);

        var controller = new LiguillaController(
            app.Object,
            new Mock<IUnitOfWork>().Object,
            new FakeEnvironment(environmentName));

        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Role, "ADMIN")
        }, "Test");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return (controller, app);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task SeedSimulation_OutsideDevelopment_Returns404_AndDoesNotCallApplication(string environmentName)
    {
        // Arrange
        var (controller, app) = Build(environmentName);

        // Act
        var result = await controller.SeedSimulation(5);

        // Assert
        Assert.IsType<NotFoundResult>(result);
        app.Verify(a => a.SeedSimulationNodesAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task SeedSimulation_InDevelopment_AsAdmin_CallsApplicationAndReturnsOk()
    {
        // Arrange
        var (controller, app) = Build(Environments.Development);

        // Act
        var result = await controller.SeedSimulation(5);

        // Assert
        Assert.IsType<OkObjectResult>(result);
        app.Verify(a => a.SeedSimulationNodesAsync(5), Times.Once);
    }
}
