using Domain.Entities;
using UseCases.Scoring;
using Xunit;

namespace PickSportsApi.UnitTests.UseCasesTests;

public class ScoringEngineTests
{
    private readonly ScoringEngine _engine = new();

    [Fact]
    public void Escenario1_CalificacionDePartido_AciertosYFallosCorrectos()
    {
        // GIVEN: Partido finalizado con resultado "AME 2 - 1 CHI" (ganador "AME")
        var match = new Match
        {
            Id = 1,
            HomeScore = 2,
            AwayScore = 1,
            WinnerAbbr = "AME",
            StatusState = "post"
        };

        var pick1 = new Pick { MatchId = 1, MemberId = 10, PickAbbr = "AME" };
        var pick2 = new Pick { MatchId = 1, MemberId = 20, PickAbbr = "CHI" };
        var pick3 = new Pick { MatchId = 1, MemberId = 30, PickAbbr = "EMPATE" };

        var matches = new List<Match> { match };
        var picks = new List<Pick> { pick1, pick2, pick3 };

        // WHEN: Se ejecuta la calificación
        _engine.EvaluatePicksAndMatches(matches, picks, totalQuinielaMembers: 3);

        // THEN: Los picks con AME se marcan como IsHit = true, los demás como false
        Assert.True(pick1.IsHit);
        Assert.False(pick2.IsHit);
        Assert.False(pick3.IsHit);
        Assert.False(pick1.IsHumillacion);
        Assert.False(pick2.IsHumillacion);
        Assert.False(pick3.IsHumillacion);
    }

    [Fact]
    public void Escenario2_DesempateEnCascadaEstricto_GanaMasUpsets()
    {
        // GIVEN: Alex (7 hits, 2 upsets, 1 humillacion) y Fer (7 hits, 1 upset, 0 humillaciones)
        var memberAlex = new QuinielaMember { Id = 1, Alias = "Alex" };
        var memberFer = new QuinielaMember { Id = 2, Alias = "Fer" };

        var match1 = new Match { Id = 1, StatusState = "post", WinnerAbbr = "A" };
        var match2 = new Match { Id = 2, StatusState = "post", WinnerAbbr = "B" };

        // Simulamos picks ya evaluados
        var picks = new List<Pick>
        {
            // Alex: 7 hits, 2 upsets, 1 humillacion
            new() { MemberId = 1, MatchId = 1, IsHit = true, IsUpsetHit = true },
            new() { MemberId = 1, MatchId = 2, IsHit = true, IsUpsetHit = true },
            new() { MemberId = 1, MatchId = 3, IsHit = true, IsUpsetHit = false },
            new() { MemberId = 1, MatchId = 4, IsHit = true, IsUpsetHit = false },
            new() { MemberId = 1, MatchId = 5, IsHit = true, IsUpsetHit = false },
            new() { MemberId = 1, MatchId = 6, IsHit = true, IsUpsetHit = false },
            new() { MemberId = 1, MatchId = 7, IsHit = true, IsUpsetHit = false },
            new() { MemberId = 1, MatchId = 8, IsHit = false, IsHumillacion = true },

            // Fer: 7 hits, 1 upset, 0 humillaciones
            new() { MemberId = 2, MatchId = 1, IsHit = true, IsUpsetHit = true },
            new() { MemberId = 2, MatchId = 2, IsHit = true, IsUpsetHit = false },
            new() { MemberId = 2, MatchId = 3, IsHit = true, IsUpsetHit = false },
            new() { MemberId = 2, MatchId = 4, IsHit = true, IsUpsetHit = false },
            new() { MemberId = 2, MatchId = 5, IsHit = true, IsUpsetHit = false },
            new() { MemberId = 2, MatchId = 6, IsHit = true, IsUpsetHit = false },
            new() { MemberId = 2, MatchId = 7, IsHit = true, IsUpsetHit = false },
            new() { MemberId = 2, MatchId = 8, IsHit = false, IsHumillacion = false }
        };

        var allMatches = Enumerable.Range(1, 8).Select(i => new Match { Id = i, StatusState = "post", WinnerAbbr = "X" }).ToList();

        // WHEN: Se calcula la tabla semanal
        var standings = _engine.CalculateWeeklyStandings(new[] { memberAlex, memberFer }, picks, allMatches);

        // THEN: Alex queda en 1° lugar sobre Fer por tener más Upsets (2 vs 1)
        Assert.Equal(2, standings.Count);
        Assert.Equal("Alex", standings[0].Alias);
        Assert.Equal(1, standings[0].Rank);
        Assert.Equal("Fer", standings[1].Alias);
        Assert.Equal(2, standings[1].Rank);
    }

