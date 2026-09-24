# 03 Coding Standards - PickSports.API

## Convenciones Generales
- **C# Version:** Se utilizan características modernas de C# (.NET 8 / 10).
- **Controladores Tradicionales:** Se exige estrictamente el uso de `ControllerBase` con atributos (`[ApiController]`, `[Route("api/[controller]")]`, `[HttpGet]`, `[HttpPost]`, etc.). **PROHIBIDO** el uso de Minimal APIs / Minimal Endpoints para lógica de negocio.
- **Async/Await:** Todo acceso a base de datos y llamadas I/O debe ser asíncrono, usando el sufijo `Async` (ej. `GetByIdAsync`, `UpsertPickAsync`).
- **CancellationToken:** Pasar `CancellationToken` a todos los métodos asíncronos desde los controladores hasta los repositorios y llamadas HTTP externas.

## Estructura por Capas (Clean Architecture)
- **Domain:** Entidades puras en inglés (`PascalCase`), heredando de `BaseEntity` o `BaseAuditableEntity`. No contienen lógica de persistencia ni referencias a frameworks web.
- **Persistence:** Fluent API en `Persistence/Configurations/` para mapeo explícito de tipos, claves foráneas, índices e integridad referencial.
- **Repositories:** Las interfaces residen en `Interface/Persistence` (`I{Entidad}Repository`) y las implementaciones en `Persistence/Repositories`.
- **UseCases:** Servicios de aplicación nombrados `{Entidad}Service` o `{Feature}UseCase`, orquestando la lógica con `IUnitOfWork`.
- **DTOs:** Objetos de transferencia en el proyecto `DTO`, separados por entidad o acción (`RegisterRequestDto`, `PickSubmissionDto`, `StandingsResponseDto`).
- **Validators:** Validadores en el proyecto `Validator` utilizando FluentValidation (`AbstractValidator<T>`).

## Naming Conventions
- **Clases e Interfaces:** `PascalCase`. Interfaces con prefijo `I` (`IUnitOfWork`, `IPickRepository`).
- **Propiedades de Entidades y DTOs:** `PascalCase` en C#, pero serializados a `camelCase` en JSON.
- **Variables locales y parámetros:** `camelCase`.
- **Campos privados:** Prefijo `_` con `camelCase` (`_unitOfWork`, `_logger`).

## Seguridad y Manejo de Secretos
- **PROHIBIDO** almacenar contraseñas, secretos JWT o VAPID en archivos de configuración versionados.
- Desarrollo local: `dotnet user-secrets`.
- Producción / Homelab: Infisical con inyección `.env` y variables con doble guión bajo (`__`).
