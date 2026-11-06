using Domain.Entities;
using Domain.Gamification;
using FluentAssertions;
using UseCases.Gamification;
using UseCases.Scoring;
using M = Domain.Entities.Match;

namespace PickSportsApi.UnitTests.UseCasesTests;

/// <summary>Constructores de escenarios compartidos por las pruebas de trofeos (SPEC-013).</summary>
internal static class TrophyScenario
{
    public static M Match(int id, int weekId, int seasonId = 1, int day = 0, bool upset = false) => new()
    {
        Id = id,
        WeekId = weekId,
        Week = new Week { Id = weekId, SeasonId = seasonId, WeekNumber = weekId },
        StatusState = "post",
        WinnerAbbr = "A",
        IsUpset = upset,
        DateUtc = new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc).AddDays(day)
    };

    public static Pick Pick(int memberId, M match, bool? hit, bool upsetHit = false, bool auto = false) => new()
    {
        MemberId = memberId,
        MatchId = match.Id,
        Match = match,
        IsHit = hit,
        IsUpsetHit = upsetHit,
        IsAutoFilled = auto,
        PickAbbr = hit == true ? "A" : "B"
    };

    public static List<QuinielaMember> Members(int count) =>
        Enumerable.Range(1, count).Select(i => new QuinielaMember { Id = i, UserId = 100 + i, Alias = $"M{i}" }).ToList();

    /// <summary>Una jornada con <paramref name="count"/> partidos; devuelve los partidos.</summary>
    public static List<M> WeekMatches(int weekId, int count, int seasonId = 1, int firstMatchId = 0)
    {
        int start = firstMatchId > 0 ? firstMatchId : weekId * 100;
        return Enumerable.Range(0, count).Select(i => Match(start + i, weekId, seasonId, day: weekId * 10 + i)).ToList();
    }
}

public class AchievementEvaluatorTests
{
    private readonly AchievementEvaluator _sut = new(new ScoringEngine());

    private HashSet<string> CodesFor(int memberId, IReadOnlyList<QuinielaMember> members, IReadOnlyList<Pick> picks, IReadOnlyList<WeeklyAward>? awards = null)
    {
        var result = _sut.Evaluate(members, picks, awards ?? new List<WeeklyAward>());
        return result.TryGetValue(memberId, out var set) ? set : new HashSet<string>();
    }

    // ---- PRIMERA_SANGRE ----
    [Fact]
    public void PrimeraSangre_ConUnAcierto_SeOtorga()
    {
        var m = TrophyScenario.Match(1, 1);
        var picks = new[] { TrophyScenario.Pick(1, m, true) };

        CodesFor(1, TrophyScenario.Members(1), picks).Should().Contain(AchievementCatalog.PrimeraSangre);
    }

    [Fact]
    public void PrimeraSangre_SoloFallos_NoSeOtorga()
    {
        var m = TrophyScenario.Match(1, 1);
        var picks = new[] { TrophyScenario.Pick(1, m, false) };

        CodesFor(1, TrophyScenario.Members(1), picks).Should().NotContain(AchievementCatalog.PrimeraSangre);
    }

    [Fact]
    public void MiembroSinPicksCalificados_NoRecibeNada()
    {
        var m = TrophyScenario.Match(1, 1);
        var picks = new[] { TrophyScenario.Pick(1, m, null) };

        _sut.Evaluate(TrophyScenario.Members(1), picks, new List<WeeklyAward>()).Should().BeEmpty();
    }

    // ---- OLFATO_DE_SORPRESA ----
    [Fact]
    public void OlfatoDeSorpresa_ConAciertoUpset_SeOtorga()
    {
        var m = TrophyScenario.Match(1, 1, upset: true);
        var picks = new[] { TrophyScenario.Pick(1, m, true, upsetHit: true) };

        CodesFor(1, TrophyScenario.Members(1), picks).Should().Contain(AchievementCatalog.OlfatoDeSorpresa);
    }

    [Fact]
    public void OlfatoDeSorpresa_AciertoSinUpset_NoSeOtorga()
    {
        var m = TrophyScenario.Match(1, 1);
        var picks = new[] { TrophyScenario.Pick(1, m, true) };

        CodesFor(1, TrophyScenario.Members(1), picks).Should().NotContain(AchievementCatalog.OlfatoDeSorpresa);
    }

