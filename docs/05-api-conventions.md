# 05 API Conventions - PickSports.API

## Estilo de Rutas y Endpoints
- **Base Route:** Todas las rutas inician con `/api/` y el nombre del recurso en plural o sustantivo descriptivo (`/api/auth`, `/api/quinielas`, `/api/picks`, `/api/matches`).
- **Controladores Tradicionales:** Heredan de `ControllerBase` y usan atributos explícitos:
  ```csharp
  [ApiController]
  [Route("api/[controller]")]
  public class QuinielasController : ControllerBase
  ```
- **Verbos HTTP:**
  - `GET`: Consulta de recursos (idempotente, sin efectos secundarios).
  - `POST`: Creación de nuevos recursos, registro de pronósticos o acciones operativas.
  - `PUT`: Actualización completa de recursos.
  - `PATCH`: Modificación parcial de recursos.
  - `DELETE`: Eliminación o desactivación lógica de recursos.

## Respuestas y Códigos de Estado
- `200 OK`: Operación exitosa con datos en el cuerpo.
- `201 Created`: Creación exitosa de recurso con encabezado `Location` o id devuelto.
- `400 Bad Request`: Error de validación (usando estándar RFC 7807 `ValidationProblemDetails`).
- `401 Unauthorized`: Token JWT ausente, expirado o inválido.
- `403 Forbidden`: Usuario autenticado pero sin permisos sobre la quiniela o rol solicitado.
- `404 Not Found`: Recurso no encontrado.
- `409 Conflict`: Violación de restricción única (ej. código de quiniela duplicado, usuario ya inscrito).
- `422 Unprocessable Entity`: Regla de negocio violada (ej. intentar guardar un pick cuando el partido ya comenzó).
- `500 Internal Server Error`: Excepción no controlada registrada en logs.

## Endpoint de Salud
- `/api/health`: Endpoint público y anónimo utilizado por Docker Compose y scripts de deploy para verificar el estado del servicio:
  ```json
  {
    "status": "ok",
    "service": "PickSportsApi",
    "environment": "Production",
    "utc": "2026-09-23T19:50:00Z"
  }
  ```
