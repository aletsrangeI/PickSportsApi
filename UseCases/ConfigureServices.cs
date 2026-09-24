using System.Reflection;
using Common.Security;
using Interface.Security;
using Interface.UseCases;
using Microsoft.Extensions.DependencyInjection;
using UseCases.Auth;
using UseCases.Catalogos;
using UseCases.ContenidoCatalogos;
using UseCases.FormFields;
using UseCases.Picks;
using UseCases.Quinielas;
using UseCases.Security;
using UseCases.Users;
using Validator;
using Validator.Auth;
using Validator.Pick;
using Validator.Quiniela;

namespace UseCases;

public static class ConfigureServices
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Mapping (Riok.Mapperly - Zero reflection, high performance)
        services.AddSingleton<Interface.Mapping.IAppMapper, UseCases.Common.Mapping.AppMapper>();
        // Security & Transversal
        services.AddSingleton<PasswordHasher>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

        // Application Use Cases
        services.AddScoped<IAuthApplication, AuthApplication>();
        services.AddScoped<IQuinielaApplication, QuinielaApplication>();
        services.AddScoped<IUsersApplication, UsersApplication>();
        services.AddScoped<IContenidoCatalogoApplication, ContenidoCatalogosApplication>();
        services.AddScoped<IFormFieldApplication, FormFieldApplication>();
        services.AddScoped<ICatalogosApplication, CatalogosApplication>();
        services.AddScoped<IPickApplication, PickApplication>();
        services.AddSingleton<UseCases.Scoring.ScoringEngine>();
        services.AddScoped<IScoringApplication, UseCases.Scoring.ScoringApplication>();
        services.AddScoped<IWhatsAppReportService, UseCases.Reports.WhatsAppReportService>();
        services.AddScoped<IWebPushNotificationService, UseCases.Notifications.WebPushNotificationService>();
        services.AddScoped<INotificationsApplication, UseCases.Notifications.NotificationsApplication>();
        services.AddScoped<IXlsxParserService, UseCases.Migration.ClosedXmlParserService>();
        services.AddScoped<IQuinielaMigrationService, UseCases.Migration.QuinielaMigrationService>();

        // Validators
        services.AddTransient<RegisterRequestDtoValidator>();
        services.AddTransient<LoginRequestDtoValidator>();
        services.AddTransient<CreateQuinielaDtoValidator>();
        services.AddTransient<JoinQuinielaDtoValidator>();
        services.AddTransient<SubmitPickDtoValidator>();
        services.AddTransient<UsersDTOValidator>();
        services.AddTransient<ContenidoCatalogosDTOValidator>();
        services.AddTransient<CatalogosDTOValidator>();
        services.AddTransient<FormFieldDTOValidator>();
        services.AddTransient<Validator.Notifications.PushSubscriptionRequestValidator>();

        return services;
    }
}