    [Fact]
    public void Escenario3_DesempatePorMenosHumillaciones_GanaMenosHumillaciones()
    {
        // GIVEN: Gigi (6 hits, 1 upset, 0 humillaciones) y Tanis (6 hits, 1 upset, 2 humillaciones)
        var memberGigi = new QuinielaMember { Id = 3, Alias = "Gigi" };
        var memberTanis = new QuinielaMember { Id = 4, Alias = "Tanis" };

        var picks = new List<Pick>
        {
            // Gigi: 6 hits, 1 upset, 0 humillaciones
            new() { MemberId = 3, MatchId = 1, IsHit = true, IsUpsetHit = true },
            new() { MemberId = 3, MatchId = 2, IsHit = true },
            new() { MemberId = 3, MatchId = 3, IsHit = true },
            new() { MemberId = 3, MatchId = 4, IsHit = true },
            new() { MemberId = 3, MatchId = 5, IsHit = true },
            new() { MemberId = 3, MatchId = 6, IsHit = true },
            new() { MemberId = 3, MatchId = 7, IsHit = false, IsHumillacion = false },

            // Tanis: 6 hits, 1 upset, 2 humillaciones
            new() { MemberId = 4, MatchId = 1, IsHit = true, IsUpsetHit = true },
            new() { MemberId = 4, MatchId = 2, IsHit = true },
            new() { MemberId = 4, MatchId = 3, IsHit = true },
            new() { MemberId = 4, MatchId = 4, IsHit = true },
            new() { MemberId = 4, MatchId = 5, IsHit = true },
            new() { MemberId = 4, MatchId = 6, IsHit = true },
            new() { MemberId = 4, MatchId = 7, IsHit = false, IsHumillacion = true },
            new() { MemberId = 4, MatchId = 8, IsHit = false, IsHumillacion = true }
        };

        var allMatches = Enumerable.Range(1, 8).Select(i => new Match { Id = i, StatusState = "post", WinnerAbbr = "X" }).ToList();

        // WHEN: Se calcula la tabla semanal
        var standings = _engine.CalculateWeeklyStandings(new[] { memberTanis, memberGigi }, picks, allMatches);

        // THEN: Gigi queda arriba de Tanis por tener menos humillaciones (0 vs 2)
        Assert.Equal(2, standings.Count);
        Assert.Equal("Gigi", standings[0].Alias);
        Assert.Equal(1, standings[0].Rank);
        Assert.Equal("Tanis", standings[1].Alias);
        Assert.Equal(2, standings[1].Rank);
    }

