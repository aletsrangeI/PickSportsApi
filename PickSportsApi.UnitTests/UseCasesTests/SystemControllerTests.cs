using DTO.System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Persistence.Context;
using Persistence.Interceptors;
using WebApi.Controllers;
using Xunit;

namespace PickSportsApi.UnitTests.UseCasesTests;

public class SystemControllerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;

    public SystemControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var interceptor = new AuditableEntitySaveChangesInterceptor();
        _context = new ApplicationDbContext(options, interceptor);
        _cache = new MemoryCache(new MemoryCacheOptions());

        var inMemorySettings = new Dictionary<string, string?>
        {
            { "App:ApiVersion", "1.0.0" },
            { "App:LatestClientVersion", "1.0.0" },
            { "App:MinSupportedClientVersion", "1.0.0" }
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
    }

    [Fact]
    public async Task GetVersion_ReturnsConfiguredVersions_FromDatabase()
    {
        // Arrange
        await _context.SystemConfigs.AddRangeAsync(
            new Domain.Entities.SystemConfig
            {
                Key = "App:LatestClientVersion",
                Value = "1.2.0",
                IsPublic = true,
                Active = true
            },
            new Domain.Entities.SystemConfig
            {
                Key = "App:MinSupportedClientVersion",
                Value = "1.1.0",
                IsPublic = true,
                Active = true
            },
            new Domain.Entities.SystemConfig
            {
                Key = "App:MaintenanceMode",
                Value = "true",
                IsPublic = true,
                Active = true
            },
            new Domain.Entities.SystemConfig
            {
                Key = "App:MaintenanceMessage",
                Value = "Mantenimiento programado",
                IsPublic = true,
                Active = true
            }
        );
        await _context.SaveChangesAsync();

        var controller = new SystemController(
            _context,
            _cache,
            _configuration,
            new NullLogger<SystemController>()
        );

        // Act
        var result = await controller.GetVersion();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<SystemVersionDto>(okResult.Value);

        Assert.Equal("1.2.0", dto.LatestClientVersion);
        Assert.Equal("1.1.0", dto.MinSupportedClientVersion);
        Assert.True(dto.MaintenanceMode);
        Assert.Equal("Mantenimiento programado", dto.MaintenanceMessage);
    }

    [Fact]
    public async Task GetVersion_UsesCache_OnSubsequentRequests()
    {
        // Arrange
        await _context.SystemConfigs.AddAsync(
            new Domain.Entities.SystemConfig
            {
                Key = "App:LatestClientVersion",
                Value = "1.0.5",
                IsPublic = true,
                Active = true
            }
        );
        await _context.SaveChangesAsync();

        var controller = new SystemController(
            _context,
            _cache,
            _configuration,
            new NullLogger<SystemController>()
        );

        // Act 1: Initial call populates cache
        var firstResult = await controller.GetVersion();
        var firstOk = Assert.IsType<OkObjectResult>(firstResult.Result);
        var firstDto = Assert.IsType<SystemVersionDto>(firstOk.Value);
        Assert.Equal("1.0.5", firstDto.LatestClientVersion);

        // Mutate database directly
        var config = await _context.SystemConfigs.FirstAsync(c => c.Key == "App:LatestClientVersion");
        config.Value = "2.0.0";
        await _context.SaveChangesAsync();

        // Act 2: Should still return cached value "1.0.5" within 60s
        var secondResult = await controller.GetVersion();
        var secondOk = Assert.IsType<OkObjectResult>(secondResult.Result);
        var secondDto = Assert.IsType<SystemVersionDto>(secondOk.Value);
        Assert.Equal("1.0.5", secondDto.LatestClientVersion);
    }

    public void Dispose()
    {
        _cache.Dispose();
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
