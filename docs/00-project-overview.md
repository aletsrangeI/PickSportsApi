# 00 Project Overview - PickSports.API

## Objetivo del Proyecto
PickSports.API es el backend para la plataforma de quinielas deportivas multideporte y multiquiniela de OrionSys, facilitando la administración de ligas (Liga MX, Premier League, Champions League, NFL), temporadas, jornadas, partidos con sincronización en vivo vía ESPN API, pronósticos con bloqueo automático, cálculo de puntuación, desempates en cascada, premios semanales y notificaciones Web Push.

## Tecnologías
- **Framework:** .NET 8 / .NET 10 WebApi
- **Arquitectura:** Clean Architecture
- **Controladores:** Tradicionales `ControllerBase` (NO minimal endpoints)
- **ORM:** Entity Framework Core con Npgsql
- **Base de Datos:** PostgreSQL
- **Autenticación:** JWT (JSON Web Tokens)
- **Documentación de API:** OpenAPI con Swagger / Scalar
- **Notificaciones:** Web Push (VAPID) y exportador WhatsApp
- **Background Workers:** .NET `BackgroundService` para sondeo de ESPN y auto-llenado

## Solución y Proyectos
La solución `PickSportsApi.sln` está dividida en las siguientes capas (Clean Architecture):
- **Domain:** Entidades de negocio (`Sport`, `League`, `Team`, `Season`, `Week`, `Match`, `Quiniela`, `QuinielaMember`, `Pick`, `WeeklyAward`, `PickAuditLog`, `PushSubscription`, `EspnHealthLog`) y abstracciones base (`BaseEntity`, `BaseAuditableEntity`).
- **UseCases:** Casos de uso y orquestación de negocio (Servicios de Aplicación).
- **Interface:** Contratos e interfaces para repositorios, UnitOfWork y servicios de aplicación.
- **DTO:** Data Transfer Objects para comunicación externa y entre capas.
- **Validator:** Validación de entradas con FluentValidation.
- **Persistence:** Acceso a datos con EF Core, `ApplicationDbContext`, configuraciones Fluent API, Repositorios especializados y UnitOfWork.
- **WebApi:** Controladores REST (`ControllerBase`), configuración de Dependency Injection, middlewares y endpoints de salud.
- **Common / Logging:** Utilidades compartidas y registro de eventos.
- **PickSportsApi.UnitTests:** Pruebas unitarias de dominio, persistencia y casos de uso con xUnit y EF Core InMemory.

## Cómo iniciar el proyecto en desarrollo
1. Configurar la cadena de conexión `picksports_db` mediante `dotnet user-secrets` hacia PostgreSQL.
2. Ejecutar las migraciones con `dotnet ef database update --project Persistence --startup-project WebApi`.
3. Ejecutar la WebApi: `dotnet run --project WebApi`.
4. Acceder al Scalar API Reference en `http://localhost:5172/scalar/v1` (o en puerto HTTPS).