    [Fact]
    public void Escenario4_AsignacionDeGalardones_SomniferoMVPYSorpresas()
    {
        // GIVEN: Jornada con partido 0-0 y el jugador apostó local
        var match00 = new Match
        {
            Id = 1,
            HomeScore = 0,
            AwayScore = 0,
            WinnerAbbr = "EMPATE",
            StatusState = "post",
            HomeTeam = new Team { Abbreviation = "TIG" },
            AwayTeam = new Team { Abbreviation = "LEO" }
        };

        // Partido con sorpresa: solo 1 de 10 participantes acertó visita
        var matchUpset = new Match
        {
            Id = 2,
            HomeScore = 0,
            AwayScore = 3, // Diferencia de 3 goles
            WinnerAbbr = "MAZ",
            StatusState = "post",
            HomeTeam = new Team { Abbreviation = "AME" },
            AwayTeam = new Team { Abbreviation = "MAZ" }
        };

        var matchDrawFailed = new Match
        {
            Id = 3,
            HomeScore = 2,
            AwayScore = 1,
            WinnerAbbr = "TIG",
            StatusState = "post",
            HomeTeam = new Team { Abbreviation = "TIG" },
            AwayTeam = new Team { Abbreviation = "LEO" }
        };

        var matches = new List<Match> { match00, matchUpset, matchDrawFailed };

        // Participante 1: apostó local en el 0-0 (Somnífero) y apostó local en la goleada 0-3 (Humillación)
        var p1Pick1 = new Pick { MatchId = 1, MemberId = 1, PickAbbr = "TIG" };
        var p1Pick2 = new Pick { MatchId = 2, MemberId = 1, PickAbbr = "AME" };
        var p1Pick3 = new Pick { MatchId = 3, MemberId = 1, PickAbbr = "EMPATE" };

        // Participante 2: apostó MAZ en la sorpresa (Upset hit)
        var p2Pick1 = new Pick { MatchId = 1, MemberId = 2, PickAbbr = "EMPATE" };
        var p2Pick2 = new Pick { MatchId = 2, MemberId = 2, PickAbbr = "MAZ" };
        var p2Pick3 = new Pick { MatchId = 3, MemberId = 2, PickAbbr = "TIG" };

        var picks = new List<Pick> { p1Pick1, p1Pick2, p1Pick3, p2Pick1, p2Pick2, p2Pick3 };

        // WHEN: Se evalúan los picks con un grupo de 10 miembros
        _engine.EvaluatePicksAndMatches(matches, picks, totalQuinielaMembers: 10);

        // THEN: P1 tiene somnífero y humillación
        Assert.True(p1Pick1.IsSomnifero);
        Assert.True(p1Pick2.IsHumillacion);
        Assert.True(p1Pick3.IsEmpateFallido);

        // P2 tiene acierto de sorpresa
        Assert.True(p2Pick2.IsUpsetHit);
        Assert.True(matchUpset.IsUpset);

        // Galardones
        var m1 = new QuinielaMember { Id = 1, Alias = "Lalo" };
        var m2 = new QuinielaMember { Id = 2, Alias = "Carlos" };
        var standings = _engine.CalculateWeeklyStandings(new[] { m1, m2 }, picks, matches);

        var awards = _engine.CalculateAwards(100, 1, standings, matches, picks);

        Assert.Contains(awards, a => a.AwardType == "MVP" && a.MemberId == 2);
        Assert.Contains(awards, a => a.AwardType == "SOMNIFERO" && a.MemberId == 1);
        Assert.Contains(awards, a => a.AwardType == "HUMILLADO" && a.MemberId == 1);
        Assert.Contains(awards, a => a.AwardType == "REY_SORPRESAS" && a.MemberId == 2);
        Assert.Contains(awards, a => a.AwardType == "EMPATE_FALLIDO" && a.MemberId == 1);
        Assert.Contains(awards, a => a.AwardType == "SOMNIFERO" && a.Notes!.Contains("TIG 0-0 LEO (pronóstico TIG)"));
        Assert.Contains(awards, a => a.AwardType == "HUMILLADO" && a.Notes!.Contains("AME 0-3 MAZ (pronóstico AME)"));
        Assert.Contains(awards, a => a.AwardType == "REY_SORPRESAS" && a.Notes!.Contains("AME 0-3 MAZ (pronóstico MAZ)"));
        Assert.Contains(awards, a => a.AwardType == "EMPATE_FALLIDO" && a.Notes!.Contains("TIG 2-1 LEO (pronóstico EMPATE)"));
        Assert.All(awards.Where(a => a.AwardType is "REY_SORPRESAS" or "HUMILLADO" or "SOMNIFERO" or "EMPATE_FALLIDO"),
            award => Assert.True(award.Notes!.Length <= 500));
    }

    [Fact]
    public void ReglaEmpateFallido_ApostóEmpatePeroHuboGanador_SeMarcaCorrectamente()
    {
        var match = new Match
        {
            Id = 5,
            HomeScore = 2,
            AwayScore = 0,
            WinnerAbbr = "LEO",
            StatusState = "post"
        };

        var pick = new Pick { MatchId = 5, MemberId = 10, PickAbbr = "EMPATE" };
        _engine.EvaluatePicksAndMatches(new[] { match }, new[] { pick }, totalQuinielaMembers: 5);

        Assert.False(pick.IsHit);
        Assert.True(pick.IsEmpateFallido);
        Assert.False(pick.IsHumillacion); // Empates rotos no son humillaciones
    }

