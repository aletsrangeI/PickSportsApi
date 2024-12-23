# Utiliza una imagen base para el SDK de .NET 8
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /app

# Copia los archivos .csproj y restaura las dependencias
COPY ["WebApi/WebApi.csproj", "WebApi/"]

RUN dotnet restore "WebApi/WebApi.csproj"

# Copia el resto del código fuente y compila la aplicación
COPY . .
WORKDIR /app/WebApi
RUN dotnet publish -c Release -o out

# Utiliza una imagen base para el runtime de ASP.NET Core
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
COPY --from=build-env /app/WebApi/out .

# Define el punto de entrada de la aplicación
ENTRYPOINT ["dotnet", "WebApi.dll"]
