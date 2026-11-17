# Guía para Agentes de IA - PickSports.API (Backend)

## Objetivo
Definir cómo trabaja un agente de IA sobre el backend de PickSports.

## Alcance
Operaciones, Clean Architecture y estándares técnicos obligatorios en `PickSportsApi`.

## Cuándo consultar este documento
Siempre que un agente de IA vaya a leer, refactorizar o crear nuevo código en el Backend.

---

## Reglas Obligatorias para Agentes de IA

1. **Clean Architecture Estricta:**
   - La solución sigue la separación de capas:
     - `Domain/`: Entidades de negocio, enums y reglas de dominio puras.
     - `DTO/`: Objetos de transferencia de datos para entrada y salida.
     - `Interface/`: Contratos de repositorios y casos de uso.
     - `UseCases/`: Implementación de la lógica de negocio y flujos de aplicación.
     - `Persistence/`: Acceso a datos (DbContext, repositorios, mapeos, interceptores).
     - `Validator/`: Validaciones FluentValidation.
     - `WebApi/`: Controladores, middlewares, inyección de dependencias y configuración.
   - **PROHIBIDO**: Escribir lógica de negocio o cálculos de puntuación dentro de los controladores de `WebApi`.

2. **Arquitectura de Endpoints (Controladores Tradicionales):**
   - **ESTÁ ESTRICTAMENTE PROHIBIDO** el uso de Minimal Endpoints (`MapGroup`, `group.MapGet`, etc.).
   - Todo endpoint debe implementarse como un Controlador Tradicional heredando de `ControllerBase` dentro de `WebApi/Controllers/` con atributos estándar (`[ApiController]`, `[Route("api/[controller]")]`, `[HttpGet]`, `[HttpPost]`, etc.).

3. **Estrategia de Pruebas y Calidad:**
   - Flujos críticos (cálculo de puntuación, desempates en cascada, autollenado aleatorio seguro con auditoría, autenticación) deben contar con pruebas unitarias siguiendo el patrón **AAA** (Arrange, Act, Assert).
   - Mockear dependencias externas (`IUnitOfWork`, repositorios) con `Moq`.

4. **Reglas de Dominio de PickSports:**
   - Índice único compuesto en pronósticos: `UNIQUE("QuinielaId", "MemberId", "MatchId")`.
   - Autollenado no invasivo: `ON CONFLICT DO NOTHING`. Jamás sobreescribir picks humanos.
   - Cierre de jornada programado al arranque del primer partido.
   - Resiliencia obligatoria al consumir ESPN API (cabeceras reales, Polly retry y fallback en cascada).

5. **Reglas anti-regresion (cada una nace de un error real; no dependen del modelo que escriba el codigo):**
   - **Editar archivos solo con Edit/Write.** Prohibido reescribir codigo con `Get-Content`/`Set-Content`/`Out-File` de PowerShell: cambian la codificacion y dejan texto roto (`Futbol` -> `FÃºtbol`). Ademas `R` es alias de `Invoke-History`. `SourceHygieneTests` falla si un `.cs` contiene texto doble codificado.
   - **Codigos de formato de eliminatoria solo desde `Domain.Common.KnockoutFormatCodes`.** Nunca escribas `"NHL_BEST_OF_7"` ni similares como literal; un modelo ya invento `NHL_BEST_OF_SEVEN`.
   - **Toda entidad nueva con `Active = true` al crearla.** `BaseAuditableEntity.Active` vale `false` por defecto y los repositorios filtran por `Active`: sin eso la fila existe pero nadie la lee. Los mocks de `IUnitOfWork` no lo detectan; cubrelo con una prueba contra el repositorio real sobre EF InMemory (ver `EspnKnockoutSyncPersistenceTests`).
   - **Anulabilidad coherente en arreglos y colecciones anonimas:** `CS8619` es error en `.editorconfig`; usa `(string?)` en todos los elementos.
   - **Un test que mockea justo lo que debe probar no prueba nada:** si cambias una regla de negocio, verifica que el test falla antes de arreglarla (mutacion rapida).
