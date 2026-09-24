# 07 Testing - PickSports.API

## Filosofía de Pruebas
Cada cambio en la lógica de negocio, cálculos de puntuación, desempates o reglas de bloqueo debe estar respaldado por pruebas automatizadas antes de fusionarse a las ramas principales (`Development` / `main`). El pipeline de CI/CD falla si alguna prueba no pasa.

## Proyectos y Herramientas
- **Proyecto de Pruebas:** `PickSportsApi.UnitTests`
- **Framework de Pruebas:** xUnit
- **Aserciones:** FluentAssertions / xUnit standard assertions
- **Mocking y Persistencia:** `Microsoft.EntityFrameworkCore.InMemory` versión compatible (9.0.0 en .NET 8) y Moq para servicios desacoplados.

## Cobertura Prioritaria
1. **Reglas de Bloqueo de Pronósticos:**
   - Intentar guardar un pick después del kickoff debe arrojar error de validación o excepción de negocio.
   - Guardar un pick antes del kickoff debe persistir exitosamente.
2. **Cálculo de Desempates en Cascada:**
   - 1° Aciertos directos.
   - 2° Aciertos en sorpresas (upset con cuota de acierto global <= 25%).
   - 3° Menos humillaciones sufridas (partidos donde el participante fue el único o minoría en fallar).
3. **Auto-llenado Seguro:**
   - Un usuario con picks existentes no debe ser sobreescrito por el proceso de auto-llenado.
   - Todo auto-llenado debe generar registro en `PickAuditLog`.

## Ejecución de Pruebas
```powershell
dotnet test PickSportsApi\PickSportsApi.sln -c Release --verbosity normal
```
