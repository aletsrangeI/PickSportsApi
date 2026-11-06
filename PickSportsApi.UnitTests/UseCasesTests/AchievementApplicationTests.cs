using Domain.Entities;
using Domain.Gamification;
using FluentAssertions;
using Interface.Persistence;
using Moq;
using UseCases.Gamification;
using UseCases.Scoring;

namespace PickSportsApi.UnitTests.UseCasesTests;

public class AchievementApplicationTests
{
    private const int QuinielaId = 10;

    /// <summary>Contexto con un "almacen" en memoria de trofeos para que ExistsAsync/InsertAsync sean coherentes.</summary>
    private sealed class Ctx
    {
        public Mock<IUnitOfWork> Uow { get; } = new() { DefaultValue = DefaultValue.Mock };
        public List<UserAchievement> Store { get; } = new();
        public List<Week> Weeks { get; } = new();
        public List<Pick> Picks { get; } = new();
        public List<QuinielaMember> Members { get; } = new();
        public List<WeeklyAward> Awards { get; } = new();
        /// <summary>Partidos reales por jornada; si no se define, se toman los partidos de los picks.</summary>
        public Dictionary<int, List<Domain.Entities.Match>> RealMatches { get; } = new();

        public Ctx()
        {
            Uow.Setup(u => u.Matches.GetByWeekIdAsync(It.IsAny<int>())).ReturnsAsync((int w) =>
                RealMatches.TryGetValue(w, out var real)
                    ? real.ToList()
                    : Picks.Select(p => p.Match).Where(m => m.WeekId == w).GroupBy(m => m.Id).Select(g => g.First()).ToList());
            Uow.Setup(u => u.Achievements.ExistsAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync((int user, string code, int season) => Store.Any(a => a.UserId == user && a.Code == code && a.SeasonId == season));
            Uow.Setup(u => u.Achievements.InsertAsync(It.IsAny<UserAchievement>()))
                .Callback<UserAchievement>(a => Store.Add(a))
                .ReturnsAsync(true);
            Uow.Setup(u => u.Weeks.GetAsync(It.IsAny<int>())).ReturnsAsync((int id) => Weeks.First(w => w.Id == id));
            Uow.Setup(u => u.Picks.GetAllPicksForQuinielaAsync(QuinielaId)).ReturnsAsync(() => Picks.ToList());
            Uow.Setup(u => u.QuinielaMembers.GetMembersAsync(QuinielaId)).ReturnsAsync(() => Members.ToList());
            Uow.Setup(u => u.WeeklyAwards.GetAllAwardsForQuinielaAsync(QuinielaId)).ReturnsAsync(() => Awards.ToList());
        }

        public AchievementApplication Build() => new(Uow.Object, new ScoringEngine());
        public IEnumerable<string> CodesOf(int userId, int? seasonId = null) =>
            Store.Where(a => a.UserId == userId && (seasonId == null || a.SeasonId == seasonId)).Select(a => a.Code);
    }

    private static Ctx SingleMemberWithOneHit(int seasonId = 1, int weekId = 1)
    {
        var ctx = new Ctx();
        var match = TrophyScenario.Match(1, weekId, seasonId);
        ctx.Weeks.Add(match.Week);
        ctx.Members.Add(new QuinielaMember { Id = 1, UserId = 101, Alias = "M1" });
        ctx.Picks.Add(TrophyScenario.Pick(1, match, true));
        return ctx;
    }

