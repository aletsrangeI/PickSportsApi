using Common;
using Logging;

namespace WebApi.Modules.Injection;

public static class InjectionExtension
{
    public static IServiceCollection AddInjection(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(configuration);
        services.AddScoped(typeof(IAppLogger<>), typeof(LoggerAdapter<>));
        return services;
    }
}