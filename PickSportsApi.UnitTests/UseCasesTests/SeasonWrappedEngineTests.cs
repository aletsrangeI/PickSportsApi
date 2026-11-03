using System.Text.Json;
using DTO.Wrapped;
using FluentAssertions;
using UseCases.Wrapped;
using Xunit;

namespace PickSportsApi.UnitTests.UseCasesTests;

public class SeasonWrappedEngineTests
{
    private readonly SeasonWrappedEngine _sut = new();

    // ------------------------------------------------------------ fallback ladders

    [Fact]
    public void Build_CuandoNoTieneSorpresasNiLoboSolitario_S03DesciendeDeNivelYNuncaQuedaVacia()
    {
        // Arrange: dos aciertos compartidos con el grupo, cero sorpresas.
        var scenario = new WrappedScenario();
        scenario.AddMember(1, "Sin Sorpresas");
        scenario.AddMember(2, "Rival A");
        scenario.AddMember(3, "Rival B");

        var week = new List<WrappedMatchInfo>
        {
            scenario.AddMatch(1, "AME", "CHI", 2, 0),
            scenario.AddMatch(1, "GDL", "PUE", 1, 0),
            scenario.AddMatch(1, "MTY", "LEO", 1, 2)
        };

        scenario.PickWinner(1, week[0]);
        scenario.PickWinner(1, week[1]);
        scenario.PickLoser(1, week[2]);
        scenario.PickWinner(2, week[0]);
        scenario.PickLoser(2, week[1]);
        scenario.PickWinner(2, week[2]);
        scenario.PickWinner(3, week[1]);
        scenario.PickWinner(3, week[2]);

        // Act
        var result = _sut.Build(scenario.Build(targetMemberId: 1));

        // Assert: el nivel 0 y 1 no existen, se usa la mejor jornada (nivel 2).
        var slide = result.Slides.Single(s => s.StoryId == "S-03");
        slide.FallbackLevel.Should().Be(2);
        slide.Subtitle.Should().Be("Jornada 1 · 2 de 3");
        slide.HighlightMetric.Should().Be("2 aciertos");
        slide.Headline.Should().NotBeNullOrWhiteSpace();
        slide.BodyText.Should().NotBeNullOrWhiteSpace();
        slide.BackgroundGradient.Should().Contain("linear-gradient");

        result.FinalPosition.Should().Be(1);
        result.TotalMembers.Should().Be(3);
        result.ArchetypeCode.Should().Be("CONSERVADOR_SILLON");
        StoryIdsOf(result).Should().Equal("S-01", "S-02", "S-03", "S-05", "S-08", "S-12", "S-14");
        StatOf(result, "Puntos").Should().Be("2");
        StatOf(result, "Efectividad").Should().Be("67%");
        StatOf(result, "Mejor racha").Should().Be("2");
        StatOf(result, "Mejor jornada").Should().Be("2");
    }

    [Fact]
    public void Build_CuandoNoTieneAciertos_GeneraHistoriasHonestasSinExcepcion()
    {
        // Arrange: seis picks, todos fallados.
        var scenario = new WrappedScenario();
        scenario.AddMember(1, "Cero Aciertos");
        scenario.AddMember(2, "Rival A");
        scenario.AddMember(3, "Rival B");

        for (var week = 1; week <= 2; week++)
        {
            for (var i = 0; i < 3; i++)
            {
                var match = scenario.AddMatch(week, "AME", "CHI", 2, 0);
                scenario.PickLoser(1, match);
                scenario.PickWinner(2, match);
                scenario.PickWinner(3, match);
            }
        }

        // Act
        var result = _sut.Build(scenario.Build(targetMemberId: 1));

        // Assert
        result.Slides.Should().HaveCountGreaterThanOrEqualTo(6);
        result.Slides.Should().OnlyContain(s => !string.IsNullOrWhiteSpace(s.BodyText));
        result.Slides.Should().OnlyContain(s => !string.IsNullOrWhiteSpace(s.Headline));

        var nightmare = result.Slides.Single(s => s.StoryId == "S-04");
        nightmare.FallbackLevel.Should().Be(2);
        nightmare.Subtitle.Should().Be("Jornada 1 · 0 de 3");

        // Con 0 aciertos totales, S-03 cae hasta el nivel 3 con texto honesto.
        var honor = result.Slides.Single(s => s.StoryId == "S-03");
        honor.FallbackLevel.Should().Be(3);
        honor.BodyText.Should().Contain("participar");
        honor.MatchTitle.Should().BeNull();

        var warmup = result.Slides.First();
        warmup.StoryId.Should().Be("S-01");
        warmup.HighlightMetric.Should().Be("0% de efectividad");

        result.FinalPosition.Should().Be(3);
        result.TotalMembers.Should().Be(3);
        result.ArchetypeCode.Should().Be("CONSERVADOR_SILLON");
        StoryIdsOf(result).Should().Equal("S-01", "S-02", "S-03", "S-04", "S-06", "S-08", "S-12");
        StatOf(result, "Puntos").Should().Be("0");
        StatOf(result, "Efectividad").Should().Be("0%");
        StatOf(result, "Mejor racha").Should().Be("0");
        StatOf(result, "Mejor jornada").Should().Be("0");
        result.FinalCard.Stats.Should().HaveCount(4);
    }

