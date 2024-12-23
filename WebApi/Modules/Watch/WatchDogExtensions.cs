using WatchDog;
using WatchDog.src.Enums;

namespace WebApi.Modules.Watch;

public static class WatchDogExtensions
{
    public static IServiceCollection AddWatchDog(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddWatchDogServices(opt =>
        {
            opt.SetExternalDbConnString = configuration.GetConnectionString(
                "PickSports"
            );
            opt.DbDriverOption = WatchDogDbDriverEnum.PostgreSql;
            opt.IsAutoClear = true;
            opt.ClearTimeSchedule = WatchDogAutoClearScheduleEnum.Monthly;
        });
        return services;
    }
}