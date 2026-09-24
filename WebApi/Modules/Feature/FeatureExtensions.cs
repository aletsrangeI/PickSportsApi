using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebApi.Modules.Feature;

public class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return DateTime.SpecifyKind(reader.GetDateTime(), DateTimeKind.Utc);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        var utc = DateTime.SpecifyKind(value, DateTimeKind.Utc);
        writer.WriteStringValue(utc.ToString("yyyy-MM-ddTHH:mm:ssZ"));
    }
}

public class NullableUtcDateTimeConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        return DateTime.SpecifyKind(reader.GetDateTime(), DateTimeKind.Utc);
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (!value.HasValue)
        {
            writer.WriteNullValue();
            return;
        }
        var utc = DateTime.SpecifyKind(value.Value, DateTimeKind.Utc);
        writer.WriteStringValue(utc.ToString("yyyy-MM-ddTHH:mm:ssZ"));
    }
}

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
            options.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter());
            options.JsonSerializerOptions.Converters.Add(new NullableUtcDateTimeConverter());
        });

        return services;
    }
}