using DTO.System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Persistence.Context;

namespace WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SystemController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SystemController> _logger;

    private const string VersionCacheKey = "System_ClientVersionConfig";

    public SystemController(
        ApplicationDbContext context,
        IMemoryCache cache,
        IConfiguration configuration,
        ILogger<SystemController> logger)
    {
        _context = context;
        _cache = cache;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Consulta el estado de versión y compatibilidad de clientes PWA/Web.
    /// </summary>
    [HttpGet("version")]
    [AllowAnonymous]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<SystemVersionDto>> GetVersion()
    {
        if (_cache.TryGetValue(VersionCacheKey, out SystemVersionDto? cachedDto) && cachedDto != null)
        {
            return Ok(new SystemVersionDto
            {
                ApiVersion = cachedDto.ApiVersion,
                LatestClientVersion = cachedDto.LatestClientVersion,
                MinSupportedClientVersion = cachedDto.MinSupportedClientVersion,
                MaintenanceMode = cachedDto.MaintenanceMode,
                MaintenanceMessage = cachedDto.MaintenanceMessage,
                ServerTimeUtc = DateTime.UtcNow
            });
        }

        try
        {
            var configs = await _context.SystemConfigs
                .AsNoTracking()
                .Where(c => c.IsPublic && c.Active)
                .ToDictionaryAsync(c => c.Key, c => c.Value);

            var apiVersion = _configuration["App:ApiVersion"] ?? "1.0.0";
            var configLatest = _configuration["App:LatestClientVersion"] ?? "1.0.0";
            var configMin = _configuration["App:MinSupportedClientVersion"] ?? "1.0.0";

            var dbLatest = configs.GetValueOrDefault("App:LatestClientVersion");
            var dbMin = configs.GetValueOrDefault("App:MinSupportedClientVersion");

            var latestClient = MaxVersion(configLatest, dbLatest);
            var minSupported = MaxVersion(configMin, dbMin);
            var maintenanceStr = configs.GetValueOrDefault("App:MaintenanceMode")
                                 ?? _configuration["App:MaintenanceMode"]
                                 ?? "false";
            var maintenanceMessage = configs.GetValueOrDefault("App:MaintenanceMessage")
                                     ?? _configuration["App:MaintenanceMessage"];

            bool.TryParse(maintenanceStr, out bool maintenanceMode);

            var dto = new SystemVersionDto
            {
                ApiVersion = apiVersion,
                LatestClientVersion = latestClient,
                MinSupportedClientVersion = minSupported,
                MaintenanceMode = maintenanceMode,
                MaintenanceMessage = string.IsNullOrWhiteSpace(maintenanceMessage) ? null : maintenanceMessage,
                ServerTimeUtc = DateTime.UtcNow
            };

            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromSeconds(60));

            _cache.Set(VersionCacheKey, dto, cacheOptions);

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error consultando configuraciones del sistema desde la base de datos.");

            var fallbackDto = new SystemVersionDto
            {
                ApiVersion = _configuration["App:ApiVersion"] ?? "1.0.0",
                LatestClientVersion = _configuration["App:LatestClientVersion"] ?? "1.0.0",
                MinSupportedClientVersion = _configuration["App:MinSupportedClientVersion"] ?? "1.0.0",
                MaintenanceMode = false,
                MaintenanceMessage = null,
                ServerTimeUtc = DateTime.UtcNow
            };

            return Ok(fallbackDto);
        }
    }

    private static string MaxVersion(string? v1, string? v2)
    {
        if (string.IsNullOrWhiteSpace(v1)) return v2 ?? "1.0.0";
        if (string.IsNullOrWhiteSpace(v2)) return v1;
        if (Version.TryParse(v1, out var parsed1) && Version.TryParse(v2, out var parsed2))
        {
            return parsed1 >= parsed2 ? v1 : v2;
        }
        return string.Compare(v1, v2, StringComparison.OrdinalIgnoreCase) >= 0 ? v1 : v2;
    }
}