    // ---- JORNADA_COMPLETA ----
    [Fact]
    public void JornadaCompleta_TodoManualYCubreLaJornada_SeOtorga()
    {
        var ms = TrophyScenario.WeekMatches(1, 3);
        var picks = ms.Select(m => TrophyScenario.Pick(1, m, false)).ToList();

        CodesFor(1, TrophyScenario.Members(1), picks).Should().Contain(AchievementCatalog.JornadaCompleta);
    }

    [Fact]
    public void JornadaCompleta_ConUnPickAutollenado_NoSeOtorga()
    {
        var ms = TrophyScenario.WeekMatches(1, 3);
        var picks = ms.Select((m, i) => TrophyScenario.Pick(1, m, false, auto: i == 2)).ToList();

        CodesFor(1, TrophyScenario.Members(1), picks).Should().NotContain(AchievementCatalog.JornadaCompleta);
    }

    [Fact]
    public void JornadaCompleta_SinPronosticarTodosLosPartidos_NoSeOtorga()
    {
        var ms = TrophyScenario.WeekMatches(1, 3);
        var picks = new List<Pick>
        {
            TrophyScenario.Pick(1, ms[0], false), TrophyScenario.Pick(1, ms[1], false),
            TrophyScenario.Pick(2, ms[0], false), TrophyScenario.Pick(2, ms[1], false), TrophyScenario.Pick(2, ms[2], false)
        };

        CodesFor(1, TrophyScenario.Members(2), picks).Should().NotContain(AchievementCatalog.JornadaCompleta);
    }

    // ---- FIEL_AL_COLISEO ----
    [Theory]
    [InlineData(5, true)]
    [InlineData(4, false)]
    public void FielAlColiseo_DependeDeJugarCincoJornadas(int weeks, bool expected)
    {
        var picks = Enumerable.Range(1, weeks)
            .Select(w => TrophyScenario.Pick(1, TrophyScenario.Match(w, w), false))
            .ToList();

        CodesFor(1, TrophyScenario.Members(1), picks).Contains(AchievementCatalog.FielAlColiseo).Should().Be(expected);
    }

    // ---- RACHA_DE_FUEGO ----
    [Fact]
    public void RachaDeFuego_CincoAciertosSeguidosPorFecha_SeOtorga()
    {
        var ms = TrophyScenario.WeekMatches(1, 6);
        // Se entregan desordenados: el orden lo dicta Match.DateUtc, no la posicion en la lista.
        var picks = ms.Select(m => TrophyScenario.Pick(1, m, true)).Reverse().ToList();

        CodesFor(1, TrophyScenario.Members(1), picks).Should().Contain(AchievementCatalog.RachaDeFuego);
    }

    [Fact]
    public void RachaDeFuego_RachaRotaPorUnFallo_NoSeOtorga()
    {
        var ms = TrophyScenario.WeekMatches(1, 9);
        // 4 aciertos, 1 fallo, 4 aciertos: nunca 5 seguidos.
        var picks = ms.Select((m, i) => TrophyScenario.Pick(1, m, i != 4)).ToList();

        CodesFor(1, TrophyScenario.Members(1), picks).Should().NotContain(AchievementCatalog.RachaDeFuego);
    }

    [Fact]
    public void RachaDeFuego_AciertosRepartidosEnVariasJornadas_NoSeOtorga()
    {
        var picks = new List<Pick>();
        for (int w = 1; w <= 5; w++) picks.Add(TrophyScenario.Pick(1, TrophyScenario.Match(w, w), true));

        CodesFor(1, TrophyScenario.Members(1), picks).Should().NotContain(AchievementCatalog.RachaDeFuego);
    }

    // ---- CAZADOR_DE_SORPRESAS ----
    [Theory]
    [InlineData(5, true)]
    [InlineData(4, false)]
    public void CazadorDeSorpresas_DependeDeCincoAciertosUpset(int upsets, bool expected)
    {
        var picks = Enumerable.Range(1, upsets)
            .Select(i => TrophyScenario.Pick(1, TrophyScenario.Match(i, i, upset: true), true, upsetHit: true))
            .ToList();

        CodesFor(1, TrophyScenario.Members(1), picks).Contains(AchievementCatalog.CazadorDeSorpresas).Should().Be(expected);
    }

