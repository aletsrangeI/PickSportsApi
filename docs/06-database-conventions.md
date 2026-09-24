# 06 Database Conventions - PickSports.API

## Motor y Proveedor
- **Motor:** PostgreSQL 16
- **Proveedor EF Core:** `Npgsql.EntityFrameworkCore.PostgreSQL`
- **Configuración de Fechas:** Compatibilidad UTC con `AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true)` y tipos `timestamp with time zone`.

## Convenciones de Esquema y Mapeo
- **Tablas:** Nombradas en plural o según la entidad de dominio (`Users`, `Quinielas`, `Matches`, `Picks`, `WeeklyAwards`, etc.).
- **Columnas:** Nombradas en PascalCase en C# y mapeadas consistentemente en PostgreSQL.
- **Identificadores:** Claves primarias enteras autoincrementales (`Id`) o UUIDs según la entidad. Claves foráneas nombradas `{Entidad}Id` (ej. `QuinielaId`, `UserId`, `MatchId`).

## Restricciones Críticas e Índices
- **Unicidad de Pronóstico:** `UNIQUE("QuinielaId", "MemberId", "MatchId")` para prevenir pronósticos duplicados del mismo usuario en la misma quiniela.
- **Unicidad de Membresía:** `UNIQUE("QuinielaId", "UserId")` para garantizar una sola inscripción por quiniela.
- **Unicidad de Partido ESPN:** `UNIQUE("EspnGameId")` para sincronización idempotente sin duplicar partidos.
- **Código de Invitación:** `UNIQUE("InviteCode")` con índice para búsquedas instantáneas al unirse a una quiniela.
- **Índices de Búsqueda:** Índices en `KickoffTimeUtc`, `Status`, `WeekId` y `UserId`.

## Estrategia de Migraciones
- Las migraciones se gestionan exclusivamente mediante EF Core CLI:
  `dotnet ef migrations add <NombreMigracion> --project Persistence --startup-project WebApi`
  `dotnet ef database update --project Persistence --startup-project WebApi`
- En entorno local y producción, `DatabaseInitializer` ejecuta migraciones pendientes y siembra los catálogos base automáticamente.
