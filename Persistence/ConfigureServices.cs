using Interface.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Persistence.Context;
using Persistence.Interceptors;
using Persistence.Repositories;

namespace Persistence;

public static class ConfigureServices
{
    public static IServiceCollection AddPersistenceServices(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<AuditableEntitySaveChangesInterceptor>();
        services.AddDbContext<ApplicationDbContext>(
            options =>
                options.UseNpgsql(
                    configuration.GetConnectionString("PickSports"),
                    builder =>
                        builder.MigrationsAssembly(
                            typeof(ApplicationDbContext).Assembly.FullName
                        )
                )
        );

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICatalogoRepository, CatalogoRepository>();
        services.AddScoped<IContenidoCatalogoRepository, ContenidoCatalogoRepository>();
        services.AddScoped<IEquipoLigaRepository, EquipoLigaRepository>();
        services.AddScoped<IFormFieldRepository, FormFieldRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        return services;
    }
}