    [Fact]
    public void CalculateMemberStreaks_CalculaRachaActualYMejorRacha()
    {
        var picks = new List<Pick>
        {
            new() { IsHit = true },
            new() { IsHit = true },
            new() { IsHit = true }, // racha de 3
            new() { IsHit = false },
            new() { IsHit = true },
            new() { IsHit = true }  // racha actual de 2
        };

        var (current, best) = _engine.CalculateMemberStreaks(picks);

        Assert.Equal(2, current);
        Assert.Equal(3, best);
    }

    [Fact]
    public void ApplyRecidivistAutofillPenalty_MiembroConTresSemanasAutollenado_SeTopaAlPeorHumanoMenosUno()
    {
        // ARRANGE
        var memberManual1 = new QuinielaMember { Id = 1, Alias = "Alex" };
        var memberManual2 = new QuinielaMember { Id = 2, Alias = "Alejandra" };
        var memberRecidivist = new QuinielaMember { Id = 3, Alias = "Gigi" };
        var members = new List<QuinielaMember> { memberManual1, memberManual2, memberRecidivist };

        // Semana 10 (actual)
        var w10Matches = Enumerable.Range(1, 9).Select(i => new Match { Id = i }).ToList();
        var matchWeekMap = w10Matches.ToDictionary(m => m.Id, m => 10);

        // Gigi tuvo semanas 8 y 9 también autollenadas
        var historicalPicks = new List<Pick>();
        for (int w = 8; w <= 9; w++)
        {
            for (int i = 100 * w; i < 100 * w + 9; i++)
            {
                historicalPicks.Add(new Pick { MemberId = 3, MatchId = i, IsAutoFilled = true });
                matchWeekMap[i] = w;
            }
        }

        // Semana 10 picks
        var week10Picks = new List<Pick>();
        // Manual 1: 5 aciertos
        for (int i = 1; i <= 9; i++)
            week10Picks.Add(new Pick { MemberId = 1, MatchId = i, IsAutoFilled = false, IsHit = i <= 5 });

        // Manual 2: 3 aciertos (min manual = 3 => tope = 3 - 1 = 2)
        for (int i = 1; i <= 9; i++)
            week10Picks.Add(new Pick { MemberId = 2, MatchId = i, IsAutoFilled = false, IsHit = i <= 3 });

        // Gigi: 6 aciertos y 2 sorpresas por azar
        for (int i = 1; i <= 9; i++)
            week10Picks.Add(new Pick { MemberId = 3, MatchId = i, IsAutoFilled = true, IsHit = i <= 6, IsUpsetHit = i <= 2 });

        // ACT
        _engine.ApplyRecidivistAutofillPenalty(members, week10Picks, historicalPicks, currentWeekNumber: 10, matchWeekNumbers: matchWeekMap);

        // ASSERT: Gigi debe quedar topada a 2 aciertos (3 - 1) y 0 upsets
        var gigiPicks = week10Picks.Where(p => p.MemberId == 3).ToList();
        Assert.Equal(2, gigiPicks.Count(p => p.IsHit == true));
        Assert.Equal(0, gigiPicks.Count(p => p.IsUpsetHit));
    }

    [Fact]
    public void ApplyRecidivistAutofillPenalty_MiembroConUnaSemanaAutollenado_NoEsCastigado()
    {
        // ARRANGE: César sólo tiene 1 semana autollenada (semana 10), semanas 8 y 9 fueron manuales
        var memberManual = new QuinielaMember { Id = 1, Alias = "Alex" };
        var memberFirstTime = new QuinielaMember { Id = 2, Alias = "César" };
        var members = new List<QuinielaMember> { memberManual, memberFirstTime };

        var matchWeekMap = new Dictionary<int, int>();
        for (int i = 1; i <= 9; i++) matchWeekMap[i] = 10;
        for (int i = 10; i <= 18; i++) matchWeekMap[i] = 9;

        var historicalPicks = new List<Pick>();
        // Semana 9 de César fue manual
        for (int i = 10; i <= 18; i++)
            historicalPicks.Add(new Pick { MemberId = 2, MatchId = i, IsAutoFilled = false, IsHit = true });

        var week10Picks = new List<Pick>();
        // Alex sacó 2 aciertos (si fuera castigado, el tope sería 1)
        for (int i = 1; i <= 9; i++)
            week10Picks.Add(new Pick { MemberId = 1, MatchId = i, IsAutoFilled = false, IsHit = i <= 2 });

        // César sacó 4 aciertos autollenados
        for (int i = 1; i <= 9; i++)
            week10Picks.Add(new Pick { MemberId = 2, MatchId = i, IsAutoFilled = true, IsHit = i <= 4, IsUpsetHit = i == 1 });

        // ACT
        _engine.ApplyRecidivistAutofillPenalty(members, week10Picks, historicalPicks, currentWeekNumber: 10, matchWeekNumbers: matchWeekMap);

        // ASSERT: Como no es reincidente (streak = 1 < 3), conserva sus 4 aciertos y su upset
        var cesarPicks = week10Picks.Where(p => p.MemberId == 2).ToList();
        Assert.Equal(4, cesarPicks.Count(p => p.IsHit == true));
        Assert.Equal(1, cesarPicks.Count(p => p.IsUpsetHit));
    }

