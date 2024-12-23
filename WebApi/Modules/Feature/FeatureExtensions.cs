using System.Text.Json.Serialization;

namespace WebApi.Modules.Feature;

public static class FeatureExtensions
{
    public static IServiceCollection AddFeature(this IServiceCollection services, IConfiguration configuration)
    {
        var myPolicy = "policyPickSport";

        services.AddCors(options => options.AddPolicy(myPolicy, builder => builder
            .SetIsOriginAllowed(_ => true) // Permitir cualquier origen
            .AllowAnyHeader()
            .AllowAnyMethod()));
        services.AddControllers().AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });


        return services;
    }
}