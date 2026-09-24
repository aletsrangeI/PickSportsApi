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
            StatusState = "post"
        };

        // Partido con sorpresa: solo 1 de 10 participantes acertó visita
        var matchUpset = new Match
        {
            Id = 2,
            HomeScore = 0,
            AwayScore = 3, // Diferencia de 3 goles
            WinnerAbbr = "MAZ",
            StatusState = "post"
        };

        var matches = new List<Match> { match00, matchUpset };

        // Participante 1: apostó local en el 0-0 (Somnífero) y apostó local en la goleada 0-3 (Humillación)
        var p1Pick1 = new Pick { MatchId = 1, MemberId = 1, PickAbbr = "TIG" };
        var p1Pick2 = new Pick { MatchId = 2, MemberId = 1, PickAbbr = "AME" };

        // Participante 2: apostó MAZ en la sorpresa (Upset hit)
        var p2Pick1 = new Pick { MatchId = 1, MemberId = 2, PickAbbr = "EMPATE" };
        var p2Pick2 = new Pick { MatchId = 2, MemberId = 2, PickAbbr = "MAZ" };

        var picks = new List<Pick> { p1Pick1, p1Pick2, p2Pick1, p2Pick2 };

        // WHEN: Se evalúan los picks con un grupo de 10 miembros
        _engine.EvaluatePicksAndMatches(matches, picks, totalQuinielaMembers: 10);

        // THEN: P1 tiene somnífero y humillación
        Assert.True(p1Pick1.IsSomnifero);
        Assert.True(p1Pick2.IsHumillacion);

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
}
