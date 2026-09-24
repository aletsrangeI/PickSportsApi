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
