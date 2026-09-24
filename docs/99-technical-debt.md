# 99 Technical Debt - PickSports.API

## Registro de Deuda Técnica y Oportunidades de Mejora

### 1. Migración Gradual de AutoMapper (Completado)
- **Estado:** Migración completada. Se eliminó AutoMapper y la vulnerabilidad `GHSA-rvv3-g6hj-g44x` (NU1903).
- **Acción:** Reemplazado por `Riok.Mapperly 4.3.1` mediante `IAppMapper` y `AppMapper`, idéntico al estándar de MesaFacil.

### 2. Resiliencia y Rate Limiting de ESPN API
- **Estado:** En Google Apps Script ocurrían bloqueos por peticiones intensivas concurrentes.
- **Acción:** En SPEC-003, asegurar que `HttpClient` use rotación de User-Agent, sondeo con retroceso exponencial (jittered backoff) y caché distribuida/en memoria para no exceder 1 petición cada 30 segundos por liga.

### 3. Migración de .NET 8 a .NET 10 Completa (Completado)
- **Estado:** Migración completada. Los 10 proyectos de la solución compilan y ejecutan pruebas en `net10.0`.
- **Acción:** Unificado TargetFramework a `net10.0` y modernizada la documentación interactiva con `Scalar.AspNetCore` y `Microsoft.AspNetCore.OpenApi` en sustitución de `Swashbuckle.AspNetCore`.