    // ---- EN_EL_PODIO ----
    [Fact]
    public void EnElPodio_TopTresDeUnaJornada_SeOtorgaAlTop3YNoAlCuarto()
    {
        var ms = TrophyScenario.WeekMatches(1, 4);
        var members = TrophyScenario.Members(4);
        var picks = new List<Pick>();
        int[] hitsPerMember = { 4, 3, 2, 1 };
        for (int mi = 0; mi < 4; mi++)
            for (int k = 0; k < 4; k++)
                picks.Add(TrophyScenario.Pick(mi + 1, ms[k], k < hitsPerMember[mi]));

        var result = _sut.Evaluate(members, picks, new List<WeeklyAward>());

        result[1].Should().Contain(AchievementCatalog.EnElPodio);
        result[3].Should().Contain(AchievementCatalog.EnElPodio);
        result[4].Should().NotContain(AchievementCatalog.EnElPodio);
    }

    [Theory]
    [InlineData(new[] { 3, 3, 3, 3, 3 }, new[] { 1, 2, 3, 4, 5 })]   // 5 empatados: los 5
    [InlineData(new[] { 5, 4, 3, 3 }, new[] { 1, 2, 3, 4 })]         // empate en el 3er lugar: los 4
    [InlineData(new[] { 5, 4, 3, 2 }, new[] { 1, 2, 3 })]            // sin empate: solo 3
    [InlineData(new[] { 0, 0, 0, 0 }, new int[0])]                   // nadie acerto: nadie
    [InlineData(new[] { 2, 0 }, new[] { 1 })]                        // menos de 3 miembros, sin aciertos no cuenta
    public void EnElPodio_UsaRankingDeCompeticionConEmpates(int[] hitsPerMember, int[] expectedMembers)
    {
        var ms = TrophyScenario.WeekMatches(1, 5);
        var members = TrophyScenario.Members(hitsPerMember.Length);
        var picks = new List<Pick>();
        for (int mi = 0; mi < hitsPerMember.Length; mi++)
            for (int k = 0; k < 5; k++)
                picks.Add(TrophyScenario.Pick(mi + 1, ms[k], k < hitsPerMember[mi]));

        var result = _sut.Evaluate(members, picks, new List<WeeklyAward>());

        var withPodium = members.Where(m => result.TryGetValue(m.Id, out var s) && s.Contains(AchievementCatalog.EnElPodio))
            .Select(m => m.Id).ToArray();
        withPodium.Should().BeEquivalentTo(expectedMembers);
    }

    [Fact]
    public void EnElPodio_SinAciertosEnLaJornada_NoSeOtorga()
    {
        var ms = TrophyScenario.WeekMatches(1, 2);
        var picks = new List<Pick>
        {
            TrophyScenario.Pick(1, ms[0], false), TrophyScenario.Pick(1, ms[1], false),
            TrophyScenario.Pick(2, ms[0], false), TrophyScenario.Pick(2, ms[1], false)
        };

        CodesFor(1, TrophyScenario.Members(2), picks).Should().NotContain(AchievementCatalog.EnElPodio);
    }

    // ---- CONSTANCIA_DE_ACERO ----
    [Theory]
    [InlineData(10, -1, true)]   // 10 jornadas manuales
    [InlineData(9, -1, false)]   // solo 9
    [InlineData(10, 4, false)]   // 10 jornadas pero una con autollenado cuenta 9
    public void ConstanciaDeAcero_DiezJornadasSinAutollenado(int weeks, int autoWeek, bool expected)
    {
        var picks = Enumerable.Range(1, weeks)
            .Select(w => TrophyScenario.Pick(1, TrophyScenario.Match(w, w), false, auto: w == autoWeek))
            .ToList();

        CodesFor(1, TrophyScenario.Members(1), picks).Contains(AchievementCatalog.ConstanciaDeAcero).Should().Be(expected);
    }

    // ---- SOLO_YO_LO_VI ----
    [Fact]
    public void SoloYoLoVi_UnicoAcertanteConCuatroMiembros_SeOtorgaSoloAEse()
    {
        var m = TrophyScenario.Match(1, 1);
        var picks = new List<Pick>
        {
            TrophyScenario.Pick(1, m, true), TrophyScenario.Pick(2, m, false),
            TrophyScenario.Pick(3, m, false), TrophyScenario.Pick(4, m, false)
        };

        var result = _sut.Evaluate(TrophyScenario.Members(4), picks, new List<WeeklyAward>());

        result[1].Should().Contain(AchievementCatalog.SoloYoLoVi);
        result[2].Should().NotContain(AchievementCatalog.SoloYoLoVi);
    }

