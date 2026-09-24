using Persistence;
using Persistence.Initialization;
using Scalar.AspNetCore;
using UseCases;
using WebApi.Modules.Authentication;
using WebApi.Modules.Feature;
using WebApi.Modules.Injection;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", true, true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", true, true)
    .AddUserSecrets<Program>(true)
    .AddEnvironmentVariables();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddFeature(builder.Configuration);
builder.Services.AddPersistenceServices(builder.Configuration);
builder.Services.AddApplicationServices();
builder.Services.AddInjection(builder.Configuration);
builder.Services.AddAuthentication(builder.Configuration);
builder.Services.AddHostedService<WebApi.BackgroundServices.EspnLiveScoreBackgroundWorker>();
builder.Services.AddOpenApi(options =>
{
    options.AddOperationTransformer((operation, context, cancellationToken) =>
    {
        if (context.Description.ActionDescriptor is Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor actionDescriptor)
        {
            var explicitName = actionDescriptor.AttributeRouteInfo?.Name;
            if (!string.IsNullOrEmpty(explicitName))
            {
                operation.OperationId = explicitName;
            }
            else
            {
                var actionName = actionDescriptor.ActionName;
                if (actionName.EndsWith("Async") && actionName.Length > 5)
                {
                    actionName = actionName.Substring(0, actionName.Length - 5) + "_Async";
                }
                operation.OperationId = $"{actionDescriptor.ControllerName}_{actionName}";
            }
        }
        return Task.CompletedTask;
    });
});

var app = builder.Build();

app.UseDeveloperExceptionPage();
app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options
        .WithTitle("PickSports API Reference")
        .WithTheme(ScalarTheme.Alternate)
        .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
});

app.UseHttpsRedirection();
app.UseCors("policyPickSport");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Endpoint de salud liviano y anónimo para Healthcheck de Docker Compose y uptime monitoring
app.MapGet("/api/health", () => Results.Ok(new
{
    status = "ok",
    service = "PickSportsApi",
    environment = app.Environment.EnvironmentName,
    utc = DateTime.UtcNow
})).AllowAnonymous();

// Sembrado inicial de base de datos
using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    try
    {
        await initializer.SeedAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Could not run DatabaseInitializer on startup (DB connection may be pending).");
    }
}

app.Run();

public partial class Program
{
};