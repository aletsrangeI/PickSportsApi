using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Persistence;
using UseCases;
using WebApi.Modules.Authentication;
using WebApi.Modules.Feature;
using WebApi.Modules.Injection;
using Xunit;

namespace PickSportsApi.UnitTests.UseCasesTests;

public class DependencyInjectionValidationTests
{
    [Fact]
    public void BuildServiceProvider_WithValidateScopes_ShouldNotThrow()
    {
        // Arrange
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());

        var inMemorySettings = new Dictionary<string, string?>
        {
            { "ConnectionStrings:PickSports", "Server=localhost;Port=5432;Database=test;User Id=test;Password=test;" },
            { "Config:Secret", "eb52d4dded37971ce40e757f2ab4964d3a1cec349e3bd263a960bc197baa8bab" },
            { "Config:Issuer", "OrionSys" },
            { "Config:Audience", "OrionSys" },
            { "Vapid:Subject", "mailto:admin@picksports.orionsys.net" },
            { "Vapid:PublicKey", "BPpp9qlf8_PFdC6WUBJmVnq8LBjA4opQDtJiZ7A0BwExzuPQHA1QL-sR9z8w-wXzit7NdOskfiDYJDBY1WVWYxk" },
            { "Vapid:PrivateKey", "GksRdldQVuu2b1lmVDkbrduMaL6xWVnckjtCR0913ew" }
        };

        builder.Configuration.AddInMemoryCollection(inMemorySettings);

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddFeature(builder.Configuration);
        builder.Services.AddPersistenceServices(builder.Configuration);
        builder.Services.AddApplicationServices();
        builder.Services.AddInjection(builder.Configuration);
        builder.Services.AddAuthentication(builder.Configuration);
        builder.Services.AddHostedService<WebApi.BackgroundServices.EspnLiveScoreBackgroundWorker>();

        // Act & Assert: Build with ValidateScopes and ValidateOnBuild enabled
        var sp = builder.Services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        Assert.NotNull(sp);

        // Verify that the singleton IHostedService can be resolved without scope exception
        var hostedServices = sp.GetServices<IHostedService>();
        Assert.NotEmpty(hostedServices);
    }
}