    [Fact]
    public void Build_ConPocasJornadas_UnoODosSemanas_GeneraSeisOSieteSlides()
    {
        // Arrange
        var scenario = new WrappedScenario();
        scenario.AddMember(1, "Novato");
        scenario.AddMember(2, "Veterano");

        var match = scenario.AddMatch(1, "AME", "CHI", 1, 0);
        var match2 = scenario.AddMatch(1, "MTY", "LEO", 0, 0);
        var match3 = scenario.AddMatch(1, "UNAM", "CAZ", 2, 1);

        scenario.PickWinner(1, match);
        scenario.PickWinner(1, match2);
        scenario.PickLoser(1, match3);
        scenario.PickWinner(2, match2);

        // Act
        var result = _sut.Build(scenario.Build(targetMemberId: 1));

        // Assert
        result.Slides.Count.Should().Be(7);
        result.Slides[0].StoryId.Should().Be("S-01");
        result.Slides[1].StoryId.Should().Be("S-02");
        StoryIdsOf(result).Should().Equal("S-01", "S-02", "S-03", "S-05", "S-11", "S-12", "S-14");
        result.Slides.Single(s => s.StoryId == "S-11").MentionedUserName.Should().Be("Veterano");
        result.FinalPosition.Should().Be(1);
        result.TotalMembers.Should().Be(2);
        result.ArchetypeCode.Should().Be("CONSERVADOR_SILLON");
        StatOf(result, "Puntos").Should().Be("2");
        StatOf(result, "Efectividad").Should().Be("67%");
        StatOf(result, "Mejor racha").Should().Be("2");
    }

    [Fact]
    public void Build_SinPartidosJugados_GeneraSeisSlidesConFallbacksHonestos()
    {
        // Arrange: temporada sin partidos jugados ni picks.
        var scenario = new WrappedScenario();
        scenario.AddMember(1, "Sin Datos");

        // Act
        var result = _sut.Build(scenario.Build(targetMemberId: 1));

        // Assert
        result.Slides.Count.Should().Be(6);
        StoryIdsOf(result).Should().Equal("S-01", "S-02", "S-03", "S-04", "S-12", "S-15");
        result.Slides.Should().OnlyContain(s => !string.IsNullOrWhiteSpace(s.BodyText));
        result.Slides.Single(s => s.StoryId == "S-03").FallbackLevel.Should().Be(3);
        result.Slides.Single(s => s.StoryId == "S-04").Subtitle.Should().Be("Sin picks registrados");
        result.FinalCard.Stats.Should().HaveCount(4);
        result.ArchetypeCode.Should().Be("CONSERVADOR_SILLON");
        result.FinalPosition.Should().Be(1);
        result.TotalMembers.Should().Be(1);
        StatOf(result, "Puntos").Should().Be("0");
        StatOf(result, "Efectividad").Should().Be("0%");
        StatOf(result, "Mejor racha").Should().Be("0");
        StatOf(result, "Mejor jornada").Should().Be("0");
    }

    [Fact]
    public void Build_ConPerfilRico_EntregaSieteSlidesYArquetipoGuardianDelHonor()
    {
        // Arrange: perfil rico en datos (clásicos, empates, premios, palizas).
        var scenario = BuildRichProfile();

        // Act
        var result = _sut.Build(scenario.Build(targetMemberId: 1));

        // Assert: arquetipo y mazo exactos, sin necesidad de aserciones negativas.
        result.Slides.Count.Should().Be(7);
        StoryIdsOf(result).Should().Equal("S-01", "S-02", "S-03", "S-10", "S-13", "S-14", "S-15");
        result.Slides.Select(s => s.StoryId).Should().OnlyHaveUniqueItems();
        result.ArchetypeCode.Should().Be("GUARDIAN_HONOR");
        result.ArchetypeTitle.Should().Be("El Guardián del Honor");
        result.ThemeColor.Should().Be("#E0407B");
        result.FinalPosition.Should().Be(4);
        result.TotalMembers.Should().Be(4);
        result.Slides.Single(s => s.StoryId == "S-13").Subtitle.Should().Be("4 empates apostados · 25%");
        result.Slides.Single(s => s.StoryId == "S-15").Subtitle.Should().Be("2 galardón(es)");
        StatOf(result, "Puntos").Should().Be("5");
        StatOf(result, "Efectividad").Should().Be("31%");
        StatOf(result, "Mejor racha").Should().Be("1");
        StatOf(result, "Sorpresas").Should().Be("1");
    }

    // ------------------------------------------------------------ arquetipos

