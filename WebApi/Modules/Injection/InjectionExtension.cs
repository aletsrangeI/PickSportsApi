using Common;
using Interface.UseCases;
using Logging;
using UseCases.Espn;

namespace WebApi.Modules.Injection;

public static class InjectionExtension
{
    public static IServiceCollection AddInjection(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(configuration);
        services.AddSingleton(typeof(IAppLogger<>), typeof(LoggerAdapter<>));

        // ESPN: handler de cabeceras Chrome para evadir WAF (403)
        services.AddTransient<EspnHttpClientHandler>();
        services.AddHttpClient("EspnClient")
            .AddHttpMessageHandler<EspnHttpClientHandler>();

        // ESPN parser y sync service
        services.AddScoped<EspnScoreboardParser>();
        services.AddScoped<IEspnSyncService, EspnSyncService>();

        return services;
    }
}