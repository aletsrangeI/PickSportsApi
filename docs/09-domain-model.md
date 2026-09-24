# 09 Domain Model - PickSports.API

## Diagrama Entidad-Relación

```mermaid
erDiagram
    Sport ||--o{ League : "contiene"
    League ||--o{ Season : "organiza"
    League ||--o{ Team : "compite"
    Season ||--o{ Week : "divide en"
    Week ||--o{ Match : "contiene"
    Team ||--o{ Match : "local/visitante"
    
    User ||--o{ QuinielaMember : "se inscribe"
    Quiniela ||--o{ QuinielaMember : "agrupa"
    User ||--o{ Quiniela : "administra"
    
    QuinielaMember ||--o{ Pick : "pronostica"
    Match ||--o{ Pick : "resultado"
    
    Week ||--o{ WeeklyAward : "reconoce en"
    QuinielaMember ||--o{ WeeklyAward : "gana"
    
    Pick ||--o{ PickAuditLog : "audita cambios"
    User ||--o{ PushSubscription : "recibe alertas"
```

## Entidades y Responsabilidades
1. **Sport:** Catálogo de deportes soportados (`SOCCER`, `NFL`).
2. **League:** Liga o torneo (`Liga MX`, `Premier League`, `Champions League`, `NFL`).
3. **Team:** Equipo o franquicia con identificador y logo de ESPN.
4. **Season & Week:** Temporadas y jornadas deportivas.
5. **Match:** Partido programado con fecha/hora UTC, marcador y estado (`SCHEDULED`, `IN_PROGRESS`, `FINAL`).
6. **Quiniela:** Torneo o quiniela creada por un usuario con código de invitación único.
7. **QuinielaMember:** Relación entre usuario y quiniela (rol `Admin` o `Member`).
8. **Pick:** Pronóstico (`HOME`, `AWAY`, `DRAW`) emitido por un miembro para un partido.
9. **WeeklyAward:** Galardón semanal calculado (MVP, Rey de Sorpresas, etc.).
10. **PickAuditLog:** Auditoría inmutable de cómo se generó cada pick (manual vs auto-llenado).
11. **PushSubscription:** Registro VAPID del navegador para notificaciones Web Push.
12. **EspnHealthLog:** Historial de peticiones a la API de ESPN para control de cuotas y bloqueos.
