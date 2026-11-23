using System.Security.Cryptography;
using System.Text;
using Domain.Common;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Persistence.Context;

namespace Persistence.Initialization;

public class DatabaseInitializer
{
    /// <summary>Tipo de temporada de ESPN para la temporada regular de la NFL (1 = pretemporada, 2 = regular, 3 = postemporada).</summary>
    private const int NflRegularSeasonType = 2;
    private const int NflRegularWeeksCount = 18;
    /// <summary>Valor por defecto de <c>League.WeeksCount</c>.</summary>
    private const int DefaultWeeksCount = 17;
    /// <summary>Version minima obligatoria del cliente (release 1.9.0). Subirla fuerza la actualizacion de la PWA.</summary>
    private const string ClientVersionFloor = "1.9.0";

    private readonly ApplicationDbContext _context;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(ApplicationDbContext context, ILogger<DatabaseInitializer> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        try
        {
            if (_context.Database.IsRelational())
            {
                await _context.Database.MigrateAsync();
            }
            await SeedSportsAndLeaguesAsync();
            await SeedAdminUserAsync();
            await SeedSystemConfigsAsync();
            await _context.SaveChangesAsync();
            await EnforceClientVersionFloorAsync();
            _logger.LogInformation("Database seeding completed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while seeding the database.");
            throw;
        }
    }

    private async Task SeedSportsAndLeaguesAsync()
    {
        // 1. Deportes
        var soccer = await _context.Sports.FirstOrDefaultAsync(s => s.Code == "SOCCER");
        if (soccer == null)
        {
            soccer = new Sport
            {
                Code = "SOCCER",
                Name = "Fútbol",
                HasDraw = true,
                Icon = "futbol",
                Active = true,
                Created = DateTime.UtcNow
            };
            await _context.Sports.AddAsync(soccer);
            await _context.SaveChangesAsync();
        }

        var nfl = await _context.Sports.FirstOrDefaultAsync(s => s.Code == "NFL");
        if (nfl == null)
        {
            nfl = new Sport
            {
                Code = "NFL",
                Name = "Fútbol Americano (NFL)",
                HasDraw = false,
                Icon = "football",
                Active = true,
                Created = DateTime.UtcNow
            };
            await _context.Sports.AddAsync(nfl);
            await _context.SaveChangesAsync();
        }

        var hockey = await _context.Sports.FirstOrDefaultAsync(s => s.Code == "HOCKEY");
        if (hockey == null)
        {
            hockey = new Sport
            {
                Code = "HOCKEY",
                Name = "Hockey",
                HasDraw = false,
                Icon = "hockey",
                Active = true,
                Created = DateTime.UtcNow
            };
            await _context.Sports.AddAsync(hockey);
            await _context.SaveChangesAsync();
        }

        // 2. Ligas
        var leaguesToSeed = new[]
        {
            new { SportId = soccer.Id, Code = "mex.1", Name = "Liga MX", Country = "México", EspnApiPath = "sports/soccer/mex.1", Logo = "https://a.espncdn.com/i/leaguelogos/soccer/500/mex.1.png", KnockoutFormatCode = (string?)KnockoutFormatCodes.FmfLiguilla },
            new { SportId = soccer.Id, Code = "eng.1", Name = "Premier League", Country = "Inglaterra", EspnApiPath = "sports/soccer/eng.1", Logo = "https://a.espncdn.com/i/leaguelogos/soccer/500/eng.1.png", KnockoutFormatCode = (string?)null },
            new { SportId = soccer.Id, Code = "uefa.champions", Name = "UEFA Champions League", Country = "Europa", EspnApiPath = "sports/soccer/uefa.champions", Logo = "https://a.espncdn.com/i/leaguelogos/soccer/500/uefa.champions.png", KnockoutFormatCode = (string?)KnockoutFormatCodes.UefaTwoLeg },
            new { SportId = nfl.Id, Code = "nfl", Name = "NFL", Country = "Estados Unidos", EspnApiPath = "sports/football/nfl", Logo = "https://a.espncdn.com/i/leaguelogos/nfl/500/nfl.png", KnockoutFormatCode = (string?)KnockoutFormatCodes.NflReseededSingleGame },
            new { SportId = hockey.Id, Code = "nhl", Name = "NHL", Country = "Estados Unidos", EspnApiPath = "sports/hockey/nhl", Logo = "https://a.espncdn.com/i/leaguelogos/hockey/500/nhl.png", KnockoutFormatCode = (string?)KnockoutFormatCodes.NhlBestOf7 }
        };

        foreach (var l in leaguesToSeed)
        {
            var existingLeague = await _context.Leagues.FirstOrDefaultAsync(x => x.Code == l.Code);
            if (existingLeague == null)
            {
                await _context.Leagues.AddAsync(new League
                {
                    SportId = l.SportId,
                    Code = l.Code,
                    Name = l.Name,
                    Country = l.Country,
                    EspnApiPath = l.EspnApiPath,
                    LogoUrl = l.Logo,
                    KnockoutFormatCode = l.KnockoutFormatCode,
                    Active = true,
                    Created = DateTime.UtcNow
                });
            }
            else if (existingLeague.KnockoutFormatCode == null && l.KnockoutFormatCode != null)
            {
                existingLeague.KnockoutFormatCode = l.KnockoutFormatCode;
            }
        }

        await _context.SaveChangesAsync();

        var nhlLeague = await _context.Leagues.FirstOrDefaultAsync(l => l.Code == "nhl");
        if (nhlLeague != null && nhlLeague.KnockoutFormatCode == null)
        {
            nhlLeague.KnockoutFormatCode = KnockoutFormatCodes.NhlBestOf7;
            await _context.SaveChangesAsync();
        }

        // 3. Temporadas activas (Liga MX tiene 2 torneos anuales: Clausura y Apertura)
        var ligaMx = await _context.Leagues.FirstOrDefaultAsync(l => l.Code == "mex.1");
        if (ligaMx != null)
        {
            var clausura = await _context.Seasons.FirstOrDefaultAsync(s => s.LeagueId == ligaMx.Id && s.Year == 2026 && s.Name.Contains("Clausura"));
            if (clausura == null)
            {
                await _context.Seasons.AddAsync(new Season
                {
                    LeagueId = ligaMx.Id,
                    Year = 2026,
                    SeasonType = 1,
                    Name = "Clausura 2026",
                    IsCurrent = true,
                    Active = true,
                    Created = DateTime.UtcNow
                });
            }

            var apertura = await _context.Seasons.FirstOrDefaultAsync(s => s.LeagueId == ligaMx.Id && s.Year == 2026 && s.Name.Contains("Apertura"));
            if (apertura == null)
            {
                await _context.Seasons.AddAsync(new Season
                {
                    LeagueId = ligaMx.Id,
                    Year = 2026,
                    SeasonType = 2,
                    Name = "Apertura 2026",
                    IsCurrent = false,
                    Active = true,
                    Created = DateTime.UtcNow
                });
            }
        }

        var nflLeague = await _context.Leagues.FirstOrDefaultAsync(l => l.Code == "nfl");
        if (nflLeague != null && nflLeague.WeeksCount == DefaultWeeksCount)
        {
            // La temporada regular de la NFL tiene 18 semanas; con 17 la semana 18 se trata como fase final y queda oculta.
            nflLeague.WeeksCount = NflRegularWeeksCount;
            await _context.SaveChangesAsync();
        }

        if (nflLeague != null && !await _context.Seasons.AnyAsync(s => s.LeagueId == nflLeague.Id && s.Year == 2026))
        {
            await _context.Seasons.AddAsync(new Season
            {
                LeagueId = nflLeague.Id,
                Year = 2026,
                SeasonType = NflRegularSeasonType,
                Name = "Temporada Regular 2026",
                IsCurrent = true,
                Active = true,
                Created = DateTime.UtcNow
            });
        }
    }

