# 08 Definition of Done (DoD) - PickSports.API

Un requerimiento, spec o issue se considera terminado (**Done**) únicamente si cumple con los siguientes criterios:

1. **Código y Estilo:**
   - La funcionalidad está implementada en controladores tradicionales (`ControllerBase`), casos de uso y repositorios según Clean Architecture.
   - No se utilizan minimal endpoints para lógica de negocio.
   - Se respetan las convenciones de nomenclatura (PascalCase en C#, camelCase en JSON).
   - Manejo exhaustivo de excepciones y validaciones con FluentValidation.

2. **Compilación y Pruebas:**
   - La solución `PickSportsApi.sln` compila limpiamente sin errores ni advertencias bloqueantes.
   - Las pruebas unitarias cubren los nuevos casos de uso y pasan al 100% con `dotnet test`.

3. **Base de Datos y Persistencia:**
   - Si hubo cambios en el modelo de entidades, la migración de EF Core ha sido generada y probada.
   - Las restricciones de unicidad e índices están declarados en Fluent API.

4. **Documentación:**
   - Si se agregaron endpoints, están documentados en OpenAPI / Swagger con sus códigos de respuesta esperados.
   - Los archivos de spec (`spec.md`, `plan.md`, `tasks.md`) correspondientes están actualizados.

5. **CI/CD y Despliegue:**
   - El Dockerfile compila correctamente en entorno Alpine.
   - El endpoint `/api/health` responde status `ok`.