    [Fact]
    public void Build_ConCuatroOmasSorpresas_AsignaCazadorDeGigantes()
    {
        // Arrange: 3 miembros, 4 aciertos marcados como sorpresa.
        var scenario = new WrappedScenario();
        scenario.AddMember(1, "Cazador");
        scenario.AddMember(2, "Rival A");
        scenario.AddMember(3, "Rival B");

        var week1 = new List<WrappedMatchInfo>
        {
            scenario.AddMatch(1, "AME", "CHI", 2, 0),
            scenario.AddMatch(1, "GDL", "PUE", 1, 0),
            scenario.AddMatch(1, "MTY", "LEO", 1, 2),
            scenario.AddMatch(1, "UNAM", "CAZ", 0, 1),
            scenario.AddMatch(1, "TIG", "NEC", 0, 2)
        };
        var week2 = new List<WrappedMatchInfo>
        {
            scenario.AddMatch(2, "AME", "PUE", 0, 2),
            scenario.AddMatch(2, "GDL", "LEO", 2, 0),
            scenario.AddMatch(2, "MTY", "CAZ", 1, 0),
            scenario.AddMatch(2, "UNAM", "NEC", 1, 3),
            scenario.AddMatch(2, "TIG", "CHI", 3, 0)
        };

        scenario.PickWinner(1, week1[0], upset: true);
        scenario.PickWinner(1, week1[1], upset: true);
        scenario.PickWinner(1, week1[2], upset: true);
        scenario.PickWinner(1, week1[3], upset: true);
        scenario.PickLoser(1, week1[4]);
        scenario.PickWinner(1, week2[0]);
        scenario.PickLoser(1, week2[1]);
        scenario.PickLoser(1, week2[2]);
        scenario.PickLoser(1, week2[3]);
        scenario.PickLoser(1, week2[4]);

        foreach (var match in week1.Concat(week2))
        {
            scenario.PickWinner(2, match);
            scenario.PickWinner(3, match);
        }

        // Act
        var result = _sut.Build(scenario.Build(targetMemberId: 1));

        // Assert
        result.ArchetypeCode.Should().Be("CAZADOR_GIGANTES");
        result.ArchetypeTitle.Should().Contain("Cazador");
        result.ArchetypeEmoji.Should().NotBeNullOrWhiteSpace();
        result.ThemeColor.Should().Be("#16C79A");
        result.FinalPosition.Should().Be(3);
        StatOf(result, "Puntos").Should().Be("5");
        StatOf(result, "Efectividad").Should().Be("50%");
        StatOf(result, "Mejor racha").Should().Be("4");
        StatOf(result, "Sorpresas").Should().Be("4");
    }

    [Fact]
    public void Build_ConMasDelSesentaYCincoPorCientoDeAciertosLocales_AsignaMarquesDeLaLocalia()
    {
        // Arrange: 5 miembros y 5 jornadas; 12 de 15 aciertos fueron victorias locales.
        var scenario = new WrappedScenario();
        for (var id = 1; id <= 5; id++) scenario.AddMember(id, $"Miembro {id}");

        var weeklyHits = new[] { 5, 2, 4, 1, 3 };

        for (var week = 1; week <= 5; week++)
        {
            var matches = new List<WrappedMatchInfo>();
            for (var i = 0; i < 5; i++)
            {
                // Una victoria visitante por jornada en la posicion 5, 4 y 3 respectivamente.
                var awayWin = (week, i) is (1, 4) or (3, 3) or (5, 2);
                matches.Add(awayWin
                    ? scenario.AddMatch(week, "AME", "CHI", 0, 2)
                    : scenario.AddMatch(week, "GDL", "PUE", 2, 0));
            }

            for (var i = 0; i < matches.Count; i++)
            {
                if (i < weeklyHits[week - 1])
                    scenario.PickWinner(1, matches[i], upset: i == 0 && week == 1);
                else
                    scenario.PickLoser(1, matches[i]);
            }

            foreach (var rival in new[] { 2, 3, 4, 5 })
            {
                for (var i = 0; i < matches.Count; i++)
                {
                    if (i < 3) scenario.PickWinner(rival, matches[i]);
                    else scenario.PickLoser(rival, matches[i]);
                }
            }
        }

        // Act
        var result = _sut.Build(scenario.Build(targetMemberId: 1));

        // Assert
        result.ArchetypeCode.Should().Be("MARQUES_LOCALIA");
        result.ThemeColor.Should().Be("#FFB020");
        result.FinalPosition.Should().Be(1);
        result.TotalMembers.Should().Be(5);
        StoryIdsOf(result).Should().Equal("S-01", "S-02", "S-03", "S-05", "S-06", "S-09", "S-12");
        StatOf(result, "Puntos").Should().Be("15");
        StatOf(result, "Efectividad").Should().Be("60%");
        StatOf(result, "Mejor racha").Should().Be("7");
        StatOf(result, "Sorpresas").Should().Be("1");
    }