    private async Task SeedAdminUserAsync()
    {
        var admin = await _context.Users.FirstOrDefaultAsync(u => u.Email == "admin@picksports.local");
        if (admin == null)
        {
            admin = new User
            {
                Username = "admin",
                Email = "admin@picksports.local",
                DisplayName = "Administrador",
                Role = "ADMIN",
                PasswordHash = HashPassword("Admin123!"),
                Active = true,
                Created = DateTime.UtcNow
            };
            await _context.Users.AddAsync(admin);
        }
    }

    private async Task SeedSystemConfigsAsync()
    {
        var configs = new List<(string Key, string Value, string Description, bool IsPublic)>
        {
            ("App:MinSupportedClientVersion", "1.0.0", "Versión mínima compatible con los contratos de API y migraciones actuales", true),
            ("App:LatestClientVersion", "1.0.0", "Versión oficial más reciente del frontend disponible", true),
            ("App:MaintenanceMode", "false", "Flag para suspender temporalmente el acceso", true),
            ("App:MaintenanceMessage", "", "Mensaje informativo durante ventanas de mantenimiento", true)
        };

        foreach (var item in configs)
        {
            var exists = await _context.SystemConfigs.AnyAsync(c => c.Key == item.Key);
            if (!exists)
            {
                await _context.SystemConfigs.AddAsync(new SystemConfig
                {
                    Key = item.Key,
                    Value = item.Value,
                    Description = item.Description,
                    IsPublic = item.IsPublic,
                    Active = true,
                    Created = DateTime.UtcNow,
                    CreatedBy = "System"
                });
            }
        }
    }

    /// <summary>
    /// Sube (nunca baja) <c>App:MinSupportedClientVersion</c> y <c>App:LatestClientVersion</c> al piso del release vigente.
    /// El valor de la tabla SystemConfigs tiene prioridad sobre appsettings y el seed solo inserta si falta la fila,
    /// por lo que sin esto una actualizacion obligatoria nunca llegaria a una base ya existente.
    /// </summary>
    private async Task EnforceClientVersionFloorAsync()
    {
        foreach (var key in new[] { "App:MinSupportedClientVersion", "App:LatestClientVersion" })
        {
            var config = await _context.SystemConfigs.FirstOrDefaultAsync(c => c.Key == key);
            if (config == null) continue;

            var current = Version.TryParse(config.Value, out var parsed) ? parsed : new Version(0, 0, 0);
            if (current >= Version.Parse(ClientVersionFloor)) continue;

            config.Value = ClientVersionFloor;
            config.Active = true;
            _logger.LogInformation("{Key} elevada de {Old} a {New}.", key, current, ClientVersionFloor);
        }

        await _context.SaveChangesAsync();
    }

    private static string HashPassword(string password)
    {
        var salt = new byte[16];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
        }

        using var sha256 = SHA256.Create();
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        var saltedPassword = new byte[salt.Length + passwordBytes.Length];
        Buffer.BlockCopy(salt, 0, saltedPassword, 0, salt.Length);
        Buffer.BlockCopy(passwordBytes, 0, saltedPassword, salt.Length, passwordBytes.Length);
        var hash = Convert.ToBase64String(sha256.ComputeHash(saltedPassword));

        return $"{Convert.ToBase64String(salt)}:{hash}";
    }
}
