# Reglas de Dominio y Negocio - PickSportsApi

## 1. Reglas de Pronósticos (Picks)
- **Opciones por Deporte:**
  - Soccer: `[LocalAbbr]`, `[AwayAbbr]` o `'EMPATE'`.
  - NFL: `[LocalAbbr]` o `[AwayAbbr]`.
- **Estrategia de Guardado:**
  - *Humano (Manual):* Operación **Upsert** (`InsertOrUpdate`). El usuario puede modificar sus picks las veces que desee mientras la jornada esté en estado `PUBLISHED`.
  - *Bot (Autollenado):* Operación **Insert If Not Exists** (`ON CONFLICT DO NOTHING`). Genera picks aleatorios exclusivamente para los partidos donde el participante no tenga registro, protegiendo los picks ingresados por humanos.

## 2. Bloqueo de Jornada (Anti-Trampas)
- En cuanto arranca el primer partido de la jornada (`DateTime.UtcNow >= FirstGameUtc`), el estado de la jornada pasa a `LOCKED`.
- Ningún usuario puede enviar o actualizar pronósticos para ningún partido de esa jornada una vez bloqueada.
- Al pasar a `LOCKED`, se ejecuta automáticamente el autollenado aleatorio y se revelan los pronósticos de los rivales.

## 3. Motor de Puntuación y Desempate en Cascada
La tabla de posiciones se ordena de forma estricta:
1. `ORDER BY Hits DESC` (Mayor cantidad de aciertos totales).
2. `THEN BY UpsetHits DESC` (Mayor cantidad de aciertos en partidos sorpresa donde <= 25% del grupo acertó).
3. `THEN BY Humillaciones ASC` (Menor cantidad de errores graves por paliza / goliza de 3+ goles o 20+ pts NFL).

## 4. Galardones Semanales
- **MVP:** 1° lugar en la tabla semanal tras aplicar desempates.
- **Rey de las Sorpresas:** Miembro con mayor número de upsets acertados.
- **El Humillado:** Quien apostó por un equipo que terminó perdiendo por 3+ goles (o 20+ pts en NFL). No aplica si apostó empate.
- **Víctima del Somnífero:** Apostó a un ganador pero el partido terminó en 0-0 (o < 24 pts combinados en NFL).
- **Rey del Empate Fallido:** Apostó EMPATE en fútbol, pero el partido sí tuvo un ganador.
