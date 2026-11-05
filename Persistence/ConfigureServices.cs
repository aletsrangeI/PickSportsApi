using Interface.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Persistence.Context;
using Persistence.Initialization;
using Persistence.Interceptors;
using Persistence.Repositories;

namespace Persistence;

public static class ConfigureServices
{
    public static IServiceCollection AddPersistenceServices(this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = ConnectionStringResolver.Resolve(configuration);

        // Los valores de parámetros SQL (que incluyen contraseñas y hashes) solo se registran si se pide
        // explícitamente; el valor por defecto es false y solo Development lo activa.
        var enableSensitiveDataLogging =
            bool.TryParse(configuration["Logging:EnableSensitiveDataLogging"], out var sensitive) && sensitive;

        services.AddScoped<AuditableEntitySaveChangesInterceptor>();
        services.AddDbContext<ApplicationDbContext>(
            options =>
            {
                options.UseNpgsql(
                    connectionString,
                    builder =>
                        builder.MigrationsAssembly(
                            typeof(ApplicationDbContext).Assembly.FullName
                        )
                );
                options.EnableSensitiveDataLogging(enableSensitiveDataLogging);
            }
        );

        // Repositorios
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ISportRepository, SportRepository>();
        services.AddScoped<ILeagueRepository, LeagueRepository>();
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<ISeasonRepository, SeasonRepository>();
        services.AddScoped<IWeekRepository, WeekRepository>();
        services.AddScoped<IMatchRepository, MatchRepository>();
        services.AddScoped<IQuinielaRepository, QuinielaRepository>();
        services.AddScoped<IQuinielaMemberRepository, QuinielaMemberRepository>();
        services.AddScoped<IPickRepository, PickRepository>();
        services.AddScoped<IWeeklyAwardRepository, WeeklyAwardRepository>();
        services.AddScoped<IPickAuditLogRepository, PickAuditLogRepository>();
        services.AddScoped<IPushSubscriptionRepository, PushSubscriptionRepository>();
        services.AddScoped<IPushNotificationLogRepository, PushNotificationLogRepository>();
        services.AddScoped<IEspnHealthLogRepository, EspnHealthLogRepository>();
        services.AddScoped<ICustomEventRepository, CustomEventRepository>();

        // Dynamic Form Repositories
        services.AddScoped<ICatalogoRepository, CatalogoRepository>();
        services.AddScoped<IContenidoCatalogoRepository, ContenidoCatalogoRepository>();
        services.AddScoped<IFormFieldRepository, FormFieldRepository>();

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<DatabaseInitializer>();

        return services;
    }
}