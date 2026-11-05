using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WebApi.BackgroundServices;
using WebApi.Modules.Workers;
using Xunit;

namespace PickSportsApi.UnitTests.WebApiTests;

public class BackgroundWorkersExtensionsTests
{
    private static ServiceCollection Register(params (string Key, string Value)[] values)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(v => v.Key, v => (string?)v.Value))
            .Build();

        var services = new ServiceCollection();
        services.AddBackgroundWorkers(config);
        return services;
    }

    private static bool HasWorker<T>(IServiceCollection services) =>
        services.Any(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(T));

    [Fact]
    public void AddBackgroundWorkers_KeysAbsent_RegistersBothWorkers()
    {
        // Producción no define las claves: conserva el comportamiento anterior.
        var services = Register();

        Assert.True(HasWorker<EspnLiveScoreBackgroundWorker>(services));
        Assert.True(HasWorker<QuinielaReminderBackgroundWorker>(services));
    }

    [Fact]
    public void AddBackgroundWorkers_BothTrue_RegistersBothWorkers()
    {
        var services = Register(
            (BackgroundWorkersExtensions.EspnLiveScoreKey, "true"),
            (BackgroundWorkersExtensions.QuinielaReminderKey, "true"));

        Assert.True(HasWorker<EspnLiveScoreBackgroundWorker>(services));
        Assert.True(HasWorker<QuinielaReminderBackgroundWorker>(services));
    }

    [Fact]
    public void AddBackgroundWorkers_BothFalse_RegistersNoWorker()
    {
        var services = Register(
            (BackgroundWorkersExtensions.EspnLiveScoreKey, "false"),
            (BackgroundWorkersExtensions.QuinielaReminderKey, "false"));

        Assert.False(HasWorker<EspnLiveScoreBackgroundWorker>(services));
        Assert.False(HasWorker<QuinielaReminderBackgroundWorker>(services));
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(IHostedService));
    }

    [Fact]
    public void AddBackgroundWorkers_OnlyEspnEnabled_RegistersOnlyEspnWorker()
    {
        var services = Register(
            (BackgroundWorkersExtensions.EspnLiveScoreKey, "true"),
            (BackgroundWorkersExtensions.QuinielaReminderKey, "false"));

        Assert.True(HasWorker<EspnLiveScoreBackgroundWorker>(services));
        Assert.False(HasWorker<QuinielaReminderBackgroundWorker>(services));
    }

    [Fact]
    public void AddBackgroundWorkers_OnlyOneKeyDisabled_KeepsTheOtherOnByDefault()
    {
        var services = Register((BackgroundWorkersExtensions.QuinielaReminderKey, "false"));

        Assert.True(HasWorker<EspnLiveScoreBackgroundWorker>(services));
        Assert.False(HasWorker<QuinielaReminderBackgroundWorker>(services));
    }

    [Theory]
    [InlineData("no")]
    [InlineData("0")]
    [InlineData("off")]
    [InlineData("quizas")]
    public void AddBackgroundWorkers_UnparseableValue_TurnsWorkerOffInsteadOfFailingOpen(string value)
    {
        // Un valor presente que no se puede interpretar nunca debe dejar un worker corriendo contra datos reales.
        var services = Register((BackgroundWorkersExtensions.EspnLiveScoreKey, value));

        Assert.False(HasWorker<EspnLiveScoreBackgroundWorker>(services));
        Assert.True(HasWorker<QuinielaReminderBackgroundWorker>(services)); // la otra clave sigue ausente = encendida
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AddBackgroundWorkers_BlankValue_IsTreatedAsAbsent(string value)
    {
        var services = Register((BackgroundWorkersExtensions.EspnLiveScoreKey, value));

        Assert.True(HasWorker<EspnLiveScoreBackgroundWorker>(services));
    }
}
