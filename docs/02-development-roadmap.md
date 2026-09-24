# 02 Development Roadmap - PickSports.API

## Fases y Especificaciones (Spec-Kit)

### SPEC-001: Dominio, Persistencia y Migraciones EF Core [COMPLETADO]
- 14 Entidades de Dominio en inglés (`Sport`, `League`, `Team`, `Season`, `Week`, `Match`, `Quiniela`, `QuinielaMember`, `Pick`, `WeeklyAward`, `PickAuditLog`, `PushSubscription`, `EspnHealthLog`).
- Configuraciones Fluent API con restricciones de unicidad e índices para rendimiento.
- `GenericRepository<T>`, repositorios especializados (`PickRepository` con Upsert y auto-llenado seguro) y `UnitOfWork`.
- `DatabaseInitializer` con seeding de deportes, ligas y administrador base.
- Migración inicial generada y probada (`InitialPickSportsSchema`).
- Suite de pruebas unitarias xUnit (`PickSportsApi.UnitTests`).

### SPEC-002: Autenticación, Usuarios y Multi-Quiniela [SIGUIENTE]
- Endpoints de registro, login JWT y consulta de perfil (`AuthController`).
- Endpoints de Quinielas: creación, membresía, unirse por código alfanumérico (`QuinielasController`).
- Frontend: Selector de Quinielas activo en Header, vistas de bienvenida y unirse a quiniela.

### SPEC-003: Integración ESPN, Calendario y Ligas
- Cliente resiliente HTTP con circuit breaker y User-Agent rotativo para ESPN API pública.
- Sincronización automática de equipos, partidos, fechas, marcadores y estados en vivo.
- Background worker `EspnSyncWorker` y logging de salud en `EspnHealthLog`.

### SPEC-004: Pronósticos, Bloqueo Automático y Auto-Llenado Aleatorio
- Endpoints para consultar partidos de jornada activa y registrar/editar picks individuales o masivos.
- Bloqueo estricto a la hora exacta de inicio de cada partido (`KickoffTimeUtc <= UtcNow`).
- Auto-llenado aleatorio seguro con idempotencia (`ON CONFLICT DO NOTHING`) y auditoría en `PickAuditLog`.

### SPEC-005: Motor de Puntuación, Desempates y Premios Semanales
- Cálculo automático de puntos por jornada y tabla acumulada.
- Desempates en cascada: 1° Aciertos directos -> 2° Sorpresas (upsets <= 25%) -> 3° Menos Humillaciones recibidas.
- Asignación de premios semanales (MVP, Rey de Sorpresas, El Humillado, Víctima del Somnífero 0-0, Rey del Empate Fallido).
- Generador de resumen para portapapeles / WhatsApp.

### SPEC-006: Notificaciones Web Push y Background Workers
- Integración de Web Push (VAPID) en segundo plano.
- Alertas de cierre de jornada (2h y 15m antes del primer partido).
- Notificaciones de acierto al terminar cada partido.
- Reporte semanal de resultados.

### SPEC-007: Migración Seamless de Quiniela Activa desde Google Sheets / XLSX
- Herramienta exclusiva para el Administrador para migrar una quiniela en curso desde XLSX.
- Mapeo automático de participantes, calendario oficial con ESPN GameId y 756+ pronósticos históricos.
- Validación matemática estricta contra hoja `Standings` (oráculo de consistencia) mediante `ScoringEngine`.
- Transición en vivo garantizando continuidad operativa: jornadas pasadas calificadas y jornada actual abierta para picks.