    /// <summary>
    /// Arma el escenario de Gigi autollenando la semana 10 de la temporada 2, con semanas 8 y 9 autollenadas
    /// en la temporada indicada. Alex es el único humano con 3 aciertos (tope de castigo = 2).
    /// </summary>
    private static (List<QuinielaMember> Members, List<Pick> Week10Picks, List<Pick> Historical) BuildRecidivistSeasonScenario(int historicalSeasonId)
    {
        var members = new List<QuinielaMember>
        {
            new() { Id = 1, Alias = "Alex" },
            new() { Id = 3, Alias = "Gigi" }
        };

        var historical = new List<Pick>();
        for (int w = 8; w <= 9; w++)
        {
            var week = new Week { Id = w, SeasonId = historicalSeasonId, WeekNumber = w };
            for (int i = 100 * w; i < 100 * w + 9; i++)
                historical.Add(new Pick { MemberId = 3, MatchId = i, IsAutoFilled = true, Match = new Match { Id = i, Week = week } });
        }

        var currentWeek = new Week { Id = 10, SeasonId = 2, WeekNumber = 10 };
        var week10Picks = new List<Pick>();
        for (int i = 1; i <= 9; i++)
        {
            week10Picks.Add(new Pick { MemberId = 1, MatchId = i, IsAutoFilled = false, IsHit = i <= 3, Match = new Match { Id = i, Week = currentWeek } });
            week10Picks.Add(new Pick { MemberId = 3, MatchId = i, IsAutoFilled = true, IsHit = i <= 6, IsUpsetHit = i <= 2, Match = new Match { Id = i, Week = currentWeek } });
        }

        return (members, week10Picks, historical);
    }

    [Fact]
    public void ApplyRecidivistAutofillPenalty_ConSeasonId_IgnoraAutollenadosDeOtraTemporada()
    {
        // ARRANGE: las semanas 8 y 9 autollenadas son de la temporada 1; la semana 10 es de la temporada 2
        var (members, week10Picks, historical) = BuildRecidivistSeasonScenario(historicalSeasonId: 1);

        // ACT
        _engine.ApplyRecidivistAutofillPenalty(members, week10Picks, historical, currentWeekNumber: 10, seasonId: 2);

        // ASSERT: en la temporada 2 Gigi solo lleva 1 semana autollenada, no es reincidente
        var gigiPicks = week10Picks.Where(p => p.MemberId == 3).ToList();
        Assert.Equal(6, gigiPicks.Count(p => p.IsHit == true));
        Assert.Equal(2, gigiPicks.Count(p => p.IsUpsetHit));
    }

    [Fact]
    public void ApplyRecidivistAutofillPenalty_ConSeasonId_MismaTemporada_ConservaElCastigo()
    {
        // ARRANGE: las semanas 8, 9 y 10 autollenadas son todas de la temporada 2
        var (members, week10Picks, historical) = BuildRecidivistSeasonScenario(historicalSeasonId: 2);

        // ACT
        _engine.ApplyRecidivistAutofillPenalty(members, week10Picks, historical, currentWeekNumber: 10, seasonId: 2);

        // ASSERT: mismo resultado que sin blindaje, Gigi topada a 2 aciertos (3 - 1) y 0 sorpresas
        var gigiPicks = week10Picks.Where(p => p.MemberId == 3).ToList();
        Assert.Equal(2, gigiPicks.Count(p => p.IsHit == true));
        Assert.Equal(0, gigiPicks.Count(p => p.IsUpsetHit));
    }

