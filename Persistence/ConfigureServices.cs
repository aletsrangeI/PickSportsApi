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
        var connectionString = new[]
        {
            Environment.GetEnvironmentVariable("ConnectionStrings__PickSports"),
            Environment.GetEnvironmentVariable("ConnectionStrings__picksports_db"),
            configuration.GetConnectionString("PickSports"),
            configuration.GetConnectionString("picksports_db"),
            configuration["ConnectionStrings:PickSports"],
            configuration["ConnectionStrings:picksports_db"]
        }.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));

        services.AddScoped<AuditableEntitySaveChangesInterceptor>();
        services.AddDbContext<ApplicationDbContext>(
            options =>
                options.UseNpgsql(
                    connectionString,
                    builder =>
                        builder.MigrationsAssembly(
                            typeof(ApplicationDbContext).Assembly.FullName
                        )
                )
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
        services.AddScoped<IEspnHealthLogRepository, EspnHealthLogRepository>();

        // Dynamic Form Repositories
        services.AddScoped<ICatalogoRepository, CatalogoRepository>();
        services.AddScoped<IContenidoCatalogoRepository, ContenidoCatalogoRepository>();
        services.AddScoped<IFormFieldRepository, FormFieldRepository>();

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<DatabaseInitializer>();

        return services;
    }
}