    [Fact]
    public void Build_ConMasEmpatesAcertadosQueElP90DelGrupo_AsignaNotarioDelEmpate()
    {
        // Arrange: 6 miembros, 4 empates reales y 4 empates fallidos del objetivo.
        var scenario = new WrappedScenario();
        for (var id = 1; id <= 6; id++) scenario.AddMember(id, $"Miembro {id}");

        var matches = new List<WrappedMatchInfo>();
        var drawMatches = new List<WrappedMatchInfo>();

        for (var week = 1; week <= 4; week++)
        {
            for (var i = 0; i < 5; i++)
            {
                var isDraw = i == 0;
                var match = isDraw
                    ? scenario.AddMatch(week, "AME", "CHI", 1, 1)
                    : scenario.AddMatch(week, "GDL", "PUE", 2, 0);
                matches.Add(match);
                if (isDraw) drawMatches.Add(match);
            }
        }

        // Objetivo: 8 empates apostados (4 acertados + 4 fallidos) y 2 aciertos a equipos.
        foreach (var match in drawMatches) scenario.Pick(1, match, "EMPATE");
        var failedDraws = matches.Where(m => !drawMatches.Contains(m)).Take(4).ToList();
        foreach (var match in failedDraws) scenario.Pick(1, match, "EMPATE");

        var regularMatches = matches.Where(m => !drawMatches.Contains(m) && !failedDraws.Contains(m)).ToList();
        scenario.PickWinner(1, regularMatches[0], upset: true);
        scenario.PickWinner(1, regularMatches[1]);
        foreach (var match in regularMatches.Skip(2)) scenario.PickLoser(1, match);

        // Resto del grupo: 3 aciertos fijos por jornada, sin empates.
        foreach (var rival in new[] { 2, 3, 4, 5, 6 })
        {
            for (var week = 1; week <= 4; week++)
            {
                var weekMatches = matches.Where(m => m.WeekNumber == week).ToList();
                for (var i = 0; i < weekMatches.Count; i++)
                {
                    if (i >= 1 && i <= 3) scenario.PickWinner(rival, weekMatches[i]);
                    else scenario.PickLoser(rival, weekMatches[i]);
                }
            }
        }

        // Act
        var result = _sut.Build(scenario.Build(targetMemberId: 1));

        // Assert
        result.ArchetypeCode.Should().Be("NOTARIO_EMPATE");
        result.ThemeColor.Should().Be("#3AA0FF");
        var notary = result.Slides.Single(s => s.StoryId == "S-13");
        notary.FallbackLevel.Should().Be(0);
        notary.Subtitle.Should().Be("8 empates apostados · 40%");
        result.FinalPosition.Should().Be(6);
        result.TotalMembers.Should().Be(6);
        StatOf(result, "Puntos").Should().Be("6");
        StatOf(result, "Efectividad").Should().Be("30%");
        StatOf(result, "Mejor racha").Should().Be("3");
        StatOf(result, "Sorpresas").Should().Be("1");
    }

    [Fact]
    public void Build_CuandoNingunArquetipoCalifica_UsaElFallbackGarantizado()
    {
        // Arrange: una sola jornada y un solo acierto (no alcanza ningun umbral).
        var scenario = new WrappedScenario();
        scenario.AddMember(1, "Promedio");
        scenario.AddMember(2, "Rival A");
        scenario.AddMember(3, "Rival B");

        var match1 = scenario.AddMatch(1, "AME", "CHI", 1, 0);
        var match2 = scenario.AddMatch(1, "GDL", "PUE", 1, 0);
        var match3 = scenario.AddMatch(1, "MTY", "LEO", 1, 0);

        scenario.PickWinner(1, match1);
        scenario.PickLoser(1, match2);
        scenario.PickLoser(1, match3);
        scenario.PickWinner(2, match1);
        scenario.PickWinner(2, match2);
        scenario.PickWinner(3, match1);

        // Act
        var result = _sut.Build(scenario.Build(targetMemberId: 1));

        // Assert: menos de 3 jornadas => CONSERVADOR_SILLON como red de seguridad.
        result.ArchetypeCode.Should().Be("CONSERVADOR_SILLON");
        result.ArchetypeTitle.Should().Be("El Conservador de Sillón");
        result.ArchetypeDescription.Should().NotBeNullOrWhiteSpace();
        result.ThemeColor.Should().Be("#7FB069");
        result.FinalPosition.Should().Be(2);
        result.TotalMembers.Should().Be(3);
        StatOf(result, "Puntos").Should().Be("1");
        StatOf(result, "Efectividad").Should().Be("33%");
        StatOf(result, "Mejor racha").Should().Be("1");
    }

    // ------------------------------------------------------------ social

    [Fact]
    public void Build_ConAlmaGemelaDefinida_NemesisNoEsLaMismaPersona()
    {
        // Arrange: el objetivo coincide 11/12 con Ana y 1/12 con Beto.
        var scenario = new WrappedScenario();
        scenario.AddMember(1, "Objetivo");
        scenario.AddMember(2, "Ana");
        scenario.AddMember(3, "Beto");

        var matches = new List<WrappedMatchInfo>();
        for (var week = 1; week <= 3; week++)
        {
            for (var i = 0; i < 4; i++)
            {
                matches.Add(scenario.AddMatch(week, "AME", "CHI", 2, 0));
            }
        }

        foreach (var match in matches)
        {
            scenario.PickWinner(1, match);
        }

        // Ana acierta 11 de 12; Beto acierta solo 1 de 12.
        for (var i = 0; i < matches.Count; i++)
        {
            if (i < 11) scenario.PickWinner(2, matches[i]);
            else scenario.PickLoser(2, matches[i]);

            if (i == 0) scenario.PickWinner(3, matches[i]);
            else scenario.PickLoser(3, matches[i]);
        }

        // Act
        var result = _sut.Build(scenario.Build(targetMemberId: 1));

        // Assert
        result.Slides.Select(s => $"{s.StoryId}|{s.FallbackLevel}|{s.MentionedUserName}").Should().Contain("S-10|0|Beto");
        var soulmate = result.Slides.Single(s => s.StoryId == "S-09");
        var nemesis = result.Slides.Single(s => s.StoryId == "S-10");

        soulmate.FallbackLevel.Should().Be(0);
        nemesis.FallbackLevel.Should().Be(0);
        soulmate.MentionedUserName.Should().Be("Ana");
        soulmate.HighlightMetric.Should().Be("92% de coincidencia");
        nemesis.MentionedUserName.Should().Be("Beto");
        nemesis.HighlightMetric.Should().Be("92% de discrepancia");
        nemesis.MentionedUserName.Should().NotBe(soulmate.MentionedUserName);

        result.FinalPosition.Should().Be(1);
        StoryIdsOf(result).Should().Equal("S-01", "S-02", "S-03", "S-05", "S-09", "S-10", "S-12");
        StatOf(result, "Puntos").Should().Be("12");
        StatOf(result, "Efectividad").Should().Be("100%");
        StatOf(result, "Mejor racha").Should().Be("12");
        StatOf(result, "Mejor jornada").Should().Be("4");
    }