    [Fact]
    public void CalculateWeeklyStandings_EmpateEnAciertos_JugadorManualGanaDesempateSobreAutollenado()
    {
        // ARRANGE: Dos jugadores empatados en todo, pero uno llenó manual y el otro fue autollenado
        var memberManual = new QuinielaMember { Id = 1, Alias = "B_Manual" };
        var memberAuto = new QuinielaMember { Id = 2, Alias = "A_Auto" }; // Nombre 'A' para probar que no gane por orden alfabético
        var members = new List<QuinielaMember> { memberManual, memberAuto };

        var matches = new List<Match> { new() { Id = 1, StatusState = "post", WinnerAbbr = "AME" } };

        var picks = new List<Pick>
        {
            new() { MemberId = 1, MatchId = 1, IsHit = true, IsAutoFilled = false },
            new() { MemberId = 2, MatchId = 1, IsHit = true, IsAutoFilled = true }
        };

        // ACT
        var standings = _engine.CalculateWeeklyStandings(members, picks, matches);

        // ASSERT: El jugador manual debe quedar en Rank 1, y el bot en Rank 2
        Assert.Equal("B_Manual", standings[0].Alias);
        Assert.Equal(1, standings[0].Rank);
        Assert.Equal("A_Auto", standings[1].Alias);
        Assert.Equal(2, standings[1].Rank);
    }

    #region Tabla General - Desempate (H-005)

    // "Ana" ganaría por orden alfabético; los criterios deportivos deben poder favorecer a "Zoe".
    private static readonly List<QuinielaMember> TiebreakMembers = new()
    {
        new() { Id = 1, UserId = 1, Alias = "Ana" },
        new() { Id = 2, UserId = 2, Alias = "Zoe" }
    };

    private static Week SeasonWeek(int weekNumber, int seasonId = 1) =>
        new() { Id = seasonId * 100 + weekNumber, SeasonId = seasonId, WeekNumber = weekNumber };

    /// <summary>
    /// Genera picks calificados: por cada jornada, la cantidad de aciertos indicada más un fallo.
    /// </summary>
    private static List<Pick> PicksByWeek(int memberId, params (Week Week, int Hits)[] weeks)
    {
        var picks = new List<Pick>();
        foreach (var (week, hits) in weeks)
        {
            for (int i = 0; i <= hits; i++)
            {
                int matchId = week.Id * 10 + i;
                picks.Add(new Pick { MemberId = memberId, MatchId = matchId, IsHit = i < hits, Match = new Match { Id = matchId, Week = week } });
            }
        }
        return picks;
    }

    private static WeeklyAward Mvp(int memberId, Week week) =>
        new() { MemberId = memberId, AwardType = "MVP", Week = week, WeekId = week.Id };

    [Fact]
    public void CalculateGeneralStandings_EmpateEnAciertos_GanaQuienTieneMasJornadasComoMvp()
    {
        // ARRANGE: ambos con 2 aciertos; Ana tuvo mejor jornada reciente, pero Zoe fue MVP 2 veces y Ana 1
        var (w1, w2, w3) = (SeasonWeek(1), SeasonWeek(2), SeasonWeek(3));
        var picks = PicksByWeek(1, (w1, 0), (w2, 0), (w3, 2));
        picks.AddRange(PicksByWeek(2, (w1, 2), (w2, 0), (w3, 0)));
        var awards = new List<WeeklyAward> { Mvp(2, w1), Mvp(2, w2), Mvp(1, w3) };

        // ACT
        var standings = _engine.CalculateGeneralStandings(TiebreakMembers, picks, seasonId: 1, uptoWeekNumber: 3, awards);

        // ASSERT
        Assert.Equal("Zoe", standings[0].Alias);
        Assert.Equal("Ana", standings[1].Alias);
    }