    [Fact]
    public void SoloYoLoVi_ConTresMiembros_NoSeOtorga()
    {
        var m = TrophyScenario.Match(1, 1);
        var picks = new List<Pick>
        {
            TrophyScenario.Pick(1, m, true), TrophyScenario.Pick(2, m, false), TrophyScenario.Pick(3, m, false)
        };

        CodesFor(1, TrophyScenario.Members(3), picks).Should().NotContain(AchievementCatalog.SoloYoLoVi);
    }

    [Fact]
    public void SoloYoLoVi_DosAcertantes_NoSeOtorga()
    {
        var m = TrophyScenario.Match(1, 1);
        var picks = new List<Pick>
        {
            TrophyScenario.Pick(1, m, true), TrophyScenario.Pick(2, m, true),
            TrophyScenario.Pick(3, m, false), TrophyScenario.Pick(4, m, false)
        };

        CodesFor(1, TrophyScenario.Members(4), picks).Should().NotContain(AchievementCatalog.SoloYoLoVi);
    }

    // ---- SEMANA_INVICTA ----
    [Theory]
    [InlineData(5, 5, true)]    // 5 de 5
    [InlineData(4, 4, false)]   // acierta todo pero solo hay 4 partidos
    [InlineData(5, 4, false)]   // 5 partidos con un fallo
    public void SemanaInvicta_TodosLosPartidosAcertadosConMinimoCinco(int matches, int hits, bool expected)
    {
        var ms = TrophyScenario.WeekMatches(1, matches);
        var picks = ms.Select((m, i) => TrophyScenario.Pick(1, m, i < hits)).ToList();

        CodesFor(1, TrophyScenario.Members(1), picks).Contains(AchievementCatalog.SemanaInvicta).Should().Be(expected);
    }

    // ---- Partidos reales de la jornada ----
    private HashSet<string> CodesWithRealMatches(IReadOnlyList<Pick> picks, int realCount)
    {
        var real = new Dictionary<int, HashSet<int>> { [1] = Enumerable.Range(100, realCount).ToHashSet() };
        var result = _sut.Evaluate(TrophyScenario.Members(1), picks, new List<WeeklyAward>(), real);
        return result.TryGetValue(1, out var set) ? set : new HashSet<string>();
    }

    [Theory]
    [InlineData(9, 3, false)]  // 3 de 9 partidos reales
    [InlineData(9, 9, true)]
    public void JornadaCompleta_SeComparaContraLosPartidosRealesDeLaJornada(int real, int picked, bool expected)
    {
        var picks = TrophyScenario.WeekMatches(1, picked, firstMatchId: 100).Select(m => TrophyScenario.Pick(1, m, false)).ToList();

        CodesWithRealMatches(picks, real).Contains(AchievementCatalog.JornadaCompleta).Should().Be(expected);
    }

    [Theory]
    [InlineData(9, 5, 5, false)]  // acierta 5 de 5 pronosticados pero la jornada tiene 9
    [InlineData(9, 9, 9, true)]
    [InlineData(4, 4, 4, false)]  // solo 4 partidos reales
    [InlineData(9, 9, 8, false)]  // un fallo
    public void SemanaInvicta_ExigeTodosLosPartidosRealesAcertadosYMinimoCinco(int real, int picked, int hits, bool expected)
    {
        var picks = TrophyScenario.WeekMatches(1, picked, firstMatchId: 100).Select((m, i) => TrophyScenario.Pick(1, m, i < hits)).ToList();

        CodesWithRealMatches(picks, real).Contains(AchievementCatalog.SemanaInvicta).Should().Be(expected);
    }

    [Fact]
    public void SoloYoLoVi_SiLosDemasNoTienenPickCalificadoEnEsePartido_NoSeOtorga()
    {
        var m = TrophyScenario.Match(1, 1);
        var picks = new List<Pick>
        {
            TrophyScenario.Pick(1, m, true), TrophyScenario.Pick(2, m, null),
            TrophyScenario.Pick(3, m, null), TrophyScenario.Pick(4, m, false)
        };

        CodesFor(1, TrophyScenario.Members(4), picks).Should().NotContain(AchievementCatalog.SoloYoLoVi);
    }

