using DTO.Wrapped;

namespace UseCases.Wrapped;

internal sealed record WrappedStory(string StoryId, double Singularity, WrappedSlideDto Slide);

/// <summary>
/// SPEC-014: fabrica de historias S-01..S-15 (S-07 diferida por falta de datos de tiempo anadido).
/// Cada historia devuelve <c>null</c> solo cuando agota su cascada; S-03, S-04, S-12 y S-15
/// siempre entregan una slide (mazo minimo garantizado de 6).
/// </summary>
internal static class WrappedStoryFactory
{
    public static readonly IReadOnlyList<string> StoryCatalog = new[]
    {
        "S-03", "S-04", "S-05", "S-06", "S-08", "S-09",
        "S-10", "S-11", "S-12", "S-13", "S-14", "S-15"
    };

    private sealed record RivalSum(WrappedMemberInfo Member, int Score);

    public static WrappedSlideDto BuildWarmup(WrappedSeasonData data)
    {
        const string storyId = "S-01";
        var member = data.Target;
        var hits = data.HitsOfTotal(member.MemberId);
        var played = data.PickCountOf(member.MemberId);
        var percent = WrappedCopy.Percent(hits, played);
        var points = member.TotalPoints;

        var headline = WrappedCopy.Choose(data.Input.SeasonId, member.MemberId, storyId, new[]
        {
            $"Así fue tu {data.Input.SeasonName}",
            $"{data.Input.SeasonName}: tu temporada en historias",
            $"El Wrapped de {member.DisplayName}"
        });

        var body = WrappedCopy.Choose(data.Input.SeasonId, member.MemberId, storyId, new[]
        {
            $"Cerraste en el puesto #{data.FinalPosition} de {data.TotalMembers} con {points} puntos. Acertaste {hits} de {played} pronósticos: {percent} de efectividad.",
            $"Fueron {data.TotalMembers} participantes y {data.Weeks.Count} jornadas. Tu lugar: #{data.FinalPosition}. Tu cosecha: {points} puntos y {hits} aciertos.",
            $"#{data.FinalPosition} de {data.TotalMembers}. {points} puntos, {hits} aciertos y una efectividad del {percent}. Esta es tu temporada."
        });

        return Slide(
            storyId,
            "WARMUP",
            0,
            headline,
            $"#{data.FinalPosition} de {data.TotalMembers} · {points} puntos",
            body,
            $"{percent} de efectividad");
    }

    public static WrappedSlideDto BuildArchetype(WrappedSeasonData data, WrappedArchetypeResult archetype)
    {
        const string storyId = "S-02";
        var member = data.Target;
        var percent = WrappedCopy.Percent(data.HitsOfTotal(member.MemberId), data.PickCountOf(member.MemberId));

        var closing = WrappedCopy.Choose(data.Input.SeasonId, member.MemberId, storyId, new[]
        {
            "Es la forma en que juegas, no solo cómo te fue.",
            "Cada pick de la temporada apuntaba a este perfil.",
            "Tu manera de leer los partidos tiene nombre propio."
        });

        return Slide(
            storyId,
            "ARCHETYPE",
            archetype.UsedFallback ? 1 : 0,
            $"{archetype.Emoji} {archetype.Title}",
            "Tu arquetipo de pronosticador",
            $"{archetype.Description} {closing}",
            $"{percent} de efectividad",
            gradient: archetype.Gradient);
    }

    public static WrappedStory? Build(string storyId, WrappedSeasonData data) => storyId switch
    {
        "S-03" => BuildLoneWolf(data),
        "S-04" => BuildNightmare(data),
        "S-05" => BuildTalisman(data),
        "S-06" => BuildCryptonite(data),
        "S-08" => BuildClockHabit(data),
        "S-09" => BuildSoulmate(data),
        "S-10" => BuildNemesis(data),
        "S-11" => BuildBigHunt(data),
        "S-12" => BuildHotStreak(data),
        "S-13" => BuildDrawNotary(data),
        "S-14" => BuildDerbyKing(data),
        "S-15" => BuildTrophyCase(data),
        _ => null
    };

    // ---------------------------------------------------------------- S-03