    [Fact]
    public void CalculateGeneralStandings_EmpateEnAciertosYMvp_GanaMejorJornadaMasReciente()
    {
        // ARRANGE: ambos con 3 aciertos y sin MVP; en la jornada 2 Zoe hizo 2 y Ana 1
        var (w1, w2) = (SeasonWeek(1), SeasonWeek(2));
        var picks = PicksByWeek(1, (w1, 2), (w2, 1));
        picks.AddRange(PicksByWeek(2, (w1, 1), (w2, 2)));

        // ACT
        var standings = _engine.CalculateGeneralStandings(TiebreakMembers, picks, seasonId: 1, uptoWeekNumber: 2);

        // ASSERT
        Assert.Equal("Zoe", standings[0].Alias);
        Assert.Equal(1, standings[0].Rank);
        Assert.Equal("Ana", standings[1].Alias);
        Assert.Equal(2, standings[1].Rank);
    }

    [Fact]
    public void CalculateGeneralStandings_EmpateEnJornadaMasReciente_ComparaLaJornadaAnterior()
    {
        // ARRANGE: jornada 3 empatada (1 y 1); en la jornada 2 Zoe hizo 1 y Ana 0
        var (w1, w2, w3) = (SeasonWeek(1), SeasonWeek(2), SeasonWeek(3));
        var picks = PicksByWeek(1, (w1, 2), (w2, 0), (w3, 1));
        picks.AddRange(PicksByWeek(2, (w1, 1), (w2, 1), (w3, 1)));

        // ACT
        var standings = _engine.CalculateGeneralStandings(TiebreakMembers, picks, seasonId: 1, uptoWeekNumber: 3);

        // ASSERT
        Assert.Equal("Zoe", standings[0].Alias);
        Assert.Equal("Ana", standings[1].Alias);
    }

    [Fact]
    public void CalculateGeneralStandings_IgnoraMvpsDeOtraTemporada()
    {
        // ARRANGE: jornadas idénticas; los MVP de Zoe son de otra temporada y no deben contar
        var w1 = SeasonWeek(1);
        var picks = PicksByWeek(1, (w1, 1));
        picks.AddRange(PicksByWeek(2, (w1, 1)));
        var awards = new List<WeeklyAward> { Mvp(2, SeasonWeek(1, seasonId: 99)), Mvp(2, SeasonWeek(2, seasonId: 99)) };

        // ACT
        var standings = _engine.CalculateGeneralStandings(TiebreakMembers, picks, seasonId: 1, uptoWeekNumber: 1, awards);

        // ASSERT: empate total, aplica el último recurso técnico
        Assert.Equal("Ana", standings[0].Alias);
        Assert.Equal("Zoe", standings[1].Alias);
    }

    [Fact]
    public void CalculateGeneralStandings_PosicionAnteriorSoloCuentaMvpsHastaLaJornadaAnterior()
    {
        // ARRANGE: jornadas idénticas; Zoe fue MVP de la jornada 2 (la actual)
        var (w1, w2) = (SeasonWeek(1), SeasonWeek(2));
        var picks = PicksByWeek(1, (w1, 1), (w2, 1));
        picks.AddRange(PicksByWeek(2, (w1, 1), (w2, 1)));
        var awards = new List<WeeklyAward> { Mvp(2, w2) };

        // ACT
        var standings = _engine.CalculateGeneralStandings(TiebreakMembers, picks, seasonId: 1, uptoWeekNumber: 2, awards);

        // ASSERT: en la jornada 1 Zoe era 2°; con el MVP de la jornada 2 sube a 1°
        var zoe = standings.Single(s => s.Alias == "Zoe");
        Assert.Equal(1, zoe.Rank);
        Assert.Equal(2, zoe.PreviousRank);
        Assert.Equal(1, zoe.RankDelta);
    }

    [Fact]
    public void CalculateGeneralStandings_SorpresasPesanMasQueMvp()
    {
        // ARRANGE: mismos aciertos; Ana tiene 1 sorpresa y Zoe 1 MVP
        var w1 = SeasonWeek(1);
        var picks = PicksByWeek(1, (w1, 1));
        picks.AddRange(PicksByWeek(2, (w1, 1)));
        picks.First(p => p.MemberId == 1 && p.IsHit == true).IsUpsetHit = true;
        var awards = new List<WeeklyAward> { Mvp(2, w1) };

        // ACT
        var standings = _engine.CalculateGeneralStandings(TiebreakMembers, picks, seasonId: 1, uptoWeekNumber: 1, awards);

        // ASSERT
        Assert.Equal("Ana", standings[0].Alias);
    }

    #endregion
}