    [Fact]
    public void Build_ConUnSoloMiembro_OmiteS09YS10SinLanzarExcepcion()
    {
        // Arrange
        var scenario = new WrappedScenario();
        scenario.AddMember(1, "Solitario");

        for (var i = 0; i < 5; i++)
        {
            var match = scenario.AddMatch(1, "AME", "CHI", 2, 0);
            scenario.PickWinner(1, match);
        }

        // Act
        var result = _sut.Build(scenario.Build(targetMemberId: 1));

        // Assert
        result.TotalMembers.Should().Be(1);
        result.FinalPosition.Should().Be(1);
        StoryIdsOf(result).Should().Equal("S-01", "S-02", "S-03", "S-05", "S-08", "S-12", "S-14");
        result.ArchetypeCode.Should().Be("MARQUES_LOCALIA");
        StatOf(result, "Puntos").Should().Be("5");
        StatOf(result, "Efectividad").Should().Be("100%");
        StatOf(result, "Mejor racha").Should().Be("5");
        StatOf(result, "Mejor jornada").Should().Be("5");
    }

    [Fact]
    public void Build_DosPerfilesDistintos_RecibenArquetiposYMazosDistintos()
    {
        // Arrange: "Juan" (dos jornadas autollenadas) y "Pedro" (notario del empate) en la misma quiniela.
        var scenario = new WrappedScenario();
        for (var id = 1; id <= 8; id++) scenario.AddMember(id, $"Miembro {id}");

        var matches = new List<WrappedMatchInfo>();
        var drawMatches = new List<WrappedMatchInfo>();

        for (var week = 1; week <= 4; week++)
        {
            for (var i = 0; i < 5; i++)
            {
                var isDraw = week % 2 == 1 && i == 0;
                var match = isDraw
                    ? scenario.AddMatch(week, "AME", "CHI", 1, 1)
                    : scenario.AddMatch(week, "GDL", "PUE", 2, 0);
                matches.Add(match);
                if (isDraw) drawMatches.Add(match);
            }
        }

        // Juan (1): solo juega las jornadas 1 y 2, 100% autollenado y con aciertos.
        foreach (var match in matches.Where(m => m.WeekNumber <= 2))
        {
            if (match.WeekNumber == 1 && match == matches[0]) scenario.PickWinner(1, match, autoFilled: true);
            else if (match.WeekNumber == 2 && match == matches[5]) scenario.PickWinner(1, match, autoFilled: true);
            else scenario.PickLoser(1, match, autoFilled: true);
        }

        // Pedro (2): apostador de empates con perfil irregular.
        foreach (var match in drawMatches) scenario.Pick(2, match, "EMPATE", hoursBeforeLock: 40);
        var failedDraws = matches.Where(m => !drawMatches.Contains(m)).Take(4).ToList();
        foreach (var match in failedDraws) scenario.Pick(2, match, "EMPATE", hoursBeforeLock: 40);
        var rest = matches.Where(m => !drawMatches.Contains(m) && !failedDraws.Contains(m)).ToList();
        scenario.PickWinner(2, rest[0], upset: true, hoursBeforeLock: 40);
        scenario.PickWinner(2, rest[1], hoursBeforeLock: 40);
        foreach (var match in rest.Skip(2)) scenario.PickLoser(2, match, hoursBeforeLock: 40);

        // Resto del grupo: constante y sin empates.
        foreach (var rival in new[] { 3, 4, 5, 6, 7, 8 })
        {
            foreach (var week in new[] { 1, 2, 3, 4 })
            {
                var weekMatches = matches.Where(m => m.WeekNumber == week).ToList();
                for (var i = 0; i < weekMatches.Count; i++)
                {
                    if (i is >= 1 and <= 3) scenario.PickWinner(rival, weekMatches[i]);
                    else scenario.PickLoser(rival, weekMatches[i]);
                }
            }
        }

        // Act
        var juan = _sut.Build(scenario.Build(targetMemberId: 1));
        var pedro = _sut.Build(scenario.Build(targetMemberId: 2));

        // Assert
        juan.ArchetypeCode.Should().Be("AMIGO_BOT");
        pedro.ArchetypeCode.Should().Be("NOTARIO_EMPATE");
        juan.ThemeColor.Should().Be("#00C2FF");
        pedro.ThemeColor.Should().Be("#3AA0FF");
        juan.FinalPosition.Should().Be(8);
        pedro.FinalPosition.Should().Be(7);
        StoryIdsOf(juan).Should().Equal("S-01", "S-02", "S-05", "S-06", "S-08", "S-09", "S-10");
        StoryIdsOf(pedro).Should().Equal("S-01", "S-02", "S-03", "S-06", "S-10", "S-11", "S-13");
        StoryIdsOf(juan).Should().NotBeEquivalentTo(StoryIdsOf(pedro));
        StatOf(juan, "Puntos").Should().Be("2");
        StatOf(juan, "Efectividad").Should().Be("20%");
        StatOf(juan, "Mejor racha").Should().Be("1");
        StatOf(pedro, "Puntos").Should().Be("4");
        StatOf(pedro, "Efectividad").Should().Be("20%");
        StatOf(pedro, "Mejor racha").Should().Be("2");
        StatOf(pedro, "Sorpresas").Should().Be("1");
    }

