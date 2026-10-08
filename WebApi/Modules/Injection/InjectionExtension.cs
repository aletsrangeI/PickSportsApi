using Common;
using Interface.UseCases;
using Logging;
using UseCases.Broadcasters;
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

        // SPEC-015: extractor de señales de TV desde ligamx.net
        services.AddHttpClient(LigaMxBroadcastScraper.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
            client.DefaultRequestHeaders.TryAddWithoutValidation(
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "es-MX,es;q=0.9");
        });
        services.AddScoped<ILigaMxBroadcastScraper, LigaMxBroadcastScraper>();

        // ESPN parser y sync service
        services.AddScoped<EspnScoreboardParser>();
        services.AddScoped<IEspnSyncService, EspnSyncService>();

        return services;
    }
}