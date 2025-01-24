using Persistence;
using UseCases;
using WatchDog;
using WebApi.Modules.Authentication;
using WebApi.Modules.Feature;
using WebApi.Modules.Injection;
using WebApi.Modules.Swagger;
using WebApi.Modules.Watch;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", true, true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", true, true);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddFeature(builder.Configuration);
builder.Services.AddPersistenceServices(builder.Configuration);
builder.Services.AddApplicationServices();
builder.Services.AddInjection(builder.Configuration);
builder.Services.AddAuthentication(builder.Configuration);
builder.Services.AddSwagger();
// builder.Services.AddWatchDog(builder.Configuration);

var app = builder.Build();

app.UseDeveloperExceptionPage();
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "TimelyIO.Service.WebApi");
    }); //Enable middleware to serve swagger-ui (HTML, JS, CSS, etc.), specifying the Swagger JSON endpoint.
});

if (app.Environment.IsDevelopment())
{
}

// app.UseWatchDogExceptionLogger();
app.UseHttpsRedirection();
app.UseCors("policyPickSport");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// app.UseWatchDog(conf =>
// {
//     conf.WatchPageUsername = builder.Configuration["WatchDog:WatchPageUsername"];
//     conf.WatchPagePassword = builder.Configuration["WatchDog:WatchPagePassword"];
// });
app.UseDeveloperExceptionPage();
app.Run();

public partial class Program
{
};