    // ------------------------------------------------------------ contrato y determinismo

    [Fact]
    public void Build_GeneraFinalCardConExactamenteCuatroEstadisticas()
    {
        // Arrange
        var scenario = BuildRichProfile();

        // Act
        var result = _sut.Build(scenario.Build(targetMemberId: 1));

        // Assert
        result.FinalCard.Should().NotBeNull();
        result.FinalCard.Stats.Should().HaveCount(4);
        result.FinalCard.Stats.Select(s => s.Label)
            .Should().Equal("Puntos", "Efectividad", "Mejor racha", "Sorpresas");
        result.FinalCard.Stats.Select(s => s.Value).Should().Equal("5", "31%", "1", "1");
        result.FinalCard.Stats.Should().OnlyContain(s => !string.IsNullOrWhiteSpace(s.Value));
        result.FinalCard.ArchetypeTitle.Should().Be(result.ArchetypeTitle);
        result.FinalCard.ThemeColor.Should().Be(result.ThemeColor);
        result.FinalCard.SeasonName.Should().Be("Apertura 2025");
        result.FinalCard.DisplayName.Should().Be("Miembro 1");
        result.FinalCard.FinalPosition.Should().Be(4);
    }

    [Fact]
    public void Build_ConLaMismaEntrada_ProduceExactamenteElMismoJson()
    {
        // Arrange
        var scenario = BuildRichProfile();
        var input = scenario.Build(targetMemberId: 1);

        // Act
        var first = _sut.Build(input);
        var second = _sut.Build(input);
        var third = _sut.Build(scenario.Build(targetMemberId: 1));

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var firstJson = JsonSerializer.Serialize(first, options);

        // Assert
        first.ArchetypeCode.Should().Be("GUARDIAN_HONOR");
        firstJson.Should().Contain("\"archetypeCode\":\"GUARDIAN_HONOR\"");
        firstJson.Should().Contain("\"storyId\":\"S-14\"");
        JsonSerializer.Serialize(second, options).Should().Be(firstJson);
        JsonSerializer.Serialize(third, options).Should().Be(firstJson);
    }

    [Fact]
    public void Build_SiempreIncluyeS01YS02ComoPrimerasSlides()
    {
        // Arrange
        var scenario = new WrappedScenario();
        scenario.AddMember(1, "Jugador");
        scenario.AddMember(2, "Rival");
        var match = scenario.AddMatch(1, "AME", "CHI", 1, 0);
        scenario.PickWinner(1, match);
        scenario.PickWinner(2, match);

        // Act
        var result = _sut.Build(scenario.Build(targetMemberId: 1));

        // Assert
        result.Slides[0].StoryId.Should().Be("S-01");
        result.Slides[0].SlideType.Should().Be("WARMUP");
        result.Slides[0].Subtitle.Should().Be("#1 de 2 · 1 puntos");
        result.Slides[1].StoryId.Should().Be("S-02");
        result.Slides[1].SlideType.Should().Be("ARCHETYPE");
        result.Slides.Should().OnlyContain(s => !string.IsNullOrWhiteSpace(s.BackgroundGradient));
        StoryIdsOf(result).Should().Equal("S-01", "S-02", "S-03", "S-04", "S-05", "S-08", "S-12");
        StatOf(result, "Puntos").Should().Be("1");
        StatOf(result, "Efectividad").Should().Be("100%");
    }

    [Fact]
    public void Build_CuandoElObjetivoEsElLider_ReportaPosicionUnoDeN()
    {
        // Arrange
        var scenario = new WrappedScenario();
        scenario.AddMember(1, "Lider");
        scenario.AddMember(2, "Segundo");
        scenario.AddMember(3, "Tercero");

        foreach (var rival in new[] { 1, 2, 3 })
        {
            for (var i = 0; i < 3; i++)
            {
                var match = scenario.AddMatch(1, "AME", "CHI", 2, 0);
                if (rival == 1) scenario.PickWinner(rival, match);
                else if (rival == 2 && i < 2) scenario.PickWinner(rival, match);
                else scenario.PickLoser(rival, match);
            }
        }

        // Act
        var result = _sut.Build(scenario.Build(targetMemberId: 1));

        // Assert
        result.FinalPosition.Should().Be(1);
        result.TotalMembers.Should().Be(3);
        result.Slides.First().Subtitle.Should().Be("#1 de 3 · 3 puntos");
        result.ArchetypeCode.Should().Be("MARQUES_LOCALIA");
        StatOf(result, "Puntos").Should().Be("3");
        StatOf(result, "Efectividad").Should().Be("100%");
        StatOf(result, "Mejor racha").Should().Be("3");
        StatOf(result, "Mejor jornada").Should().Be("3");
    }