    // ---- EvaluateWeekAsync ----
    [Fact]
    public async Task EvaluateWeek_OtorgaAlUserIdDelMiembroConLosDatosDelTrofeo()
    {
        var ctx = SingleMemberWithOneHit(seasonId: 7, weekId: 3);

        await ctx.Build().EvaluateWeekAsync(QuinielaId, 3);

        var granted = ctx.Store.Should().ContainSingle(a => a.Code == AchievementCatalog.PrimeraSangre).Subject;
        granted.UserId.Should().Be(101);
        granted.SeasonId.Should().Be(7);
        granted.QuinielaId.Should().Be(QuinielaId);
        granted.IsSeen.Should().BeFalse();
        ctx.Uow.Verify(u => u.Save(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EvaluateWeek_ReevaluarLaMismaJornada_NoDuplica()
    {
        var ctx = SingleMemberWithOneHit();
        var sut = ctx.Build();

        await sut.EvaluateWeekAsync(QuinielaId, 1);
        int afterFirst = ctx.Store.Count;
        await sut.EvaluateWeekAsync(QuinielaId, 1);

        afterFirst.Should().BeGreaterThan(0);
        ctx.Store.Should().HaveCount(afterFirst);
        ctx.Store.GroupBy(a => (a.UserId, a.Code, a.SeasonId)).Should().OnlyContain(g => g.Count() == 1);
    }

    [Fact]
    public async Task EvaluateWeek_ElMismoTrofeoSeOtorgaDeNuevoEnOtraTemporada()
    {
        var ctx = SingleMemberWithOneHit(seasonId: 1, weekId: 1);
        var sut = ctx.Build();
        await sut.EvaluateWeekAsync(QuinielaId, 1);

        var match2 = TrophyScenario.Match(2, 2, seasonId: 2);
        ctx.Weeks.Add(match2.Week);
        ctx.Picks.Add(TrophyScenario.Pick(1, match2, true));
        await sut.EvaluateWeekAsync(QuinielaId, 2);

        ctx.Store.Where(a => a.Code == AchievementCatalog.PrimeraSangre)
            .Select(a => a.SeasonId).Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Fact]
    public async Task EvaluateWeek_PicksDeOtraTemporada_NoCuentan()
    {
        // Arrange: el unico acierto es de la temporada 1, pero se califica una jornada de la temporada 2
        var ctx = new Ctx();
        var oldMatch = TrophyScenario.Match(1, 1, seasonId: 1);
        var currentMatch = TrophyScenario.Match(2, 2, seasonId: 2);
        ctx.Weeks.AddRange(new[] { oldMatch.Week, currentMatch.Week });
        ctx.Members.Add(new QuinielaMember { Id = 1, UserId = 101, Alias = "M1" });
        ctx.Picks.Add(TrophyScenario.Pick(1, oldMatch, true));
        ctx.Picks.Add(TrophyScenario.Pick(1, currentMatch, false));

        // Act
        await ctx.Build().EvaluateWeekAsync(QuinielaId, 2);

        // Assert
        ctx.Store.Should().NotContain(a => a.Code == AchievementCatalog.PrimeraSangre);
    }

    [Fact]
    public async Task EvaluateWeek_MiembrosSinPicksCalificados_NoReciben()
    {
        var ctx = new Ctx();
        var match = TrophyScenario.Match(1, 1);
        ctx.Weeks.Add(match.Week);
        ctx.Members.AddRange(TrophyScenario.Members(2));
        ctx.Picks.Add(TrophyScenario.Pick(1, match, true));

        await ctx.Build().EvaluateWeekAsync(QuinielaId, 1);

        ctx.CodesOf(102).Should().BeEmpty();
        ctx.CodesOf(101).Should().Contain(AchievementCatalog.PrimeraSangre);
    }

    // ---- Partidos reales de la jornada (JORNADA_COMPLETA / SEMANA_INVICTA) ----
    private static Ctx MemberWithPicksOnRealWeek(int realMatches, int pickedMatches, int postponed = 0)
    {
        var ctx = new Ctx();
        var real = TrophyScenario.WeekMatches(1, realMatches);
        ctx.Weeks.Add(real[0].Week);
        ctx.Members.Add(new QuinielaMember { Id = 1, UserId = 101, Alias = "M1" });
        foreach (var m in real.Take(pickedMatches)) ctx.Picks.Add(TrophyScenario.Pick(1, m, true));
        var all = real.ToList();
        for (int i = 0; i < postponed; i++)
        {
            var p = TrophyScenario.Match(900 + i, 1);
            p.StatusState = "postponed";
            all.Add(p);
        }
        ctx.RealMatches[1] = all;
        return ctx;
    }

    [Fact]
    public async Task EvaluateWeek_PronosticarSoloAlgunosPartidosReales_NoDaJornadaCompletaNiSemanaInvicta()
    {
        var ctx = MemberWithPicksOnRealWeek(realMatches: 9, pickedMatches: 5); // 5 aciertos de 9

        await ctx.Build().EvaluateWeekAsync(QuinielaId, 1);

        ctx.CodesOf(101).Should().NotContain(new[] { AchievementCatalog.JornadaCompleta, AchievementCatalog.SemanaInvicta });
        ctx.CodesOf(101).Should().Contain(AchievementCatalog.PrimeraSangre);
    }

    [Fact]
    public async Task EvaluateWeek_PronosticarTodosLosPartidosReales_DaJornadaCompletaYSemanaInvicta()
    {
        var ctx = MemberWithPicksOnRealWeek(realMatches: 9, pickedMatches: 9);

        await ctx.Build().EvaluateWeekAsync(QuinielaId, 1);

        ctx.CodesOf(101).Should().Contain(new[] { AchievementCatalog.JornadaCompleta, AchievementCatalog.SemanaInvicta });
    }

    [Fact]
    public async Task EvaluateWeek_LosPartidosPospuestosNoCuentanComoPartidosReales()
    {
        var ctx = MemberWithPicksOnRealWeek(realMatches: 5, pickedMatches: 5, postponed: 2);

        await ctx.Build().EvaluateWeekAsync(QuinielaId, 1);

        ctx.CodesOf(101).Should().Contain(new[] { AchievementCatalog.JornadaCompleta, AchievementCatalog.SemanaInvicta });
    }

    // ---- Concurrencia ----
    [Fact]
    public async Task EvaluateWeek_SiUnInsertPierdeLaCarrera_SigueConLosDemasTrofeos()
    {
        // Arrange: otra calificacion inserta PRIMERA_SANGRE justo antes; el indice unico hace fallar nuestro insert
        var ctx = SingleMemberWithOneHit();
        ctx.Uow.Setup(u => u.Achievements.InsertAsync(It.IsAny<UserAchievement>()))
            .Returns((UserAchievement a) =>
            {
                if (a.Code == AchievementCatalog.PrimeraSangre)
                {
                    ctx.Store.Add(new UserAchievement { UserId = a.UserId, Code = a.Code, SeasonId = a.SeasonId });
                    throw new InvalidOperationException("duplicate key");
                }
                ctx.Store.Add(a);
                return Task.FromResult(true);
            });

        // Act
        var act = () => ctx.Build().EvaluateWeekAsync(QuinielaId, 1);

        // Assert
        await act.Should().NotThrowAsync();
        ctx.CodesOf(101).Should().Contain(new[] { AchievementCatalog.PrimeraSangre, AchievementCatalog.JornadaCompleta });
        ctx.Store.Count(a => a.Code == AchievementCatalog.PrimeraSangre).Should().Be(1);
    }

    [Fact]
    public async Task Grant_SiElInsertFallaYElTrofeoNoExiste_PropagaElError()
    {
        var ctx = new Ctx();
        ctx.Uow.Setup(u => u.Achievements.InsertAsync(It.IsAny<UserAchievement>())).ThrowsAsync(new InvalidOperationException("db caida"));

        var act = () => ctx.Build().GrantAsync(101, AchievementCatalog.EnElPodio, QuinielaId, 1);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ---- COLECCIONISTA_DE_BRONCE ----
    private static Ctx MemberWithBronzeConditions(int weeks)
    {
        // Semanas de un partido, acierto manual; la primera es upset => Primera Sangre, Olfato, Jornada Completa y (con 5) Fiel.
        var ctx = new Ctx();
        ctx.Members.Add(new QuinielaMember { Id = 1, UserId = 101, Alias = "M1" });
        for (int w = 1; w <= weeks; w++)
        {
            var match = TrophyScenario.Match(w, w, upset: w == 1);
            ctx.Weeks.Add(match.Week);
            ctx.Picks.Add(TrophyScenario.Pick(1, match, true, upsetHit: w == 1));
        }
        return ctx;
    }

    [Fact]
    public async Task Coleccionista_ConTodosLosBronceOtorgadosEnLaMismaPasada_SeOtorga()
    {
        var ctx = MemberWithBronzeConditions(5);

        await ctx.Build().EvaluateWeekAsync(QuinielaId, 5);

        ctx.CodesOf(101).Should().Contain(AchievementCatalog.ColeccionistaDeBronce);
    }

    [Fact]
    public async Task Coleccionista_FaltandoUnBronce_NoSeOtorga()
    {
        var ctx = MemberWithBronzeConditions(4); // sin Fiel al Coliseo

        await ctx.Build().EvaluateWeekAsync(QuinielaId, 4);

        ctx.CodesOf(101).Should().NotContain(AchievementCatalog.ColeccionistaDeBronce);
        ctx.CodesOf(101).Should().Contain(AchievementCatalog.PrimeraSangre);
    }

    [Fact]
    public async Task Coleccionista_CuentaTrofeosBronceYaObtenidosEnPasadasAnteriores()
    {
        // Arrange: Fiel al Coliseo ya estaba en la vitrina pero hoy solo se cumplen los otros tres
        var ctx = MemberWithBronzeConditions(4);
        ctx.Store.Add(new UserAchievement { UserId = 101, Code = AchievementCatalog.FielAlColiseo, SeasonId = 1 });

        // Act
        await ctx.Build().EvaluateWeekAsync(QuinielaId, 4);

        // Assert
        ctx.CodesOf(101).Should().Contain(AchievementCatalog.ColeccionistaDeBronce);
    }

    // ---- GrantAsync ----
    [Fact]
    public async Task Grant_CreaElTrofeoYDevuelveTrue()
    {
        var ctx = new Ctx();

        var result = await ctx.Build().GrantAsync(101, AchievementCatalog.IndiscutidoDelColiseo, QuinielaId, 4);

        result.Should().BeTrue();
        var saved = ctx.Store.Should().ContainSingle().Subject;
        saved.Code.Should().Be(AchievementCatalog.IndiscutidoDelColiseo);
        saved.SeasonId.Should().Be(4);
        saved.QuinielaId.Should().Be(QuinielaId);
        saved.UserId.Should().Be(101);
    }

    [Fact]
    public async Task Grant_SiYaLoTieneEnLaTemporada_DevuelveFalseYNoDuplica()
    {
        var ctx = new Ctx();
        var sut = ctx.Build();
        await sut.GrantAsync(101, AchievementCatalog.EnElPodio, QuinielaId, 4);

        var second = await sut.GrantAsync(101, AchievementCatalog.EnElPodio, QuinielaId, 4);

        second.Should().BeFalse();
        ctx.Store.Should().HaveCount(1);
    }

    [Fact]
    public async Task Grant_CodigoInexistenteEnElCatalogo_DevuelveFalse()
    {
        var ctx = new Ctx();

        var result = await ctx.Build().GrantAsync(101, "NO_EXISTE", QuinielaId, 4);

        result.Should().BeFalse();
        ctx.Store.Should().BeEmpty();
    }

    // ---- Unseen / Acknowledge ----
    [Fact]
    public async Task GetUnseen_MapeaConDatosDelCatalogo()
    {
        var ctx = new Ctx();
        ctx.Uow.Setup(u => u.Achievements.GetUnseenAsync(101)).ReturnsAsync(new List<UserAchievement>
        {
            new() { Id = 5, UserId = 101, Code = AchievementCatalog.SemanaInvicta, SeasonId = 2, UnlockedAt = DateTime.UtcNow }
        });

        var result = await ctx.Build().GetUnseenAsync(101);

        result.isSuccess.Should().BeTrue();
        var dto = result.Data!.Should().ContainSingle().Subject;
        dto.Id.Should().Be(5);
        dto.Name.Should().Be("Semana Invicta");
        dto.Tier.Should().Be(AchievementCatalog.Gold);
        dto.Gamerscore.Should().Be(75);
        dto.TitleName.Should().Be("Intocable");
        dto.IsSeen.Should().BeFalse();
    }

    [Fact]
    public async Task Acknowledge_MarcaComoVistoYPersiste()
    {
        var ctx = new Ctx();
        var achievement = new UserAchievement { Id = 5, UserId = 101, Code = AchievementCatalog.PrimeraSangre, SeasonId = 1 };
        ctx.Uow.Setup(u => u.Achievements.GetByIdAsync(5)).ReturnsAsync(achievement);

        var result = await ctx.Build().AcknowledgeAsync(101, 5);

        result.isSuccess.Should().BeTrue();
        achievement.IsSeen.Should().BeTrue();
        achievement.SeenAt.Should().NotBeNull();
        ctx.Uow.Verify(u => u.Achievements.UpdateAsync(achievement), Times.Once);
    }

    [Fact]
    public async Task Acknowledge_YaVisto_EsIdempotenteSinError()
    {
        var ctx = new Ctx();
        var seenAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        var achievement = new UserAchievement { Id = 5, UserId = 101, Code = AchievementCatalog.PrimeraSangre, SeasonId = 1, IsSeen = true, SeenAt = seenAt };
        ctx.Uow.Setup(u => u.Achievements.GetByIdAsync(5)).ReturnsAsync(achievement);

        var result = await ctx.Build().AcknowledgeAsync(101, 5);

        result.isSuccess.Should().BeTrue();
        achievement.SeenAt.Should().Be(seenAt);
    }

    [Fact]
    public async Task Acknowledge_TrofeoDeOtroUsuario_Rechaza()
    {
        var ctx = new Ctx();
        var achievement = new UserAchievement { Id = 5, UserId = 999, Code = AchievementCatalog.PrimeraSangre, SeasonId = 1 };
        ctx.Uow.Setup(u => u.Achievements.GetByIdAsync(5)).ReturnsAsync(achievement);

        var result = await ctx.Build().AcknowledgeAsync(101, 5);

        result.isSuccess.Should().BeFalse();
        result.Message.Should().Be("Trofeo no encontrado.");
        achievement.IsSeen.Should().BeFalse();
        ctx.Uow.Verify(u => u.Achievements.UpdateAsync(It.IsAny<UserAchievement>()), Times.Never);
    }

    [Fact]
    public async Task Acknowledge_TrofeoInexistente_Rechaza()
    {
        var ctx = new Ctx();
        ctx.Uow.Setup(u => u.Achievements.GetByIdAsync(5)).ReturnsAsync((UserAchievement?)null);

        var result = await ctx.Build().AcknowledgeAsync(101, 5);

        result.isSuccess.Should().BeFalse();
        result.Message.Should().Be("Trofeo no encontrado.");
    }

    // ---- Vitrina ----
    private static UserAchievement Won(string code, int seasonId, int id = 0) =>
        new() { Id = id, UserId = 101, Code = code, SeasonId = seasonId, UnlockedAt = DateTime.UtcNow };

    private static Ctx ShowcaseCtx(User? user, params UserAchievement[] achievements)
    {
        var ctx = new Ctx();
        ctx.Uow.Setup(u => u.Users.GetAsync(101)).ReturnsAsync(user ?? new User { Id = 101 });
        ctx.Uow.Setup(u => u.Achievements.GetByUserAsync(101, null)).ReturnsAsync(achievements.ToList());
        ctx.Uow.Setup(u => u.Seasons.GetAsync(1)).ReturnsAsync(new Season { Id = 1, Year = 2026, Name = "Apertura 2026" });
        ctx.Uow.Setup(u => u.Seasons.GetAsync(2)).ReturnsAsync(new Season { Id = 2, Year = 2027, Name = "Clausura 2027" });
        return ctx;
    }

    [Fact]
    public async Task Showcase_GamerscoreYNivelSonPorTemporada()
    {
        // 120 G en la temporada 1 (Oro 75 + Plata 30 + Bronce 15) y 30 G en la 2 (una Plata)
        var ctx = ShowcaseCtx(null,
            Won(AchievementCatalog.SemanaInvicta, 1), Won(AchievementCatalog.EnElPodio, 1), Won(AchievementCatalog.PrimeraSangre, 1),
            Won(AchievementCatalog.RachaDeFuego, 2));

        var current = (await ctx.Build().GetShowcaseAsync(101, 2)).Data!;
        var previous = (await ctx.Build().GetShowcaseAsync(101, 1)).Data!;

        current.Gamerscore.Should().Be(30);
        current.Level.Should().Be("Novato");
        current.NextLevelAt.Should().Be(50);
        current.SeasonName.Should().Be("Clausura 2027");
        previous.Gamerscore.Should().Be(120);
        previous.Level.Should().Be("Aprendiz");
        previous.NextLevelAt.Should().Be(150);
        current.AvailableSeasons.Should().BeEquivalentTo(new[]
        {
            new DTO.Gamification.SeasonScoreDto { SeasonId = 1, SeasonName = "Apertura 2026", Gamerscore = 120 },
            new DTO.Gamification.SeasonScoreDto { SeasonId = 2, SeasonName = "Clausura 2027", Gamerscore = 30 }
        });
    }

    [Theory]
    [InlineData(2027, 2026, new[] { 1, 2 })]   // Id 1 = 2027, Id 2 = 2026: ordenar por Id descendente daria [2, 1]
    [InlineData(2026, 2027, new[] { 2, 1 })]   // Ids y anos crecientes: ordenar por Id ascendente daria [1, 2]
    public async Task Showcase_AvailableSeasonsSeOrdenaPorYearDescendente(int year1, int year2, int[] expectedOrder)
    {
        var ctx = ShowcaseCtx(null, Won(AchievementCatalog.PrimeraSangre, 1), Won(AchievementCatalog.PrimeraSangre, 2));
        ctx.Uow.Setup(u => u.Seasons.GetAsync(1)).ReturnsAsync(new Season { Id = 1, Year = year1, Name = "S1" });
        ctx.Uow.Setup(u => u.Seasons.GetAsync(2)).ReturnsAsync(new Season { Id = 2, Year = year2, Name = "S2" });

        var data = (await ctx.Build().GetShowcaseAsync(101, 1)).Data!;

        data.AvailableSeasons.Select(s => s.SeasonId).Should().Equal(expectedOrder);
    }

    [Fact]
    public async Task Showcase_SeparaDesbloqueadosYBloqueados()
    {
        var ctx = ShowcaseCtx(null, Won(AchievementCatalog.PrimeraSangre, 1), Won(AchievementCatalog.PrimeraSangre, 2));

        var data = (await ctx.Build().GetShowcaseAsync(101, 1)).Data!;

        data.Unlocked.Should().ContainSingle(a => a.Code == AchievementCatalog.PrimeraSangre);
        data.Locked.Should().HaveCount(AchievementCatalog.All.Count - 1);
        data.Locked.Should().NotContain(l => l.Code == AchievementCatalog.PrimeraSangre);
    }

    [Fact]
    public async Task Showcase_SinTemporada_UsaLaMasRecienteConGamificacionActiva()
    {
        // Arrange: el orden por Year (1 -> 2027 > 2 -> 2026) es el INVERSO del orden por Id, y la temporada 3
        // (la mas reciente de todas, 2030) pertenece a una quiniela sin gamificacion y debe ignorarse.
        var ctx = ShowcaseCtx(null, Won(AchievementCatalog.PrimeraSangre, 1), Won(AchievementCatalog.EnElPodio, 2));
        ctx.Uow.Setup(u => u.Seasons.GetAsync(1)).ReturnsAsync(new Season { Id = 1, Year = 2027, Name = "Clausura 2027" });
        ctx.Uow.Setup(u => u.Seasons.GetAsync(2)).ReturnsAsync(new Season { Id = 2, Year = 2026, Name = "Apertura 2026" });
        ctx.Uow.Setup(u => u.Seasons.GetAsync(3)).ReturnsAsync(new Season { Id = 3, Year = 2030, Name = "Futura 2030" });
        ctx.Uow.Setup(u => u.Quinielas.GetUserQuinielasAsync(101)).ReturnsAsync(new List<Quiniela>
        {
            new() { Id = 1, SeasonId = 1, GamificationEnabled = true },
            new() { Id = 2, SeasonId = 2, GamificationEnabled = true },
            new() { Id = 3, SeasonId = 3, GamificationEnabled = false }
        });

        // Act
        var data = (await ctx.Build().GetShowcaseAsync(101, null)).Data!;

        // Assert
        data.SeasonId.Should().Be(1);
        data.Gamerscore.Should().Be(15);
    }

    [Fact]
    public async Task Showcase_SinQuinielasConGamificacion_DevuelveVitrinaVacia()
    {
        var ctx = ShowcaseCtx(null);
        ctx.Uow.Setup(u => u.Quinielas.GetUserQuinielasAsync(101)).ReturnsAsync(new List<Quiniela>
        {
            new() { Id = 1, SeasonId = 1, GamificationEnabled = false }
        });

        var data = (await ctx.Build().GetShowcaseAsync(101, null)).Data!;

        data.SeasonId.Should().BeNull();
        data.Gamerscore.Should().Be(0);
        data.Level.Should().Be("Novato");
        data.Unlocked.Should().BeEmpty();
        data.Locked.Should().HaveCount(AchievementCatalog.All.Count);
    }

    [Fact]
    public async Task Showcase_IncluyeElTituloEquipado()
    {
        var ctx = ShowcaseCtx(new User { Id = 101, EquippedTitleCode = AchievementCatalog.SoloYoLoVi });

        var data = (await ctx.Build().GetShowcaseAsync(101, 1)).Data!;

        data.EquippedTitleCode.Should().Be(AchievementCatalog.SoloYoLoVi);
        data.EquippedTitle.Should().Be("Ojo de Halcón");
    }

    // ---- EquipTitle ----
    [Fact]
    public async Task EquipTitle_AceptaUnTituloGanadoEnOtraTemporada()
    {
        var user = new User { Id = 101 };
        var ctx = ShowcaseCtx(user, Won(AchievementCatalog.SoloYoLoVi, 1));

        var result = await ctx.Build().EquipTitleAsync(101, AchievementCatalog.SoloYoLoVi);

        result.isSuccess.Should().BeTrue();
        user.EquippedTitleCode.Should().Be(AchievementCatalog.SoloYoLoVi);
        ctx.Uow.Verify(u => u.Users.UpdateAsync(user), Times.Once);
        result.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task EquipTitle_TituloNoDesbloqueado_Rechaza()
    {
        var user = new User { Id = 101 };
        var ctx = ShowcaseCtx(user, Won(AchievementCatalog.PrimeraSangre, 1));

        var result = await ctx.Build().EquipTitleAsync(101, AchievementCatalog.SemanaInvicta);

        result.isSuccess.Should().BeFalse();
        result.Message.Should().Be("No has desbloqueado ese título.");
        user.EquippedTitleCode.Should().BeNull();
        ctx.Uow.Verify(u => u.Users.UpdateAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task EquipTitle_TrofeoDesbloqueadoPeroSinTitulo_Rechaza()
    {
        var user = new User { Id = 101 };
        var ctx = ShowcaseCtx(user, Won(AchievementCatalog.PrimeraSangre, 1));

        var result = await ctx.Build().EquipTitleAsync(101, AchievementCatalog.PrimeraSangre);

        result.isSuccess.Should().BeFalse();
        result.Message.Should().Be("No has desbloqueado ese título.");
        user.EquippedTitleCode.Should().BeNull();
    }

    [Fact]
    public async Task EquipTitle_CodigoInexistente_Rechaza()
    {
        var user = new User { Id = 101 };
        var ctx = ShowcaseCtx(user, Won(AchievementCatalog.SemanaInvicta, 1));

        var result = await ctx.Build().EquipTitleAsync(101, "NO_EXISTE");

        result.isSuccess.Should().BeFalse();
        result.Message.Should().Be("No has desbloqueado ese título.");
    }

    [Fact]
    public async Task EquipTitle_CodigoNulo_Desequipa()
    {
        var user = new User { Id = 101, EquippedTitleCode = AchievementCatalog.SemanaInvicta };
        var ctx = ShowcaseCtx(user, Won(AchievementCatalog.SemanaInvicta, 1));

        var result = await ctx.Build().EquipTitleAsync(101, null);

        result.isSuccess.Should().BeTrue();
        user.EquippedTitleCode.Should().BeNull();
        ctx.Uow.Verify(u => u.Users.UpdateAsync(user), Times.Once);
    }
}
