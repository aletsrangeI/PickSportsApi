using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Persistence;
using Persistence.Context;
using Xunit;

namespace PickSportsApi.UnitTests.PersistenceTests;

public class ConnectionStringAndLoggingTests
{
    private const string Cs = "Server=localhost;Port=5432;Database=picksports_test;User Id=u;Password=p;";

    private static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(v => v.Key, v => v.Value))
            .Build();

    // ---------- ConnectionStringResolver ----------

    [Fact]
    public void Resolve_PrefersEnvironmentVariableOverConfiguration()
    {
        var config = Config(("ConnectionStrings:PickSports", "config"));

        var result = ConnectionStringResolver.Resolve(config, n => n == "ConnectionStrings__PickSports" ? "env" : null);

        Assert.Equal("env", result);
    }

    [Fact]
    public void Resolve_FallsBackToLegacyAliasOnlyWhenPrimaryIsMissing()
    {
        var onlyAlias = Config(("ConnectionStrings:picksports_db", "alias"));
        var both = Config(("ConnectionStrings:PickSports", "primary"), ("ConnectionStrings:picksports_db", "alias"));

        Assert.Equal("alias", ConnectionStringResolver.Resolve(onlyAlias, _ => null));
        Assert.Equal("primary", ConnectionStringResolver.Resolve(both, _ => null));
    }

    [Fact]
    public void Resolve_IgnoresBlankValues_AndReturnsNullWhenNothingIsDefined()
    {
        var config = Config(("ConnectionStrings:PickSports", "   "));

        Assert.Null(ConnectionStringResolver.Resolve(config, _ => ""));
    }

    // ---------- EnableSensitiveDataLogging ----------

    private static bool? SensitiveLoggingEnabled(params (string Key, string? Value)[] values)
    {
        var services = new ServiceCollection();
        services.AddPersistenceServices(Config(values.Append(("ConnectionStrings:PickSports", Cs)).ToArray()));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<ApplicationDbContext>>();
        return options.FindExtension<CoreOptionsExtension>()?.IsSensitiveDataLoggingEnabled;
    }

    [Fact]
    public void SensitiveDataLogging_IsOffByDefault()
    {
        Assert.False(SensitiveLoggingEnabled());
    }

    [Fact]
    public void SensitiveDataLogging_IsOnlyOnWhenExplicitlyEnabled()
    {
        Assert.True(SensitiveLoggingEnabled(("Logging:EnableSensitiveDataLogging", "true")));
        Assert.False(SensitiveLoggingEnabled(("Logging:EnableSensitiveDataLogging", "false")));
        Assert.False(SensitiveLoggingEnabled(("Logging:EnableSensitiveDataLogging", "cualquier-cosa")));
    }
}