    private static WrappedStory? BuildLoneWolf(WrappedSeasonData data)
    {
        const string storyId = "S-03";
        var member = data.Target;
        var hits = data.HitsOf(member.MemberId);

        // Nivel 0: unico participante del grupo en acertar ese partido.
        var loneWolf = hits
            .Where(p => data.HitsInMatch(p.MatchId) == 1)
            .OrderByDescending(p => data.VotesInMatch(p.MatchId))
            .ThenBy(p => data.MatchOf(p.MatchId).DateUtc)
            .ThenBy(p => p.MatchId)
            .FirstOrDefault();

        if (loneWolf is not null)
        {
            var match = data.MatchOf(loneWolf.MatchId);
            var votes = data.VotesInMatch(match.MatchId);
            var pickLabel = DisplayPick(loneWolf.PickAbbr, match);

            return new WrappedStory(storyId, 0.95, Slide(
                storyId,
                "LONE_WOLF",
                0,
                WrappedCopy.Choose(data.Input.SeasonId, member.MemberId, storyId, new[]
                {
                    "El Lobo Solitario",
                    "Nadie más lo vio venir",
                    "Tu apuesta más solitaria"
                }),
                $"Jornada {match.WeekNumber} · único de {votes}",
                WrappedCopy.Choose(data.Input.SeasonId, member.MemberId, storyId, new[]
                {
                    $"Fuiste el único de {votes} participantes que acertó {pickLabel}. Mientras todos miraban al rival, tú ya habías leído el partido.",
                    $"Solo tú, entre {votes} pronósticos, clavaste {pickLabel}. El grupo entero se quedó mirando el marcador.",
                    $"Uno de {votes}. Ese {pickLabel} fue tuyo y de nadie más en la quiniela."
                }),
                $"Único de {votes}",
                match));
        }

        // Nivel 1: la sorpresa más improbable que acertó.
        var upset = hits
            .Where(p => p.IsUpsetHit)
            .OrderBy(p => data.HitRateInMatch(p.MatchId))
            .ThenByDescending(p => data.MatchOf(p.MatchId).DateUtc)
            .ThenBy(p => p.MatchId)
            .FirstOrDefault();

        if (upset is not null)
        {
            var match = data.MatchOf(upset.MatchId);
            var rate = WrappedCopy.Percent(data.HitsInMatch(match.MatchId), data.VotesInMatch(match.MatchId));
            var singularity = Math.Clamp(0.85 - data.HitRateInMatch(match.MatchId) * 0.5, 0.35, 0.85);

            return new WrappedStory(storyId, singularity, Slide(
                storyId,
                "LONE_WOLF",
                1,
                WrappedCopy.Choose(data.Input.SeasonId, member.MemberId, storyId, new[]
                {
                    "La sorpresa que cazaste",
                    "Contra todo pronóstico",
                    "Tu pick más improbable"
                }),
                $"Jornada {match.WeekNumber} · sorpresa",
                WrappedCopy.Choose(data.Input.SeasonId, member.MemberId, storyId, new[]
                {
                    $"Solo el {rate} del grupo acertó este partido y tú fuiste uno de ellos. La lógica decía una cosa y tú dijiste otra.",
                    $"El {rate} del grupo vio lo mismo que tú: {DisplayPick(upset.PickAbbr, match)}. Poca compañía, mucho mérito.",
                    $"Apostaste contra el {100 - PercentageValue(rate)}% y ganaste. Ese {DisplayPick(upset.PickAbbr, match)} valía doble."
                }),
                $"Sorpresa del {rate}",
                match));
        }

        // Nivel 2: su mejor jornada (solo si tuvo al menos un acierto; con 0 aciertos baja al nivel 3).
        var playedWeeks = data.PlayedWeeksOf(member.MemberId);
        if (playedWeeks.Count > 0)
        {
            var week = data.BestWeekNumber(member.MemberId);
            var weekHits = data.HitsInWeek(member.MemberId, week);
            var weekPicks = data.PicksInWeek(member.MemberId, week);

            if (weekHits > 0)
            {
                var singularity = Math.Clamp(0.25 + 0.05 * weekHits, 0.25, 0.6);

                return new WrappedStory(storyId, singularity, Slide(
                    storyId,
                    "LONE_WOLF",
                    2,
                    WrappedCopy.Choose(data.Input.SeasonId, member.MemberId, storyId, new[]
                    {
                        "Tu semana de ensueño",
                        "La jornada en la que todo salió",
                        "Tu mejor domingo"
                    }),
                    $"Jornada {week} · {weekHits} de {weekPicks}",
                    WrappedCopy.Choose(data.Input.SeasonId, member.MemberId, storyId, new[]
                    {
                        $"La jornada {week} fue tu punto más alto: {weekHits} aciertos de {weekPicks} partidos. Ese fin de semana el calendario jugó para ti.",
                        $"En la jornada {week} sumaste {weekHits} aciertos de {weekPicks}. No hubo lobo solitario, pero sí una jornada redonda.",
                        $"Tu mejor cosecha llegó en la jornada {week}: {weekHits} de {weekPicks}. Vale como momento cumbre."
                    }),
                    $"{weekHits} aciertos"));
            }
        }

        // Nivel 3: el Gol del Honor (nunca queda vacia).
        var club = data.TeamsOf(member.MemberId)
            .OrderByDescending(t => t.Value.Picks)
            .ThenBy(t => t.Key, StringComparer.Ordinal)
            .Select(t => t.Key)
            .FirstOrDefault();

        WrappedPickInfo? honor = null;
        if (club is not null)
        {
            honor = hits
                .Where(p => string.Equals(WrappedSeasonData.Normalize(p.PickAbbr), club, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(p => data.TotalGoals(data.MatchOf(p.MatchId)))
                .ThenBy(p => p.MatchId)
                .FirstOrDefault();
        }

        honor ??= hits
            .OrderByDescending(p => data.TotalGoals(data.MatchOf(p.MatchId)))
            .ThenBy(p => p.MatchId)
            .FirstOrDefault();

        if (honor is not null)
        {
            var match = data.MatchOf(honor.MatchId);
            var goals = data.TotalGoals(match);

            return new WrappedStory(storyId, 0.2, Slide(
                storyId,
                "LONE_WOLF",
                3,
                "El Gol del Honor",
                $"Jornada {match.WeekNumber} · {goals} goles",
                WrappedCopy.Choose(data.Input.SeasonId, member.MemberId, storyId, new[]
                {
                    $"Sin sorpresas ni lobos solitarios, te quedó esto: {DisplayPick(honor.PickAbbr, match)} en un partido de {goals} goles. Un acierto honrado.",
                    $"No fue la temporada soñada, pero {DisplayPick(honor.PickAbbr, match)} en un duelo de {goals} goles es un recuerdo que se guarda.",
                    $"El honor también cuenta: acertar {DisplayPick(honor.PickAbbr, match)} en un partido de {goals} goles fue tu momento de la temporada."
                }),
                $"{goals} goles",
                match));
        }

        return new WrappedStory(storyId, 0.15, Slide(
            storyId,
            "LONE_WOLF",
            3,
            "El Gol del Honor",
            "Temporada de aprendizaje",
            WrappedCopy.Choose(data.Input.SeasonId, member.MemberId, storyId, new[]
            {
                "No hubo sorpresas, ni lobos solitarios, ni aciertos que festejar. Tu mejor momento fue participar y volver a intentarlo la próxima jornada.",
                "Sin aciertos no hay lobo solitario, pero sí una lección: la temporada se juega jornada a jornada. La revancha empieza en la próxima.",
                "Cero aciertos en el casillero. Aun así, tu mejor momento fue participar, seguir votando y no abandonar la quiniela."
            }),
            "0 aciertos"));
    }

    // ---------------------------------------------------------------- S-04

    private static WrappedStory? BuildNightmare(WrappedSeasonData data)
    {
        const string storyId = "S-04";
        var member = data.Target;
        var picks = data.PicksOf(member.MemberId);

        // Nivel 0: la peor paliza sufrida.
        var humiliation = picks
            .Where(p => p.IsHumillacion)
            .OrderByDescending(p => GoalDifference(data.MatchOf(p.MatchId)))
            .ThenByDescending(p => data.MatchOf(p.MatchId).DateUtc)
            .ThenByDescending(p => p.MatchId)
            .FirstOrDefault();

        if (humiliation is not null)
        {
            var match = data.MatchOf(humiliation.MatchId);
            var difference = GoalDifference(match);
            var singularity = Math.Clamp(0.45 + difference * 0.08, 0.45, 0.95);

            return new WrappedStory(storyId, singularity, Slide(
                storyId,
                "NIGHTMARE",
                0,
                WrappedCopy.Choose(data.Input.SeasonId, member.MemberId, storyId, new[]
                {
                    "La Pesadilla",
                    "La paliza que aún duele",
                    "Ese partido no existió"
                }),
                $"Jornada {match.WeekNumber} · -{difference} goles",
                WrappedCopy.Choose(data.Input.SeasonId, member.MemberId, storyId, new[]
                {
                    $"Apostaste por {DisplayPick(humiliation.PickAbbr, match)} y te devolvieron una paliza de {difference} goles. Hay marcadores que se borran de la memoria.",
                    $"{difference} goles de diferencia. Elegiste {DisplayPick(humiliation.PickAbbr, match)} y el partido decidió humillarte en público.",
                    $"Fue tu peor tarde: {match.HomeName} {match.HomeScore} - {match.AwayScore} {match.AwayName}. Tú ibas con {DisplayPick(humiliation.PickAbbr, match)}."
                }),
                $"-{difference} goles",
                match));
        }

        // Nivel 1 (el nivel de gol al 90' no existe: se omite): victimas del somnifero 0-0.
        var somnifero = picks
            .Where(p => p.IsSomnifero)
            .OrderByDescending(p => data.MatchOf(p.MatchId).DateUtc)
            .ThenByDescending(p => p.MatchId)
            .FirstOrDefault();

        if (somnifero is not null)
        {
            var match = data.MatchOf(somnifero.MatchId);

            return new WrappedStory(storyId, 0.45, Slide(
                storyId,
                "NIGHTMARE",
                1,
                WrappedCopy.Choose(data.Input.SeasonId, member.MemberId, storyId, new[]
                {
                    "El Somnífero",
                    "Noventa minutos perdidos",
                    "El 0-0 que te noqueó"
                }),
                $"Jornada {match.WeekNumber} · 0-0",
                WrappedCopy.Choose(data.Input.SeasonId, member.MemberId, storyId, new[]
                {
                    $"El partido terminó 0-0 y tú habías apostado por {DisplayPick(somnifero.PickAbbr, match)}. Noventa minutos que no volverán.",
                    $"Nadie marcó y tú necesitabas un gol de {DisplayPick(somnifero.PickAbbr, match)}. El reloj avanzó y el marcador no.",
                    $"Un empate sin goles, el marcador más cruel cuando apuestas a {DisplayPick(somnifero.PickAbbr, match)}."
                }),
                "0-0",
                match));
        }

        // Nivel 2: la peor jornada.
        var playedWeeks = data.PlayedWeeksOf(member.MemberId);
        if (playedWeeks.Count > 0)
        {
            var week = data.WorstWeekNumber(member.MemberId);
            var weekHits = data.HitsInWeek(member.MemberId, week);
            var weekPicks = data.PicksInWeek(member.MemberId, week);

            return new WrappedStory(storyId, 0.3, Slide(
                storyId,
                "NIGHTMARE",
                2,
                WrappedCopy.Choose(data.Input.SeasonId, member.MemberId, storyId, new[]
                {
                    "Tu peor jornada",
                    "La jornada para olvidar",
                    "El fin de semana negro"
                }),
                $"Jornada {week} · {weekHits} de {weekPicks}",
                WrappedCopy.Choose(data.Input.SeasonId, member.MemberId, storyId, new[]
                {
                    $"La jornada {week} fue la más dura: {weekHits} aciertos de {weekPicks} partidos. Hasta las malas rachas se recuerdan.",
                    $"En la jornada {week} el marcador te dio la espalda: {weekHits} de {weekPicks}. Aprendiste que la temporada también se juega cuesta arriba.",
                    $"Jornada {week}: {weekHits} aciertos de {weekPicks}. No hubo paliza ni 0-0 traicionero, solo un domingo gris."
                }),
                $"{weekHits} de {weekPicks}"));
        }

        return new WrappedStory(storyId, 0.2, Slide(
            storyId,
            "NIGHTMARE",
            2,
            "Tu peor jornada",
            "Sin picks registrados",
            WrappedCopy.Choose(data.Input.SeasonId, member.MemberId, storyId, new[]
            {
                "La temporada pasó sin que dejaras picks en el marcador. La próxima vez, el calendario te espera con la primera jornada.",
                "No hubo picks, ni aciertos, ni pesadillas: solo una butaca vacía. Vuelve en la próxima temporada y que el marcador te extrañe.",
                "Sin pronósticos registrados no hay paliza que contar. La única pesadilla fue no jugar."
            }),
            "0 picks"));
    }

    // ---------------------------------------------------------------- S-05

    private static WrappedStory? BuildTalisman(WrappedSeasonData data)
    {
        const string storyId = "S-05";
        var member = data.Target;
        var teams = data.TeamsOf(member.MemberId);

        // Nivel 0: equipo con >= 75% de acierto sobre >= 4 picks.
        var talisman = teams.Values
            .Where(t => t.Picks >= 4 && t.Accuracy >= 0.75)
            .OrderByDescending(t => t.Accuracy)
            .ThenByDescending(t => t.Picks)
            .ThenBy(t => t.Abbr, StringComparer.Ordinal)
            .FirstOrDefault();

        if (talisman is not null)
        {
            var rate = WrappedCopy.Percent(talisman.Hits, talisman.Picks);

            return new WrappedStory(storyId, Math.Clamp(0.5 + talisman.Accuracy * 0.45, 0.5, 0.95), Slide(
                storyId,
                "TALISMAN",
                0,
                WrappedCopy.Choose(data.Input.SeasonId, member.MemberId, storyId, new[]
                {
                    "Tu Talismán",
                    "El equipo de la suerte",
                    "Punto casi seguro"
                }),
                $"{talisman.Abbr} · {talisman.Picks} picks",
                WrappedCopy.Choose(data.Input.SeasonId, member.MemberId, storyId, new[]
                {
                    $"Con {talisman.Abbr} acertaste {talisman.Hits} de {talisman.Picks} ({rate}). Cuando aparecía en el calendario, era punto casi seguro.",
                    $"{talisman.Abbr} te respondió {talisman.Hits} veces de {talisman.Picks} ({rate}). Ese equipo y tú tienen un pacto no escrito.",
                    $"Cada vez que apostaste por {talisman.Abbr} dormiste tranquilo: {talisman.Hits} aciertos en {talisman.Picks} intentos ({rate})."
                }),
                $"{rate} de acierto"));
        }

        // Nivel 1: el equipo "chico" que le dio puntos.
        var smallTeam = teams.Values
            .Where(t => t.Hits > 0)
            .OrderBy(t => data.GroupTeams.TryGetValue(t.Abbr, out var group) ? group.Accuracy : 1d)
            .ThenBy(t => t.Abbr, StringComparer.Ordinal)
            .FirstOrDefault();

        if (smallTeam is not null)
        {
            return new WrappedStory(storyId, 0.55, Slide(
                storyId,
                "TALISMAN",
                1,
                "El equipo que te dio puntos",
                $"{smallTeam.Abbr} · {smallTeam.Hits} aciertos",
                WrappedCopy.Choose(data.Input.SeasonId, member.MemberId, storyId, new[]
                {
                    $"Nadie apostaba por {smallTeam.Abbr}, pero a ti te pagó {smallTeam.Hits} acierto(s) en {smallTeam.Picks} intentos. Los puntos de la fe valen doble.",
                    $"{smallTeam.Abbr} era el equipo que el grupo evitaba y a ti te devolvió {smallTeam.Hits} acierto(s). Ahí está el mérito.",
                    $"Contra la corriente del grupo, {smallTeam.Abbr} te dio {smallTeam.Hits} punto(s) en {smallTeam.Picks} apuestas. Los tapados también responden."
                }),
                $"{smallTeam.Hits} aciertos"));
        }

        // Nivel 2: su ultimo acierto del torneo.
        var lastHit = data.HitsOf(member.MemberId)
            .OrderByDescending(p => data.MatchOf(p.MatchId).DateUtc)
            .ThenByDescending(p => p.MatchId)
            .FirstOrDefault();

        if (lastHit is null) return null;

        var lastMatch = data.MatchOf(lastHit.MatchId);

        return new WrappedStory(storyId, 0.35, Slide(
            storyId,
            "TALISMAN",
            2,
            "Tu último acierto",
            $"Jornada {lastMatch.WeekNumber}",
            WrappedCopy.Choose(data.Input.SeasonId, member.MemberId, storyId, new[]
            {
                $"El último punto de la temporada llegó con {DisplayPick(lastHit.PickAbbr, lastMatch)}. No será un talismán, pero fue tuyo.",
                $"Cerraste con {DisplayPick(lastHit.PickAbbr, lastMatch)} en la jornada {lastMatch.WeekNumber}. Poco botín, pero la última palabra fue tuya.",
                $"La temporada se despidió con un acierto: {DisplayPick(lastHit.PickAbbr, lastMatch)}. Suficiente para irse con algo."
            }),
            "Último acierto",
            lastMatch));
    }

    // ---------------------------------------------------------------- S-06

    private static WrappedStory? BuildCryptonite(WrappedSeasonData data)
    {
        const string storyId = "S-06";
        var member = data.Target;
        var teams = data.TeamsOf(member.MemberId);

        // Nivel 0: equipo con >= 4 fallos.
        var cursed = teams.Values
            .Where(t => t.Misses >= 4)
            .OrderByDescending(t => t.Misses)
            .ThenBy(t => t.Abbr, StringComparer.Ordinal)
            .FirstOrDefault();

        if (cursed is not null)
        {
            var lastFailure = data.PicksOf(member.MemberId)
                .Where(p => p.IsHit == false && string.Equals(
                    WrappedSeasonData.Normalize(p.PickAbbr), cursed.Abbr, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(p => data.MatchOf(p.MatchId).DateUtc)
                .ThenByDescending(p => p.MatchId)
                .FirstOrDefault();

            var match = lastFailure is null ? null : data.MatchOf(lastFailure.MatchId);

            return new WrappedStory(storyId, Math.Clamp(0.45 + cursed.Misses * 0.07, 0.45, 0.9), Slide(
                storyId,
                "CRYPTONITE",
                0,
                WrappedCopy.Choose(data.Input.SeasonId, member.MemberId, storyId, new[]
                {
                    "La Kriptonita",
                    "Tu mufa de la temporada",
                    "El equipo que te arruinó"
                }),
                $"{cursed.Abbr} · {cursed.Misses} fallos",
                WrappedCopy.Choose(data.Input.SeasonId, member.MemberId, storyId, new[]
                {
                    $"Apostaste {cursed.Picks} veces por {cursed.Abbr} y fallaste {cursed.Misses}. Ese equipo fue tu kriptonita: cada vez que confiaste, te cobró.",
                    $"{cursed.Misses} fallos con {cursed.Abbr} en {cursed.Picks} intentos. La próxima temporada, mira dos veces antes de firmar ese pick.",
                    $"Contra todo pronóstico, {cursed.Abbr} te quito {cursed.Misses} puntos. La estadística dice que no vuelvas a apostar por ellos."
                }),
                $"{cursed.Misses} fallos",
                match));
        }

        // Nivel 1: el equipo que le empató de sorpresa.
        var drawsLost = data.PicksOf(member.MemberId)
            .Where(p => p.IsEmpateFallido)
            .Select(p => new { Pick = p, Match = data.MatchOf(p.MatchId) })
            .ToList();

        if (drawsLost.Count > 0)
        {
            var byTeam = drawsLost
                .Select(x => new
                {
                    Team = ClosestTeam(x.Match),
                    x.Match
                })
                .Where(x => x.Team is not null)
                .GroupBy(x => x.Team!)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key, StringComparer.Ordinal)
                .FirstOrDefault();

            if (byTeam is not null)
            {
                var match = byTeam
                    .OrderByDescending(x => x.Match.DateUtc)
                    .ThenByDescending(x => x.Match.MatchId)
                    .First()
                    .Match;

                return new WrappedStory(storyId, 0.4, Slide(
                    storyId,
                    "CRYPTONITE",
                    1,
                    "El empate que te robaron",
                    $"{byTeam.Key} · {byTeam.Count()} vez(ces)",
                    WrappedCopy.Choose(data.Input.SeasonId, member.MemberId, storyId, new[]
                    {
                        $"{byTeam.Key} te empató de sorpresa {byTeam.Count()} vez(ces) cuando ya cantabas el punto. El empate es el resultado más traicionero.",
                        $"Con {byTeam.Key} la historia terminó en tablas {byTeam.Count()} vez(ces). Tu pick de victoria se quedó sin premio.",
                        $"{byTeam.Key} apareció {byTeam.Count()} vez(ces) para robarte el punto con un empate de último momento."
                    }),
                    $"{byTeam.Count()} empates fallidos",
                    match));
            }
        }

        // Nivel 2: autocritica por cambios de ultima hora.
        var lastMinute = data.PicksOf(member.MemberId)
            .Where(p => p.IsHit == false && IsLastMinute(data.MatchOf(p.MatchId), p))
            .OrderByDescending(p => data.MatchOf(p.MatchId).DateUtc)
            .ThenByDescending(p => p.MatchId)
            .FirstOrDefault();

        if (lastMinute is not null)
        {
            var match = data.MatchOf(lastMinute.MatchId);

            return new WrappedStory(storyId, 0.35, Slide(
                storyId,
                "CRYPTONITE",
                2,
                "El cambio de última hora",
                $"Jornada {match.WeekNumber} · a menos de 1 hora",
                WrappedCopy.Choose(data.Input.SeasonId, member.MemberId, storyId, new[]
                {
                    $"Editaste tu pick a menos de una hora del cierre y perdiste el punto. La prisa también juega en contra: {DisplayPick(lastMinute.PickAbbr, match)} no alcanzó.",
                    $"Un cambio de último minuto te costó el acierto: {DisplayPick(lastMinute.PickAbbr, match)} no era el resultado. Lección aprendida.",
                    $"A última hora cambiaste a {DisplayPick(lastMinute.PickAbbr, match)} y el marcador te cobró la osadía. La próxima, deja el pick tranquilo."
                }),
                "Cambio de último minuto",
                match));
        }

        return null;
    }

    // ---------------------------------------------------------------- S-08

    private static WrappedStory? BuildClockHabit(WrappedSeasonData data)
    {
        const string storyId = "S-08";
        var member = data.Target;
        var picks = data.PicksOf(member.MemberId)
            .Where(p => data.MatchOf(p.MatchId).WeekFirstGameUtc.HasValue)
            .ToList();

        if (picks.Count == 0) return null;

        var averageHours = picks.Average(p => Math.Clamp(
            (data.MatchOf(p.MatchId).WeekFirstGameUtc!.Value - p.SubmittedUtc).TotalHours,
            0d,
            24d * 30d));

        var autofilledWeeks = data.AutofilledWeeks(member.MemberId);
        var averageLabel = WrappedCopy.OneDecimal(averageHours);

        if (autofilledWeeks >= 2)
        {
            return new WrappedStory(storyId, 0.65, Slide(
                storyId,
                "CLOCK_HABIT",
                2,
                WrappedCopy.Choose(data.Input.SeasonId, member.MemberId, storyId, new[]
                {
                    "El Amigo del Bot",
                    "El azar jugó por ti",
                    "Autollenado con suerte"
                }),
                $"{autofilledWeeks} jornadas con el bot",
                WrappedCopy.Choose(data.Input.SeasonId, member.MemberId, storyId, new[]
                {
                    $"En {autofilledWeeks} jornadas el autollenado aleatorio completó tu boleta. A veces el azar sabe más que uno.",
                    $"El bot te salvó {autofilledWeeks} veces: entró, eligió al azar y aun así sumaste. Confianza ciega en el sistema.",
                    $"{autofilledWeeks} jornadas las resolvió el autollenado. El reloj te ganó la carrera, pero el marcador te dio puntos."
                }),
                $"{autofilledWeeks} jornadas auto"));
        }

        if (averageHours < 3d)
        {
            var singularity = Math.Clamp(1d - averageHours / 3d, 0.35, 0.9);

            return new WrappedStory(storyId, singularity, Slide(
                storyId,
                "CLOCK_HABIT",
                0,
                WrappedCopy.Choose(data.Input.SeasonId, member.MemberId, storyId, new[]
                {
                    "El Hábito del Reloj",
                    "Adicto a la adrenalina",
                    "Siempre al límite"
                }),
                $"Promedio: {averageLabel} h antes del cierre",
                WrappedCopy.Choose(data.Input.SeasonId, member.MemberId, storyId, new[]
                {
                    $"Votaste en promedio {averageLabel} horas antes del cierre. Pura adrenalina de último minuto: pensar el pick con el reloj en contra.",
                    $"Tu boleta siempre llegaba sobre la hora: {averageLabel} horas de promedio antes del primer partido. La presión es parte del ritual.",
                    $"{averageLabel} horas de margen promedio. No es indecisión, es emoción de último segundo."
                }),
                $"{averageLabel} h de margen"));
        }

        return new WrappedStory(storyId, 0.4, Slide(
            storyId,
            "CLOCK_HABIT",
            1,
            WrappedCopy.Choose(data.Input.SeasonId, member.MemberId, storyId, new[]
            {
                "Disciplina de relojero",
                "El que planea con tiempo",
                "Picks con anticipación"
            }),
            $"Promedio: {averageLabel} h antes del cierre",
            WrappedCopy.Choose(data.Input.SeasonId, member.MemberId, storyId, new[]
            {
                $"Promedio de {averageLabel} horas de anticipación. Nunca te agarró el cierre: la disciplina también es una forma de talento.",
                $"Votabas con {averageLabel} horas de margen. Mientras otros corrían, tú ya tenías los picks listos.",
                $"{averageLabel} horas de ventaja promedio. Tu ritual era tranquilo, con tiempo para pensar cada partido."
            }),
            $"{averageLabel} h de margen"));
    }

    // ---------------------------------------------------------------- S-09

    private static WrappedStory? BuildSoulmate(WrappedSeasonData data)
    {
        const string storyId = "S-09";
        var resolved = ResolveSoulmate(data);
        if (resolved is null) return null;

        var (pair, level) = resolved.Value;

        if (level == 0)
        {
            var rate = WrappedCopy.Percent(pair.CoincidenceRatio);

            return new WrappedStory(storyId, Math.Clamp(0.3 + pair.CoincidenceRatio * 0.6, 0.3, 0.9), Slide(
                storyId,
                "SOULMATE",
                0,
                WrappedCopy.Choose(data.Input.SeasonId, data.Target.MemberId, storyId, new[]
                {
                    "Alma Gemela",
                    "Piensan igual",
                    "El socio perfecto"
                }),
                $"{pair.Member.DisplayName} · {rate} de coincidencia",
                WrappedCopy.Choose(data.Input.SeasonId, data.Target.MemberId, storyId, new[]
                {
                    $"Coincidieron en {pair.Coincident} de {pair.Common} pronósticos ({rate}). Piensan igual hasta cuando se equivocan.",
                    $"Con {pair.Member.DisplayName} compartiste {pair.Coincident} de {pair.Common} picks ({rate}). Una sociedad peligrosa para el resto.",
                    $"{pair.Common} partidos con picks en común y {pair.Coincident} coincidencias ({rate}). {pair.Member.DisplayName} es tu espejo en la quiniela."
                }),
                $"{rate} de coincidencia",
                mentioned: pair.Member));
        }

        return new WrappedStory(storyId, 0.4, Slide(
            storyId,
            "SOULMATE",
            1,
            "Empates compartidos",
            $"{pair.Member.DisplayName} · {pair.SharedDraws} empates",
            WrappedCopy.Choose(data.Input.SeasonId, data.Target.MemberId, storyId, new[]
            {
                $"Con {pair.Member.DisplayName} firmaron {pair.SharedDraws} empates juntos. Dos notarios del resultado más incomprendido del fútbol.",
                $"{pair.SharedDraws} empates apostados a la vez con {pair.Member.DisplayName}. Cuando el partido pinta cerrado, los dos lo saben.",
                $"Tú y {pair.Member.DisplayName} coincidieron en {pair.SharedDraws} empates. Una sociedad que apuesta al X sin miedo."
            }),
            $"{pair.SharedDraws} empates juntos",
            mentioned: pair.Member));
    }

    // ---------------------------------------------------------------- S-10

    private static WrappedStory? BuildNemesis(WrappedSeasonData data)
    {
        const string storyId = "S-10";
        if (data.Members.Count < 2) return null;

        var soulmateId = ResolveSoulmate(data)?.Pair.Member.MemberId;

        var nemesis = data.PairsOf(data.Target.MemberId)
            .Where(p => p.Common >= 10 && p.Member.MemberId != soulmateId)
            .OrderByDescending(p => p.DiscrepancyRatio)
            .ThenByDescending(p => p.Discrepant)
            .ThenBy(p => p.Member.MemberId)
            .FirstOrDefault();

        if (nemesis is not null)
        {
            var rate = WrappedCopy.Percent(nemesis.DiscrepancyRatio);

            return new WrappedStory(storyId, Math.Clamp(0.3 + nemesis.DiscrepancyRatio * 0.6, 0.3, 0.9), Slide(
                storyId,
                "NEMESIS",
                0,
                WrappedCopy.Choose(data.Input.SeasonId, data.Target.MemberId, storyId, new[]
                {
                    "El Némesis",
                    "Tu rival de siempre",
                    "Polos opuestos"
                }),
                $"{nemesis.Member.DisplayName} · {rate} de discrepancia",
                WrappedCopy.Choose(data.Input.SeasonId, data.Target.MemberId, storyId, new[]
                {
                    $"Discreparon en {nemesis.Discrepant} de {nemesis.Common} picks ({rate}). Si uno acierta, el otro sufre.",
                    $"Con {nemesis.Member.DisplayName} coincidieron apenas {nemesis.Coincident} de {nemesis.Common} veces. Dos formas opuestas de ver el fútbol.",
                    $"{nemesis.Common} picks compartidos y solo {nemesis.Coincident} coincidencias: {nemesis.Member.DisplayName} es tu némesis estadística."
                }),
                $"{rate} de discrepancia",
                mentioned: nemesis.Member));
        }

        // Nivel 1: el rival con el que mas intercambio posiciones.
        var swapped = data.PairsOf(data.Target.MemberId)
            .Where(p => p.Member.MemberId != soulmateId && p.PositionChanges > 0)
            .OrderByDescending(p => p.PositionChanges)
            .ThenBy(p => p.Member.MemberId)
            .FirstOrDefault();

        if (swapped is null) return null;

        return new WrappedStory(storyId, 0.45, Slide(
            storyId,
            "NEMESIS",
            1,
            "Intercambio de posiciones",
            $"{swapped.Member.DisplayName} · {swapped.PositionChanges} cruces",
            WrappedCopy.Choose(data.Input.SeasonId, data.Target.MemberId, storyId, new[]
            {
                $"Con {swapped.Member.DisplayName} se pasaron {swapped.PositionChanges} veces en la tabla. Cada jornada contra él era un duelo personal.",
                $"{swapped.PositionChanges} cruces de posición con {swapped.Member.DisplayName}. Se turnaban el lugar como si estuviera reservado.",
                $"La tabla los juntó {swapped.PositionChanges} veces: {swapped.Member.DisplayName} arriba, tú abajo y al revés la jornada siguiente."
            }),
            $"{swapped.PositionChanges} cambios de posición",
            mentioned: swapped.Member));
    }

    // ---------------------------------------------------------------- S-11

    private static WrappedStory? BuildBigHunt(WrappedSeasonData data)
    {
        const string storyId = "S-11";
        var member = data.Target;

        var lastWeeks = data.Weeks.Skip(Math.Max(0, data.Weeks.Count - 3)).ToList();
        if (lastWeeks.Count == 0) return null;
        if (data.Members.Count < 2) return null;

        var targetScore = lastWeeks.Sum(w => data.HitsInWeek(member.MemberId, w));

        var rivals = data.Members
            .Where(m => m.MemberId != member.MemberId)
            .Select(m => new RivalSum(m, lastWeeks.Sum(w => data.HitsInWeek(m.MemberId, w))))
            .ToList();

        var overtaken = rivals
            .Where(r => targetScore > r.Score)
            .OrderBy(r => targetScore - r.Score)
            .ThenBy(r => r.Member.MemberId)
            .FirstOrDefault();

        if (overtaken is not null)
        {
            var margin = targetScore - overtaken.Score;

            return new WrappedStory(storyId, 0.85, Slide(
                storyId,
                "BIG_HUNT",
                0,
                WrappedCopy.Choose(data.Input.SeasonId, member.MemberId, storyId, new[]
                {
                    "La Gran Cacería",
                    "Remontada en la recta final",
                    "Cazaste a un rival directo"
                }),
                $"Últimas {lastWeeks.Count} jornadas · +{margin}",
                WrappedCopy.Choose(data.Input.SeasonId, member.MemberId, storyId, new[]
                {
                    $"En el cierre le sacaste {margin} punto(s) a {overtaken.Member.DisplayName} y lo dejaste atrás en la tabla. Las últimas jornadas se juegan con todo.",
                    $"{overtaken.Member.DisplayName} era tu rival directo y en las últimas {lastWeeks.Count} jornadas lo superaste por {margin} punto(s). Cacería completada.",
                    $"Recta final perfecta: {targetScore} aciertos contra los {overtaken.Score} de {overtaken.Member.DisplayName}. La persecución terminó con él detrás."
                }),
                $"+{margin} puntos",
                mentioned: overtaken.Member));
        }

        var chased = rivals
            .Where(r => r.Score - targetScore >= 1 && r.Score - targetScore <= 2)
            .OrderBy(r => r.Score - targetScore)
            .ThenBy(r => r.Member.MemberId)
            .FirstOrDefault();

        if (chased is null) return null;

        var missedBy = chased.Score - targetScore;

        return new WrappedStory(storyId, 0.5, Slide(
            storyId,
            "BIG_HUNT",
            1,
            "La persecución frustrada",
            $"Últimas {lastWeeks.Count} jornadas · -{missedBy}",
            WrappedCopy.Choose(data.Input.SeasonId, member.MemberId, storyId, new[]
            {
                $"Te quedaste a {missedBy} punto(s) de {chased.Member.DisplayName} en las últimas {lastWeeks.Count} jornadas. Duele más cuanto más cerca.",
                $"{chased.Member.DisplayName} te sacó {missedBy} punto(s) en la recta final. La persecución quedó a un suspiro de la gloria.",
                $"Perseguiste a {chased.Member.DisplayName} hasta el final y te faltaron {missedBy} punto(s). Una cacería que merecía otro cierre."
            }),
            $"A {missedBy} punto(s)",
            mentioned: chased.Member));
    }

    // ---------------------------------------------------------------- S-12

    private static WrappedStory? BuildHotStreak(WrappedSeasonData data)
    {
        const string storyId = "S-12";
        var member = data.Target;
        var streak = data.LongestStreak(member.MemberId);

        if (streak >= 4)
        {
            var singularity = Math.Clamp(0.35 + streak * 0.06, 0.35, 0.95);

            return new WrappedStory(storyId, singularity, Slide(
                storyId,
                "HOT_STREAK",
                0,
                WrappedCopy.Choose(data.Input.SeasonId, member.MemberId, storyId, new[]
                {
                    "La Racha en Llamas",
                    "Imparable",
                    "Cuando entraste en calor"
                }),
                $"{streak} aciertos consecutivos",
                WrappedCopy.Choose(data.Input.SeasonId, member.MemberId, storyId, new[]
                {
                    $"{streak} aciertos consecutivos. Cuando entraste en calor, no hubo quién te parara.",
                    $"Encadenaste {streak} aciertos seguidos superando los cuatro de la marca. La racha que el grupo recuerda.",
                    $"{streak} partidos seguidos acertando. Ese tramo de la temporada fue tuyo y de nadie más."
                }),
                $"{streak} seguidos"));
        }

        // Nivel 1: mejor fin de semana (siempre elegible, incluso con 0 aciertos).
        var week = data.BestWeekNumber(member.MemberId);
        var weekHits = week == 0 ? 0 : data.HitsInWeek(member.MemberId, week);
        var weekPicks = week == 0 ? 0 : data.PicksInWeek(member.MemberId, week);
        var singularityFallback = weekPicks == 0
            ? 0.15
            : Math.Clamp(0.2 + 0.4 * weekHits / weekPicks, 0.2, 0.6);

        if (week == 0)
        {
            return new WrappedStory(storyId, singularityFallback, Slide(
                storyId,
                "HOT_STREAK",
                1,
                "Tu mejor fin de semana",
                "Sin jornadas registradas",
                WrappedCopy.Choose(data.Input.SeasonId, member.MemberId, storyId, new[]
                {
                    "No hubo racha ni mejor jornada esta temporada, pero la afición nunca se apagó. El próximo torneo empieza en cero para todos.",
                    "Cero jornadas jugadas y ninguna racha que contar. La próxima temporada llega con la página en blanco.",
                    "Sin actividad en el calendario, la racha queda pendiente. La próxima vez, el primer pick enciende la mecha."
                }),
                "0 aciertos"));
        }

        return new WrappedStory(storyId, singularityFallback, Slide(
            storyId,
            "HOT_STREAK",
            1,
            WrappedCopy.Choose(data.Input.SeasonId, member.MemberId, storyId, new[]
            {
                "Tu mejor fin de semana",
                "El pico de la temporada",
                "Tu domingo perfecto"
            }),
            $"Jornada {week} · {weekHits} de {weekPicks}",
            weekHits == 0
                ? WrappedCopy.Choose(data.Input.SeasonId, member.MemberId, storyId, new[]
                {
                    $"La jornada {week} fue tu mejor registro, aunque no alcanzó para aciertos: 0 de {weekPicks}. La constancia de votar también cuenta.",
                    $"Sin aciertos en la jornada {week}, pero con {weekPicks} picks firmados. A veces la racha es simplemente no rendirse.",
                    $"La jornada {week} quedó en cero de {weekPicks}. El próximo torneo empieza igual para todos y ahí se rompe la mala racha."
                })
                : WrappedCopy.Choose(data.Input.SeasonId, member.MemberId, storyId, new[]
                {
                    $"La jornada {week} fue tu pico: {weekHits} aciertos de {weekPicks}. Sin racha de cuatro, pero con un fin de semana para recordar.",
                    $"Tu mejor fin de semana fue el de la jornada {week}: {weekHits} de {weekPicks}. Ese domingo el marcador estuvo de tu lado.",
                    $"Jornada {week}: {weekHits} aciertos de {weekPicks}. No hubo racha histórica, pero sí un pico que vale la pena contar."
                }),
            $"{weekHits} aciertos"));
    }

    // ---------------------------------------------------------------- S-13

    private static WrappedStory? BuildDrawNotary(WrappedSeasonData data)
    {
        const string storyId = "S-13";
        var member = data.Target;
        var picks = data.PicksOf(member.MemberId);
        if (picks.Count == 0) return null;

        var draws = data.DrawsPicked(member.MemberId);
        var share = (double)draws / picks.Count;
        var groupShare = data.AverageDrawShare();

        if (draws >= 3 && share > groupShare + 0.05)
        {
            var shareLabel = WrappedCopy.Percent(share);
            var groupLabel = WrappedCopy.Percent(groupShare);

            return new WrappedStory(storyId, Math.Clamp(0.45 + (share - groupShare), 0.45, 0.9), Slide(
                storyId,
                "DRAW_NOTARY",
                0,
                WrappedCopy.Choose(data.Input.SeasonId, member.MemberId, storyId, new[]
                {
                    "El Notario del Empate",
                    "Firma el empate",
                    "Especialista en el X"
                }),
                $"{draws} empates apostados · {shareLabel}",
                WrappedCopy.Choose(data.Input.SeasonId, member.MemberId, storyId, new[]
                {
                    $"Apostaste {draws} empates ({shareLabel} de tus picks), muy por encima del {groupLabel} del grupo. Acertaste {data.DrawHits(member.MemberId)}.",
                    $"El empate es tu firma: {draws} picks al X, un {shareLabel} del total frente al {groupLabel} del grupo.",
                    $"{draws} empates pronosticados ({shareLabel}) cuando el grupo apenas firmaba el {groupLabel}. El Notario está en sesión."
                }),
                $"{shareLabel} empates"));
        }

        // Nivel 1: efectividad en partidos de 0 o 1 gol.
        var lowScoring = picks
            .Where(p => data.TotalGoals(data.MatchOf(p.MatchId)) <= 1)
            .ToList();

        if (lowScoring.Count < 2) return null;

        var lowHits = lowScoring.Count(p => p.IsHit == true);
        var lowRate = (double)lowHits / lowScoring.Count;
        if (lowHits == 0 || lowRate < 0.5) return null;

        var rateLabel = WrappedCopy.Percent(lowHits, lowScoring.Count);

        return new WrappedStory(storyId, 0.45, Slide(
            storyId,
            "DRAW_NOTARY",
            1,
            "Especialista en partidos cerrados",
            $"{lowHits} de {lowScoring.Count} · {rateLabel}",
            WrappedCopy.Choose(data.Input.SeasonId, member.MemberId, storyId, new[]
            {
                $"En partidos de 0 o 1 gol acertaste {lowHits} de {lowScoring.Count} ({rateLabel}). Los marcadores cortos no te asustan.",
                $"Cuando el partido pintaba cerrado, tú acertabas: {lowHits} de {lowScoring.Count} en duelos de pocos goles ({rateLabel}).",
                $"{rateLabel} de efectividad en partidos de 0 o 1 gol ({lowHits} de {lowScoring.Count}). Ahí se ve el ojo para el resultado seco."
            }),
            $"{rateLabel} en partidos cerrados"));
    }

    // ---------------------------------------------------------------- S-14

    private static WrappedStory? BuildDerbyKing(WrappedSeasonData data)
    {
        const string storyId = "S-14";
        var member = data.Target;
        var picks = data.PicksOf(member.MemberId);

        // Nivel 0: rendimiento en clasicos.
        var derby = picks
            .Where(p => data.MatchOf(p.MatchId).DerbyName is not null)
            .ToList();

        var derbyHits = derby.Count(p => p.IsHit == true);
        if (derby.Count >= 2 && derbyHits >= 1)
        {
            var rate = WrappedCopy.Percent(derbyHits, derby.Count);
            var bestDerby = derby
                .Where(p => p.IsHit == true)
                .OrderByDescending(p => data.MatchOf(p.MatchId).DateUtc)
                .ThenByDescending(p => p.MatchId)
                .FirstOrDefault();

            return new WrappedStory(storyId, 0.75, Slide(
                storyId,
                "DERBY_KING",
                0,
                WrappedCopy.Choose(data.Input.SeasonId, member.MemberId, storyId, new[]
                {
                    "El Rey de los Clásicos",
                    "Los grandes se te dan",
                    "Clásicos con sabor"
                }),
                $"{derbyHits} de {derby.Count} en clásicos · {rate}",
                WrappedCopy.Choose(data.Input.SeasonId, member.MemberId, storyId, new[]
                {
                    $"En los clásicos acertaste {derbyHits} de {derby.Count} ({rate}). Los partidos grandes se te dan mejor que al resto.",
                    $"Los clásicos te respetan: {derbyHits} aciertos en {derby.Count} partidos ({rate}). Cuando hay rivalidad, tú respondes.",
                    $"{rate} de efectividad en clásicos ({derbyHits} de {derby.Count}). Ese es tu terreno."
                }),
                $"{rate} en clásicos",
                bestDerby is null ? null : data.MatchOf(bestDerby.MatchId)));
        }

        // Nivel 1: rendimiento ante equipos grandes.
        var big = picks
            .Where(p =>
            {
                var match = data.MatchOf(p.MatchId);
                return WrappedSeasonData.BigTeams.Contains(WrappedSeasonData.Normalize(match.HomeAbbr), StringComparer.Ordinal)
                       || WrappedSeasonData.BigTeams.Contains(WrappedSeasonData.Normalize(match.AwayAbbr), StringComparer.Ordinal);
            })
            .ToList();

        var bigHits = big.Count(p => p.IsHit == true);
        if (big.Count < 2 || bigHits == 0) return null;

        var bigRate = WrappedCopy.Percent(bigHits, big.Count);

        return new WrappedStory(storyId, 0.5, Slide(
            storyId,
            "DERBY_KING",
            1,
            "Rendimiento ante los grandes",
            $"{bigHits} de {big.Count} · {bigRate}",
            WrappedCopy.Choose(data.Input.SeasonId, member.MemberId, storyId, new[]
            {
                $"Contra los equipos grandes acertaste {bigHits} de {big.Count} ({bigRate}). No hubo clásico oficial, pero la talla se notó.",
                $"Los equipos grandes no te intimidaron: {bigHits} aciertos en {big.Count} partidos ({bigRate}).",
                $"{bigRate} de efectividad ante los seis grandes ({bigHits} de {big.Count}). La jerarquía también se juega en la quiniela."
            }),
            $"{bigRate} ante los grandes"));
    }

    // ---------------------------------------------------------------- S-15

    private static WrappedStory? BuildTrophyCase(WrappedSeasonData data)
    {
        const string storyId = "S-15";
        var member = data.Target;
        var awards = data.AwardCount(member.MemberId);

        if (awards > 0)
        {
            return new WrappedStory(storyId, Math.Clamp(0.4 + awards * 0.1, 0.4, 0.95), Slide(
                storyId,
                "TROPHY_CASE",
                0,
                WrappedCopy.Choose(data.Input.SeasonId, member.MemberId, storyId, new[]
                {
                    "La Vitrina de la Temporada",
                    "Galardones con tu nombre",
                    "Tu colección de premios"
                }),
                $"{awards} galardón(es)",
                WrappedCopy.Choose(data.Input.SeasonId, member.MemberId, storyId, new[]
                {
                    $"Sumaste {awards} galardón(es) durante la temporada. La vitrina ya tiene nombre y apellido.",
                    $"{awards} premio(s) en el año: MVP, somníferos o humillaciones, todo cuenta en la vitrina.",
                    $"El jurado te entregó {awards} galardón(es). No es la copa, pero se exhibe igual."
                }),
                $"{awards} galardones"));
        }

        var weeksInTop5 = data.WeeksInTop5(member.MemberId);

        return new WrappedStory(storyId, 0.15, Slide(
            storyId,
            "TROPHY_CASE",
            1,
            WrappedCopy.Choose(data.Input.SeasonId, member.MemberId, storyId, new[]
            {
                "Cerca de la cima",
                "La vitrina vacía",
                "Constancia sin trofeo"
            }),
            $"{weeksInTop5} jornada(s) en el Top 5",
            weeksInTop5 > 0
                ? WrappedCopy.Choose(data.Input.SeasonId, member.MemberId, storyId, new[]
                {
                    $"No hubo trofeos individuales, pero estuviste {weeksInTop5} jornada(s) dentro del Top 5. La constancia también se exhibe.",
                    $"Sin galardones en la vitrina, pero {weeksInTop5} jornada(s) en el Top 5. Ese mérito no se borra.",
                    $"{weeksInTop5} apariciones en el Top 5 y ninguna copa. La regularidad fue tu premio silencioso."
                })
                : WrappedCopy.Choose(data.Input.SeasonId, member.MemberId, storyId, new[]
                {
                    "Esta temporada no hubo galardones ni visitas al Top 5. La vitrina espera; la próxima temporada empieza de cero para todos.",
                    "Vitrina vacía y ninguna jornada en el Top 5. No es el final: es el punto de partida de la revancha.",
                    "Ni premios ni Top 5 esta vez. La vitrina sigue abierta y el próximo torneo trae otra oportunidad."
                }),
            $"{weeksInTop5} en Top 5"));
    }

    // ---------------------------------------------------------------- helpers

    private static (WrappedSeasonData.PairStat Pair, int Level)? ResolveSoulmate(WrappedSeasonData data)
    {
        if (data.Members.Count < 2) return null;

        var pairs = data.PairsOf(data.Target.MemberId)
            .Where(p => p.Common >= 10)
            .ToList();

        var soulmate = pairs
            .Where(p => p.CoincidenceRatio >= 0.5)
            .OrderByDescending(p => p.CoincidenceRatio)
            .ThenByDescending(p => p.Coincident)
            .ThenBy(p => p.Member.MemberId)
            .FirstOrDefault();

        if (soulmate is not null) return (soulmate, 0);

        var sharedDraws = pairs
            .Where(p => p.SharedDraws >= 3)
            .OrderByDescending(p => p.SharedDraws)
            .ThenBy(p => p.Member.MemberId)
            .FirstOrDefault();

        return sharedDraws is null ? null : (sharedDraws, 1);
    }

    private static WrappedSlideDto Slide(
        string storyId,
        string slideType,
        int fallbackLevel,
        string headline,
        string subtitle,
        string body,
        string? highlightMetric = null,
        WrappedMatchInfo? match = null,
        WrappedMemberInfo? mentioned = null,
        string? gradient = null) =>
        new()
        {
            StoryId = storyId,
            SlideType = slideType,
            FallbackLevel = fallbackLevel,
            Headline = headline,
            Subtitle = subtitle,
            BodyText = body,
            HighlightMetric = highlightMetric,
            MatchTitle = match is null ? null : $"{match.HomeName} vs {match.AwayName}",
            ScoreDisplay = match is null ? null : $"{match.HomeScore} - {match.AwayScore}",
            MentionedUserName = mentioned?.DisplayName,
            MentionedUserAvatarUrl = mentioned?.AvatarUrl,
            BackgroundGradient = gradient ?? WrappedCopy.GradientFor(storyId)
        };

    private static string DisplayPick(string? abbreviation, WrappedMatchInfo match)
    {
        var normalized = WrappedSeasonData.Normalize(abbreviation);
        if (normalized == WrappedSeasonData.DrawAbbr) return "el empate";
        if (string.Equals(normalized, WrappedSeasonData.Normalize(match.HomeAbbr), StringComparison.Ordinal)) return match.HomeName;
        if (string.Equals(normalized, WrappedSeasonData.Normalize(match.AwayAbbr), StringComparison.Ordinal)) return match.AwayName;
        return normalized;
    }

    private static int GoalDifference(WrappedMatchInfo match) => Math.Abs(match.HomeScore - match.AwayScore);

    // CONTRACT_MISMATCH: WrappedPickInfo de un empate fallido no identifica que equipo "empato";
    // se toma el local del partido como representante determinista del cruce.
    private static string? ClosestTeam(WrappedMatchInfo match) =>
        string.IsNullOrWhiteSpace(match.HomeAbbr) ? match.AwayAbbr : match.HomeAbbr;
    private static bool IsLastMinute(WrappedMatchInfo match, WrappedPickInfo pick)
    {
        if (!match.WeekFirstGameUtc.HasValue) return false;

        var hours = (match.WeekFirstGameUtc.Value - pick.SubmittedUtc).TotalHours;
        // Cambio de ultima hora = enviado en la hora previa al primer partido (nunca despues del cierre).
        return hours is >= 0d and < 1d;
    }

    private static int PercentageValue(string? percentLabel)
    {
        if (string.IsNullOrEmpty(percentLabel)) return 0;
        var digits = new string(percentLabel.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var value) ? value : 0;
    }
}