    // ------------------------------------------------------------ robustez y mutacion conceptual

    [Fact]
    public void Build_ConUnPickHuerfano_IgnoraElPickYDevuelveElMismoResultado()
    {
        // Arrange: entrada valida con un unico acierto de tres picks.
        var scenario = new WrappedScenario();
        scenario.AddMember(1, "Objetivo");
        scenario.AddMember(2, "Rival");

        var won = scenario.AddMatch(1, "AME", "CHI", 2, 0);
        var lost1 = scenario.AddMatch(2, "GDL", "PUE", 0, 1);
        var lost2 = scenario.AddMatch(3, "MTY", "LEO", 1, 1);

        scenario.PickWinner(1, won);
        scenario.PickLoser(1, lost1);
        scenario.PickLoser(1, lost2);
        scenario.PickWinner(2, won);
        scenario.PickWinner(2, lost1);
        scenario.PickWinner(2, lost2);

        var clean = scenario.Build(targetMemberId: 1);
        var submitted = clean.Picks[0].SubmittedUtc;

        // Pick huerfano: su MatchId no existe en input.Matches y ademas se marca como acierto y sorpresa.
        var orphan = new WrappedPickInfo(1, 9_999, "AME", false, true, true, true, true, true, submitted);
        var withOrphan = clean with { Picks = clean.Picks.Append(orphan).ToList() };

        // Act
        var expected = _sut.Build(clean);
        var actual = _sut.Build(withOrphan);

        // Assert: no lanza y el pick huerfano no contamina agregados, mazos ni la card final.
        Serialize(actual).Should().Be(Serialize(expected));
        actual.FinalPosition.Should().Be(2);
        actual.TotalMembers.Should().Be(2);
        actual.Slides.Should().HaveCount(7);
        StatOf(actual, "Puntos").Should().Be("1");
        StatOf(actual, "Efectividad").Should().Be("33%");
        StatOf(actual, "Mejor racha").Should().Be("1");
        StatOf(actual, "Mejor jornada").Should().Be("1");
        actual.FinalCard.Stats.Should().HaveCount(4);
    }

    [Fact]
    public void Build_DosEntradasQueDifierenEnUnSoloPick_ProducenStatsDistintas()
    {
        // Arrange: cuatro picks, un solo acierto.
        var scenario = new WrappedScenario();
        scenario.AddMember(1, "Objetivo");
        scenario.AddMember(2, "Rival");

        var week1 = scenario.AddMatch(1, "AME", "CHI", 1, 0);
        var week2 = scenario.AddMatch(2, "GDL", "PUE", 2, 0);
        var week3 = scenario.AddMatch(3, "MTY", "LEO", 1, 1);
        var week4 = scenario.AddMatch(4, "UNAM", "CAZ", 0, 2);

        scenario.PickWinner(1, week1);
        scenario.PickLoser(1, week2);
        scenario.PickLoser(1, week3);
        scenario.PickLoser(1, week4);
        scenario.PickWinner(2, week1);
        scenario.PickWinner(2, week2);
        scenario.PickWinner(2, week3);
        scenario.PickWinner(2, week4);

        var before = scenario.Build(targetMemberId: 1);

        // La segunda entrada cambia UN solo pick: el de la jornada 2 pasa de fallo a acierto.
        var after = before with
        {
            Picks = before.Picks
                .Select(p => p.MatchId == week2.MatchId ? p with { IsHit = true } : p)
                .ToList()
        };

        // Act
        var baseline = _sut.Build(before);
        var mutated = _sut.Build(after);

        // Assert: las entradas difieren exactamente en un pick...
        after.Picks.Should().HaveCount(before.Picks.Count);
        after.Picks.Zip(before.Picks).Count(pair => pair.First != pair.Second).Should().Be(1);

        // ...y aun asi la card final cambia (el motor no devuelve un resultado constante).
        StatOf(baseline, "Efectividad").Should().Be("25%");
        StatOf(mutated, "Efectividad").Should().Be("50%");
        StatOf(baseline, "Mejor racha").Should().Be("1");
        StatOf(mutated, "Mejor racha").Should().Be("2");
        baseline.FinalCard.Stats.Select(s => $"{s.Label}={s.Value}")
            .Should().NotEqual(mutated.FinalCard.Stats.Select(s => $"{s.Label}={s.Value}"));
    }

    // ------------------------------------------------------------ helpers

    private static string StatOf(SeasonWrappedDto dto, string label) =>
        dto.FinalCard.Stats.Single(s => s.Label == label).Value;

    private static IReadOnlyList<string> StoryIdsOf(SeasonWrappedDto dto) =>
        dto.Slides.Select(s => s.StoryId).ToList();

