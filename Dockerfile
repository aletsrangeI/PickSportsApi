# Etapa 1: Build y Publish con SDK de .NET
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Copiar archivos .csproj para aprovechar el caché de capas de Docker
COPY ["WebApi/WebApi.csproj", "WebApi/"]
COPY ["Common/Common.csproj", "Common/"]
COPY ["Logging/Logging.csproj", "Logging/"]
COPY ["Domain/Domain.csproj", "Domain/"]
COPY ["DTO/DTO.csproj", "DTO/"]
COPY ["Interface/Interface.csproj", "Interface/"]
COPY ["UseCases/UseCases.csproj", "UseCases/"]
COPY ["Validator/Validator.csproj", "Validator/"]
COPY ["Persistence/Persistence.csproj", "Persistence/"]

RUN dotnet restore "WebApi/WebApi.csproj"

# Copiar el código fuente completo y compilar la API
COPY . .
RUN dotnet publish "WebApi/WebApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Etapa 2: Runtime ligero (.NET ASP.NET en Alpine, ~100MB)
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true

# Preparar directorio de almacenamiento de avatares con permisos adecuados
RUN mkdir -p /app/uploads/avatars && chown -R $APP_UID /app/uploads

# Ejecutar como usuario sin privilegios por seguridad
USER $APP_UID

COPY --from=build /app/publish .

# Healthcheck interno del contenedor
HEALTHCHECK --interval=15s --timeout=3s --start-period=10s --retries=3 \
  CMD wget --no-verbose --tries=1 --spider http://localhost:8080/api/health || exit 1

ENTRYPOINT ["dotnet", "WebApi.dll"]
