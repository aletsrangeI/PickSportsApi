using System.Reflection;
using Interface.UseCases;
using Microsoft.Extensions.DependencyInjection;
using UseCases.Catalogos;
using UseCases.ContenidoCatalogos;
using UseCases.EquipoLigas;
using UseCases.FormFields;
using UseCases.Users;
using Validator;

namespace UseCases;

public static class ConfigureServices
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddAutoMapper(Assembly.GetExecutingAssembly());
        services.AddScoped<IUsersApplication, UsersApplication>();
        services.AddScoped<IContenidoCatalogoApplication, ContenidoCatalogosApplication>();
        services.AddScoped<IEquipoLigaApplication, EquipoLigasApplication>();
        services.AddScoped<IFormFieldApplication, FormFieldApplication>();
        services.AddScoped<ICatalogosApplication, CatalogosApplication>();
        services.AddTransient<UsersDTOValidator>();
        services.AddTransient<ContenidoCatalogosDTOValidator>();
        services.AddTransient<CatalogosDTOValidator>();
        services.AddTransient<EquipoLigaDTOValidator>();
        services.AddTransient<FormFieldDTOValidator>();

        return services;
    }
}