    private static string Serialize(SeasonWrappedDto dto) =>
        JsonSerializer.Serialize(dto, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    // ------------------------------------------------------------ fixture

    private static WrappedScenario BuildRichProfile()
    {
        var scenario = new WrappedScenario();
        for (var id = 1; id <= 4; id++) scenario.AddMember(id, $"Miembro {id}");

        var matches = new List<WrappedMatchInfo>();
        for (var week = 1; week <= 4; week++)
        {
            matches.Add(scenario.AddMatch(week, "AME", "GDL", 2, 1, "El Clásico Nacional"));
            matches.Add(scenario.AddMatch(week, "MTY", "UANL", 1, 1, "El Clásico Regio"));
            matches.Add(scenario.AddMatch(week, "UNAM", "CAZ", 0, 2));
            matches.Add(scenario.AddMatch(week, "PUE", "CHI", 0, 0));
        }

        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            if (i % 5 == 0) scenario.PickWinner(1, match, upset: i == 0);
            else if (i % 7 == 0) scenario.Pick(1, match, "EMPATE");
            else if (i % 4 == 0) scenario.PickLoser(1, match, humillacion: true, hoursBeforeLock: 0.5);
            else if (i % 3 == 0) scenario.PickLoser(1, match, somnifero: true);
            else scenario.PickLoser(1, match);

            foreach (var rival in new[] { 2, 3, 4 })
            {
                if (i % 2 == 0) scenario.PickWinner(rival, match);
                else scenario.PickLoser(rival, match);
            }
        }

        scenario.AddAward(1, 1, "MVP");
        scenario.AddAward(1, 2, "REY_SORPRESAS");

        return scenario;
    }

    private sealed class WrappedScenario
    {
        private static readonly DateTime Origin = new(2025, 8, 1, 18, 0, 0, DateTimeKind.Utc);

        private readonly List<(int Id, string Name)> _memberDefinitions = new();
        private readonly List<WrappedMatchInfo> _matches = new();
        private readonly List<WrappedPickInfo> _picks = new();
        private readonly List<WrappedAwardInfo> _awards = new();
        private readonly Dictionary<int, int> _explicitPoints = new();
        private int _nextMatchId = 1;

        public void AddMember(int id, string name) => _memberDefinitions.Add((id, name));

        public WrappedMatchInfo AddMatch(
            int week,
            string home,
            string away,
            int homeScore,
            int awayScore,
            string? derby = null,
            int dayOffset = 0)
        {
            var date = Origin.AddDays(week * 7 + dayOffset);
            var winner = homeScore > awayScore ? home : awayScore > homeScore ? away : "EMPATE";

            var match = new WrappedMatchInfo(
                _nextMatchId++,
                week,
                week,
                date,
                date.AddHours(-6),
                home,
                away,
                $"{home} FC",
                $"{away} FC",
                homeScore,
                awayScore,
                winner,
                false,
                derby);

            _matches.Add(match);
            return match;
        }

        public void Pick(
            int memberId,
            WrappedMatchInfo match,
            string pickAbbr,
            bool autoFilled = false,
            bool upset = false,
            bool humillacion = false,
            bool somnifero = false,
            bool empateFallido = false,
            double hoursBeforeLock = 30)
        {
            var isHit = string.Equals(pickAbbr, match.WinnerAbbr, StringComparison.OrdinalIgnoreCase);

            _picks.Add(new WrappedPickInfo(
                memberId,
                match.MatchId,
                pickAbbr,
                autoFilled,
                isHit,
                upset && isHit,
                humillacion && !isHit,
                somnifero && !isHit,
                empateFallido && !isHit,
                match.WeekFirstGameUtc!.Value.AddHours(-hoursBeforeLock)));
        }

        public void PickWinner(
            int memberId,
            WrappedMatchInfo match,
            bool autoFilled = false,
            bool upset = false,
            double hoursBeforeLock = 30) =>
            Pick(memberId, match, match.WinnerAbbr!, autoFilled, upset, hoursBeforeLock: hoursBeforeLock);

        public void PickLoser(
            int memberId,
            WrappedMatchInfo match,
            bool autoFilled = false,
            bool humillacion = false,
            bool somnifero = false,
            bool empateFallido = false,
            double hoursBeforeLock = 30)
        {
            var loser = string.Equals(match.WinnerAbbr, match.HomeAbbr, StringComparison.OrdinalIgnoreCase)
                ? match.AwayAbbr
                : match.HomeAbbr;

            Pick(memberId, match, loser, autoFilled, humillacion: humillacion, somnifero: somnifero,
                empateFallido: empateFallido, hoursBeforeLock: hoursBeforeLock);
        }

        public void AddAward(int memberId, int week, string awardType = "MVP") =>
            _awards.Add(new WrappedAwardInfo(memberId, week, awardType));

        public WrappedEngineInput Build(int targetMemberId, int seasonId = 1, string seasonName = "Apertura 2025")
        {
            var members = _memberDefinitions.Select(member =>
            {
                var memberPicks = _picks.Where(p => p.MemberId == member.Id).ToList();
                var hits = memberPicks.Count(p => p.IsHit == true);
                var upsets = memberPicks.Count(p => p.IsUpsetHit);
                var humiliations = memberPicks.Count(p => p.IsHumillacion);

                return new WrappedMemberInfo(
                    member.Id,
                    member.Id,
                    member.Name,
                    null,
                    _explicitPoints.GetValueOrDefault(member.Id, hits),
                    hits,
                    upsets,
                    humiliations,
                    0);
            }).ToList();

            return new WrappedEngineInput(seasonId, seasonName, targetMemberId, members, _matches, _picks, _awards);
        }
    }
}