    [Fact]
    public void SoloYoLoVi_SiSoloHayTresMiembrosConPickEnElPartido_NoSeOtorga()
    {
        var m = TrophyScenario.Match(1, 1);
        var picks = new List<Pick>
        {
            TrophyScenario.Pick(1, m, true), TrophyScenario.Pick(2, m, false), TrophyScenario.Pick(3, m, false)
        };

        CodesFor(1, TrophyScenario.Members(4), picks).Should().NotContain(AchievementCatalog.SoloYoLoVi);
    }

    // ---- TRICAMPEON_DE_JORNADA ----
    private static WeeklyAward Mvp(int memberId, int weekId) => new() { MemberId = memberId, WeekId = weekId, AwardType = "MVP" };

    [Fact]
    public void TricampeonDeJornada_TresMvpEnLaTemporada_SeOtorga()
    {
        var picks = Enumerable.Range(1, 3).Select(w => TrophyScenario.Pick(1, TrophyScenario.Match(w, w), false)).ToList();
        var awards = new List<WeeklyAward> { Mvp(1, 1), Mvp(1, 2), Mvp(1, 3) };

        CodesFor(1, TrophyScenario.Members(1), picks, awards).Should().Contain(AchievementCatalog.TricampeonDeJornada);
    }

    [Fact]
    public void TricampeonDeJornada_SoloDosMvp_NoSeOtorga()
    {
        var picks = Enumerable.Range(1, 3).Select(w => TrophyScenario.Pick(1, TrophyScenario.Match(w, w), false)).ToList();
        var awards = new List<WeeklyAward> { Mvp(1, 1), Mvp(1, 2) };

        CodesFor(1, TrophyScenario.Members(1), picks, awards).Should().NotContain(AchievementCatalog.TricampeonDeJornada);
    }

    [Fact]
    public void TricampeonDeJornada_UnMvpEsDeOtraTemporada_NoCuenta()
    {
        // Jornadas 1 y 2 pertenecen a la temporada evaluada; la 99 no tiene picks en ella.
        var picks = Enumerable.Range(1, 2).Select(w => TrophyScenario.Pick(1, TrophyScenario.Match(w, w), false)).ToList();
        var awards = new List<WeeklyAward> { Mvp(1, 1), Mvp(1, 2), Mvp(1, 99) };

        CodesFor(1, TrophyScenario.Members(1), picks, awards).Should().NotContain(AchievementCatalog.TricampeonDeJornada);
    }

    [Fact]
    public void TricampeonDeJornada_IgnoraOtrosTiposDeGalardon()
    {
        var picks = Enumerable.Range(1, 3).Select(w => TrophyScenario.Pick(1, TrophyScenario.Match(w, w), false)).ToList();
        var awards = Enumerable.Range(1, 3).Select(w => new WeeklyAward { MemberId = 1, WeekId = w, AwardType = "REY_SORPRESAS" }).ToList();

        CodesFor(1, TrophyScenario.Members(1), picks, awards).Should().NotContain(AchievementCatalog.TricampeonDeJornada);
    }

    // ---- Catalogo: Gamerscore y niveles ----
    [Theory]
    [InlineData(0, "Novato")]
    [InlineData(49, "Novato")]
    [InlineData(50, "Aprendiz")]
    [InlineData(149, "Aprendiz")]
    [InlineData(150, "Cazador")]
    [InlineData(299, "Cazador")]
    [InlineData(300, "Cazador Experto")]
    [InlineData(600, "Maestro Cazador")]
    [InlineData(999, "Maestro Cazador")]
    [InlineData(1000, "Leyenda")]
    public void LevelFor_RespetaLosUmbrales(int gamerscore, string expected)
    {
        AchievementCatalog.LevelFor(gamerscore).Should().Be(expected);
    }

    [Fact]
    public void NextLevelAt_DevuelveElSiguienteUmbralYNullEnElMaximo()
    {
        AchievementCatalog.NextLevelAt(49).Should().Be(50);
        AchievementCatalog.NextLevelAt(150).Should().Be(300);
        AchievementCatalog.NextLevelAt(1000).Should().BeNull();
    }
}
