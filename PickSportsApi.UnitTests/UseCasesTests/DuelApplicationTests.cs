using Domain.Entities;
using DTO.Duels;
using FluentAssertions;
using Interface.Persistence;
using Interface.UseCases;
using Moq;
using UseCases.Duels;
using Match = Domain.Entities.Match;

namespace PickSportsApi.UnitTests.UseCasesTests;

public class DuelApplicationTests
{
    private const int QuinielaId = 10, SeasonId = 7, LeagueId = 5, OwnerUserId = 100, WeekId = 33;

    // ----------------------------------------------------------------- Infraestructura

    private sealed class Ctx
    {
        public readonly Mock<IUnitOfWork> Uow = new() { DefaultValue = DefaultValue.Mock };
        public readonly Mock<IDuelRepository> Duels = new();
        public readonly Mock<IAchievementApplication> Achievements = new();
        public Quiniela Quiniela = new() { Id = QuinielaId, SeasonId = SeasonId, LeagueId = LeagueId, GamificationEnabled = true };
        public List<QuinielaMember> Members = new();
        public List<Pick> WeekPicks = new();
        public List<Pick> QuinielaPicks = new();
        public List<DuelFixture> AllFixtures = new();
        public List<DuelFixture> WeekFixtures = new();
        public QuinielaDuelSeason? SeasonInfo;
        public int WeekNumber = 3;

        public DuelApplication Sut => new(Uow.Object, Achievements.Object);

        public Ctx()
        {
            Uow.Setup(u => u.Duels).Returns(Duels.Object);
            Uow.Setup(u => u.Quinielas.GetAsync(QuinielaId)).ReturnsAsync(() => Quiniela);
            Uow.Setup(u => u.Leagues.GetAsync(LeagueId)).ReturnsAsync(new League { Id = LeagueId, WeeksCount = 17 });
            Uow.Setup(u => u.QuinielaMembers.GetMembersAsync(QuinielaId)).ReturnsAsync(() => Members);
            Uow.Setup(u => u.Weeks.GetAsync(WeekId)).ReturnsAsync(() => new Week { Id = WeekId, WeekNumber = WeekNumber, SeasonId = SeasonId });
            Uow.Setup(u => u.Picks.GetAllPicksForWeekAsync(QuinielaId, WeekId)).ReturnsAsync(() => WeekPicks);
            Uow.Setup(u => u.Picks.GetAllPicksForQuinielaAsync(QuinielaId)).ReturnsAsync(() => QuinielaPicks);
            Uow.Setup(u => u.WeeklyAwards.GetAllAwardsForQuinielaAsync(QuinielaId)).ReturnsAsync(new List<WeeklyAward>());
            Duels.Setup(d => d.GetFixturesAsync(QuinielaId)).ReturnsAsync(() => AllFixtures);
            Duels.Setup(d => d.GetFixturesByWeekNumberAsync(QuinielaId, It.IsAny<int>())).ReturnsAsync(() => WeekFixtures);
            Duels.Setup(d => d.GetSeasonInfoAsync(QuinielaId)).ReturnsAsync(() => SeasonInfo);
            Duels.Setup(d => d.AnyResolvedAsync(QuinielaId)).ReturnsAsync(false);
            Duels.Setup(d => d.UpdateFixtureAsync(It.IsAny<DuelFixture>())).ReturnsAsync(true);
            Duels.Setup(d => d.InsertFixturesAsync(It.IsAny<IEnumerable<DuelFixture>>())).ReturnsAsync(true);
            Duels.Setup(d => d.UpsertSeasonInfoAsync(It.IsAny<QuinielaDuelSeason>())).ReturnsAsync(true);
        }

        public Ctx WithMembership(int userId, string role)
        {
            Uow.Setup(u => u.QuinielaMembers.GetMembershipAsync(QuinielaId, userId))
                .ReturnsAsync(new QuinielaMember { Id = 900 + userId, UserId = userId, QuinielaId = QuinielaId, Role = role, Alias = "x" });
            return this;
        }

        public Ctx WithUser(int userId, string role)
        {
            Uow.Setup(u => u.Users.GetAsync(userId)).ReturnsAsync(new User { Id = userId, Role = role });
            return this;
        }

        public Ctx WithMembers(int count)
        {
            Members = Enumerable.Range(1, count).Select(i => Member(i)).ToList();
            return this;
        }

        /// <summary>Jornadas previas: el miembro i obtiene (count - i + 1) aciertos, asi su Rank en la Tabla General es i.</summary>
        public Ctx WithPriorRanks(int count)
        {
            var week = new Week { Id = 1, WeekNumber = 1, SeasonId = SeasonId };
            var match = new Match { Id = 1, WeekId = 1, Week = week };
            QuinielaPicks = new List<Pick>();
            for (var m = 1; m <= count; m++)
                for (var h = 0; h < count - m + 1; h++)
                    QuinielaPicks.Add(new Pick { MemberId = m, IsHit = true, MatchId = 1, Match = match, PickAbbr = "A" });
            return this;
        }

        public Ctx WithWeekHits(params (int memberId, int hits)[] hits)
        {
            WeekPicks = new List<Pick>();
            foreach (var (memberId, count) in hits)
                for (var i = 0; i < count; i++)
                    WeekPicks.Add(new Pick { MemberId = memberId, IsHit = true, PickAbbr = "A" });
            return this;
        }
    }

    private static QuinielaMember Member(int id, string? alias = null, int totalHits = 0, string? titleCode = null) => new()
    {
        Id = id,
        UserId = OwnerUserId + id,
        QuinielaId = QuinielaId,
        Alias = alias ?? $"M{id}",
        TotalHits = totalHits,
        User = new User { Id = OwnerUserId + id, DisplayName = $"User {id}", EquippedTitleCode = titleCode },
    };

    [Fact]
    public void CalculateChampionStats_ComputesStreakAdvantageAndBestWeek()
    {
        // Arrange: campeon = miembro 1. G (local), G (visita, por ventaja), E, G, BYE ignorado.
        var adv = Fx(2, 3, 5, 1, "AWAY", hh: 4, ah: 4);
        adv.WonByAdvantage = true;
        var fixtures = new List<DuelFixture>
        {
            Fx(1, 1, 1, 2, "HOME", hh: 6, ah: 2),
            adv,
            Fx(3, 5, 1, 3, "DRAW", hh: 5, ah: 5),
            Fx(4, 7, 4, 1, "AWAY", hh: 1, ah: 3),
            Fx(5, 9, 1, null, "BYE"),
        };

        // Act
        var stats = DuelApplication.CalculateChampionStats(fixtures, 1);

        // Assert
        stats.AdvantageWins.Should().Be(1);
        stats.BestWinStreak.Should().Be(2);
        stats.BestDuelHits.Should().Be(6);
        stats.BestDuelWeekNumber.Should().Be(1);
    }

    [Fact]
    public void CalculateChampionStats_WithoutPlayedDuels_ReturnsZeros()
    {
        var stats = DuelApplication.CalculateChampionStats(new List<DuelFixture> { Fx(1, 1, 1, null, "BYE") }, 1);

        stats.Should().Be((0, 0, 0, (int?)null));
    }

    private static DuelFixture Fx(int id, int week, int home, int? away, string? result = null, int hh = 0, int ah = 0) => new()
    {
        Id = id,
        QuinielaId = QuinielaId,
        WeekNumber = week,
        Round = week,
        Leg = 1,
        HomeMemberId = home,
        HomeMember = Member(home),
        AwayMemberId = away,
        AwayMember = away.HasValue ? Member(away.Value) : null,
        Result = result,
        HomeHits = hh,
        AwayHits = ah,
    };

    // ----------------------------------------------------------------- ScheduleAsync: validaciones

    [Fact]
    public async Task Schedule_QuinielaInexistente_Falla()
    {
        var ctx = new Ctx { Quiniela = null! };

        var result = await ctx.Sut.ScheduleAsync(QuinielaId, OwnerUserId);

        result.isSuccess.Should().BeFalse();
        result.Message.Should().Be("La quiniela no existe.");
    }

    [Fact]
    public async Task Schedule_UsuarioSinPermiso_Falla()
    {
        var ctx = new Ctx().WithMembers(4).WithMembership(OwnerUserId, "MEMBER").WithUser(OwnerUserId, "USER");

        var result = await ctx.Sut.ScheduleAsync(QuinielaId, OwnerUserId);

        result.isSuccess.Should().BeFalse();
        result.Message.Should().Be("Solo el Owner o un Admin puede generar los duelos.");
        ctx.Duels.Verify(d => d.InsertFixturesAsync(It.IsAny<IEnumerable<DuelFixture>>()), Times.Never);
    }

    [Fact]
    public async Task Schedule_GamificacionApagada_Falla()
    {
        var ctx = new Ctx().WithMembers(4).WithMembership(OwnerUserId, "OWNER");
        ctx.Quiniela.GamificationEnabled = false;

        var result = await ctx.Sut.ScheduleAsync(QuinielaId, OwnerUserId);

        result.isSuccess.Should().BeFalse();
        result.Message.Should().Be("La gamificación no está activada en esta quiniela.");
    }

    [Fact]
    public async Task Schedule_SinTemporada_Falla()
    {
        var ctx = new Ctx().WithMembers(4).WithMembership(OwnerUserId, "OWNER");
        ctx.Quiniela.SeasonId = null;

        var result = await ctx.Sut.ScheduleAsync(QuinielaId, OwnerUserId);

        result.isSuccess.Should().BeFalse();
        result.Message.Should().Be("La quiniela no tiene temporada asignada.");
    }

    [Fact]
    public async Task Schedule_LigaSinJornadasRegulares_Falla()
    {
        var ctx = new Ctx().WithMembers(4).WithMembership(OwnerUserId, "OWNER");
        ctx.Uow.Setup(u => u.Leagues.GetAsync(LeagueId)).ReturnsAsync(new League { Id = LeagueId, WeeksCount = 0 });

        var result = await ctx.Sut.ScheduleAsync(QuinielaId, OwnerUserId);

        result.isSuccess.Should().BeFalse();
        result.Message.Should().Be("La liga no tiene jornadas regulares definidas.");
    }

    [Fact]
    public async Task Schedule_MenosDeDosMiembros_Falla()
    {
        var ctx = new Ctx().WithMembers(1).WithMembership(OwnerUserId, "OWNER");

        var result = await ctx.Sut.ScheduleAsync(QuinielaId, OwnerUserId);

        result.isSuccess.Should().BeFalse();
        result.Message.Should().Be("Se necesitan al menos 2 miembros para generar duelos.");
    }

    [Fact]
    public async Task Schedule_ConDuelosResueltos_NoRegenera()
    {
        var ctx = new Ctx().WithMembers(4).WithMembership(OwnerUserId, "OWNER");
        ctx.Duels.Setup(d => d.AnyResolvedAsync(QuinielaId)).ReturnsAsync(true);

        var result = await ctx.Sut.ScheduleAsync(QuinielaId, OwnerUserId);

        result.isSuccess.Should().BeFalse();
        result.Message.Should().Be("Ya hay duelos resueltos; no se puede regenerar el calendario.");
        ctx.Duels.Verify(d => d.DeleteFixturesAsync(It.IsAny<int>()), Times.Never);
    }

    // ----------------------------------------------------------------- ScheduleAsync: exito

    [Fact]
    public async Task Schedule_Owner_GeneraCalendarioYGuardaSemilla()
    {
        var ctx = new Ctx().WithMembers(9).WithMembership(OwnerUserId, "OWNER");
        List<DuelFixture>? inserted = null;
        ctx.Duels.Setup(d => d.InsertFixturesAsync(It.IsAny<IEnumerable<DuelFixture>>()))
            .Callback<IEnumerable<DuelFixture>>(f => inserted = f.ToList()).ReturnsAsync(true);
        QuinielaDuelSeason? saved = null;
        ctx.Duels.Setup(d => d.UpsertSeasonInfoAsync(It.IsAny<QuinielaDuelSeason>()))
            .Callback<QuinielaDuelSeason>(s => saved = s).ReturnsAsync(true);

        var result = await ctx.Sut.ScheduleAsync(QuinielaId, OwnerUserId);

        result.isSuccess.Should().BeTrue();
        inserted.Should().NotBeNullOrEmpty();
        inserted!.Should().OnlyContain(f => f.QuinielaId == QuinielaId);
        ctx.Duels.Verify(d => d.DeleteFixturesAsync(QuinielaId), Times.Once);
        saved.Should().NotBeNull();
        saved!.QuinielaId.Should().Be(QuinielaId);
        saved.ClosedAt.Should().BeNull();
        result.Data.TotalWeeks.Should().Be(17);
        result.Data.DuelWeeks.Should().Equal(1, 3, 5, 7, 10, 12, 14, 16, 17);
        result.Data.RestWeeks.Should().Equal(2, 4, 6, 8, 9, 11, 13, 15);
        inserted.Select(f => f.WeekNumber).Distinct().OrderBy(w => w).Should().Equal(result.Data.DuelWeeks);
        ctx.Uow.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Schedule_AdminDeSistema_SinMembresia_Puede()
    {
        var ctx = new Ctx().WithMembers(4).WithUser(OwnerUserId, "ADMIN");

        var result = await ctx.Sut.ScheduleAsync(QuinielaId, OwnerUserId);

        result.isSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Schedule_Regenerar_ConservaCopaYReseteaCierre()
    {
        var ctx = new Ctx().WithMembers(4).WithMembership(OwnerUserId, "ADMIN");
        ctx.SeasonInfo = new QuinielaDuelSeason
        {
            QuinielaId = QuinielaId, Seed = 1, CupName = "Copa Vieja", PunishmentText = "Pagar tacos",
            ChampionMemberId = 2, ClosedAt = DateTime.UtcNow,
        };
        QuinielaDuelSeason? saved = null;
        ctx.Duels.Setup(d => d.UpsertSeasonInfoAsync(It.IsAny<QuinielaDuelSeason>()))
            .Callback<QuinielaDuelSeason>(s => saved = s).ReturnsAsync(true);

        var result = await ctx.Sut.ScheduleAsync(QuinielaId, OwnerUserId);

        result.isSuccess.Should().BeTrue();
        saved!.CupName.Should().Be("Copa Vieja");
        saved.PunishmentText.Should().Be("Pagar tacos");
        saved.ChampionMemberId.Should().BeNull();
        saved.ClosedAt.Should().BeNull();
    }

    // ----------------------------------------------------------------- ResolveWeekAsync: resultado

    [Theory]
    [InlineData(5, 3, "HOME")]
    [InlineData(2, 4, "AWAY")]
    [InlineData(3, 3, "DRAW")]
    public async Task ResolveWeek_JornadaUno_ResuelveSegunAciertos(int homeHits, int awayHits, string expected)
    {
        var ctx = new Ctx { WeekNumber = 1 }.WithMembers(2).WithWeekHits((1, homeHits), (2, awayHits));
        var fixture = Fx(1, 1, 1, 2);
        ctx.WeekFixtures = new() { fixture };
        ctx.AllFixtures = new() { fixture, Fx(2, 2, 1, 2) };

        await ctx.Sut.ResolveWeekAsync(QuinielaId, WeekId);

        fixture.Result.Should().Be(expected);
        fixture.HomeHits.Should().Be(homeHits);
        fixture.AwayHits.Should().Be(awayHits);
        fixture.WonByAdvantage.Should().BeFalse();
        fixture.HomeRankBefore.Should().BeNull();
        fixture.ResolvedAt.Should().NotBeNull();
        ctx.Duels.Verify(d => d.UpdateFixtureAsync(fixture), Times.Once);
    }

    [Fact]
    public async Task ResolveWeek_JornadaUno_EmpateAunqueHayaBrecha_NoAplicaVentaja()
    {
        var ctx = new Ctx { WeekNumber = 1 }.WithMembers(6).WithPriorRanks(6).WithWeekHits((1, 2), (6, 2));
        var fixture = Fx(1, 1, 1, 6);
        ctx.WeekFixtures = new() { fixture };
        ctx.AllFixtures = new() { fixture, Fx(2, 2, 1, 6) };

        await ctx.Sut.ResolveWeekAsync(QuinielaId, WeekId);

        fixture.Result.Should().Be("DRAW");
        fixture.WonByAdvantage.Should().BeFalse();
    }

    [Fact]
    public async Task ResolveWeek_Bye_NoSumaNiResta()
    {
        var ctx = new Ctx().WithMembers(3).WithPriorRanks(3).WithWeekHits((1, 4));
        var bye = Fx(1, 3, 1, null);
        ctx.WeekFixtures = new() { bye };
        ctx.AllFixtures = new() { bye, Fx(2, 9, 1, 2) };

        await ctx.Sut.ResolveWeekAsync(QuinielaId, WeekId);

        bye.Result.Should().Be("BYE");
        bye.HomeHits.Should().Be(0);
        bye.AwayHits.Should().Be(0);
        bye.WonByAdvantage.Should().BeFalse();
    }

    // ----------------------------------------------------------------- Ventaja del Retador

    private static (Ctx ctx, DuelFixture fixture) AdvantageSetup(int home, int away, (int memberId, int hits)[] hits)
    {
        var ctx = new Ctx { WeekNumber = 3 }.WithMembers(6).WithPriorRanks(6).WithWeekHits(hits);
        var fixture = Fx(1, 3, home, away);
        ctx.WeekFixtures = new() { fixture };
        ctx.AllFixtures = new() { fixture, Fx(2, 9, 1, 2) };
        return (ctx, fixture);
    }

    [Fact]
    public async Task ResolveWeek_EmpateConBrechaDeTres_GanaElDeAbajo()
    {
        var (ctx, fixture) = AdvantageSetup(1, 4, new[] { (1, 2), (4, 2) });

        await ctx.Sut.ResolveWeekAsync(QuinielaId, WeekId);

        fixture.Result.Should().Be("AWAY");
        fixture.WonByAdvantage.Should().BeTrue();
        fixture.HomeRankBefore.Should().Be(1);
        fixture.AwayRankBefore.Should().Be(4);
    }

    [Fact]
    public async Task ResolveWeek_EmpateConBrechaDeTres_LocalDeAbajoGana()
    {
        var (ctx, fixture) = AdvantageSetup(6, 2, new[] { (6, 3), (2, 3) });

        await ctx.Sut.ResolveWeekAsync(QuinielaId, WeekId);

        fixture.Result.Should().Be("HOME");
        fixture.WonByAdvantage.Should().BeTrue();
    }

    [Fact]
    public async Task ResolveWeek_EmpateConBrechaDeDos_EsEmpate()
    {
        var (ctx, fixture) = AdvantageSetup(3, 5, new[] { (3, 2), (5, 2) });

        await ctx.Sut.ResolveWeekAsync(QuinielaId, WeekId);

        fixture.Result.Should().Be("DRAW");
        fixture.WonByAdvantage.Should().BeFalse();
        fixture.HomeRankBefore.Should().Be(3);
        fixture.AwayRankBefore.Should().Be(5);
    }

    [Fact]
    public async Task ResolveWeek_BrechaDeTresSinEmpate_GanaQuienTieneMasAciertos()
    {
        var (ctx, fixture) = AdvantageSetup(1, 4, new[] { (1, 3), (4, 2) });

        await ctx.Sut.ResolveWeekAsync(QuinielaId, WeekId);

        fixture.Result.Should().Be("HOME");
        fixture.WonByAdvantage.Should().BeFalse();
    }

    // ----------------------------------------------------------------- Idempotencia y descanso

    [Fact]
    public async Task ResolveWeek_Recalificar_RecalculaYReseteaVentaja()
    {
        var (ctx, fixture) = AdvantageSetup(1, 4, new[] { (1, 2), (4, 2) });
        await ctx.Sut.ResolveWeekAsync(QuinielaId, WeekId);
        fixture.Result.Should().Be("AWAY");
        fixture.WonByAdvantage.Should().BeTrue();

        // El resultado oficial cambia: ahora el lider tiene mas aciertos.
        ctx.WithWeekHits((1, 4), (4, 2));
        await ctx.Sut.ResolveWeekAsync(QuinielaId, WeekId);
        fixture.Result.Should().Be("HOME");
        fixture.WonByAdvantage.Should().BeFalse();

        // Recalificar sin cambios deja el mismo resultado.
        await ctx.Sut.ResolveWeekAsync(QuinielaId, WeekId);
        fixture.Result.Should().Be("HOME");
        fixture.HomeHits.Should().Be(4);
        fixture.AwayHits.Should().Be(2);
    }

    [Fact]
    public async Task ResolveWeek_FechaDeDescanso_NoTocaNada()
    {
        var ctx = new Ctx { WeekNumber = 4 }.WithMembers(4);
        ctx.WeekFixtures = new();
        ctx.AllFixtures = new() { Fx(1, 3, 1, 2), Fx(2, 9, 1, 2) };

        await ctx.Sut.ResolveWeekAsync(QuinielaId, WeekId);

        ctx.Duels.Verify(d => d.UpdateFixtureAsync(It.IsAny<DuelFixture>()), Times.Never);
        ctx.Duels.Verify(d => d.UpsertSeasonInfoAsync(It.IsAny<QuinielaDuelSeason>()), Times.Never);
        ctx.Achievements.Verify(a => a.GrantAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    // ----------------------------------------------------------------- Cierre y campeon

    private static Ctx ClosingSetup(
        QuinielaMember a, QuinielaMember b, string earlierResult, (int, int) lastWeekHits)
    {
        var ctx = new Ctx { WeekNumber = 5 };
        ctx.Members = new() { a, b };
        ctx.WithWeekHits((a.Id, lastWeekHits.Item1), (b.Id, lastWeekHits.Item2));
        var earlier = Fx(1, 4, a.Id, b.Id, earlierResult, 2, 2);
        earlier.HomeMember = a;
        earlier.AwayMember = b;
        var last = Fx(2, 5, a.Id, b.Id);
        ctx.WeekFixtures = new() { last };
        // Copia "vieja" de la ultima fecha con un resultado que cambiaria al campeon: el codigo debe usar la recien calculada.
        ctx.AllFixtures = new() { earlier, Fx(2, 5, a.Id, b.Id, "AWAY", 0, 9) };
        return ctx;
    }

    [Fact]
    public async Task Schedule_FalloAlInsertar_HaceRollbackSinCommit()
    {
        var ctx = new Ctx().WithMembers(4).WithMembership(OwnerUserId, "OWNER");
        ctx.Duels.Setup(d => d.InsertFixturesAsync(It.IsAny<IEnumerable<DuelFixture>>())).ThrowsAsync(new InvalidOperationException("boom"));

        var result = await ctx.Sut.ScheduleAsync(QuinielaId, OwnerUserId);

        result.isSuccess.Should().BeFalse();
        result.Message.Should().Be("Error al generar el calendario de duelos. No se modificó el calendario anterior.");
        ctx.Uow.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        ctx.Uow.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResolveWeek_SinPicksCalificados_NoResuelveNada()
    {
        var ctx = new Ctx { WeekNumber = 3 }.WithMembers(2).WithPriorRanks(2);
        ctx.WeekPicks = new() { new Pick { MemberId = 1, IsHit = null, PickAbbr = "A" }, new Pick { MemberId = 2, IsHit = null, PickAbbr = "A" } };
        var fixture = Fx(1, 3, 1, 2);
        ctx.WeekFixtures = new() { fixture };
        ctx.AllFixtures = new() { fixture };

        await ctx.Sut.ResolveWeekAsync(QuinielaId, WeekId);

        ctx.Duels.Verify(d => d.UpdateFixtureAsync(It.IsAny<DuelFixture>()), Times.Never);
        fixture.Result.Should().BeNull();
        ctx.Duels.Verify(d => d.UpsertSeasonInfoAsync(It.IsAny<QuinielaDuelSeason>()), Times.Never);
    }

    [Fact]
    public async Task ResolveWeek_UltimaFechaAntesQueIntermedia_NoCierra_YCierraAlResolverLaPendiente()
    {
        var a = Member(1, "Ana", 10); var b = Member(2, "Beto", 30);
        var ctx = new Ctx { WeekNumber = 5 };
        ctx.Members = new() { a, b };
        ctx.WithWeekHits((1, 4), (2, 1));
        var pending = Fx(1, 4, 1, 2);            // intermedia aun sin calificar
        var last = Fx(2, 5, 1, 2);
        ctx.WeekFixtures = new() { last };
        ctx.AllFixtures = new() { pending, Fx(2, 5, 1, 2) };

        await ctx.Sut.ResolveWeekAsync(QuinielaId, WeekId);

        ctx.Duels.Verify(d => d.UpsertSeasonInfoAsync(It.IsAny<QuinielaDuelSeason>()), Times.Never);
        ctx.Achievements.Verify(x => x.GrantAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);

        // Ahora se califica la intermedia (semana 4): la 5 ya esta resuelta.
        ctx.WeekNumber = 4;
        ctx.WithWeekHits((1, 1), (2, 3));
        var pendingTracked = Fx(1, 4, 1, 2);
        ctx.WeekFixtures = new() { pendingTracked };
        ctx.AllFixtures = new() { Fx(1, 4, 1, 2), last };
        QuinielaDuelSeason? saved = null;
        ctx.Duels.Setup(d => d.UpsertSeasonInfoAsync(It.IsAny<QuinielaDuelSeason>()))
            .Callback<QuinielaDuelSeason>(s => saved = s).ReturnsAsync(true);

        await ctx.Sut.ResolveWeekAsync(QuinielaId, WeekId);

        saved.Should().NotBeNull();
        saved!.ChampionMemberId.Should().Be(2, "3 pts y 1 victoria cada uno con la tabla completa; desempata TotalHits (Beto 30 > Ana 10)");
        ctx.Achievements.Verify(x => x.GrantAsync(b.UserId, "INDISCUTIDO_DEL_COLISEO", QuinielaId, SeasonId), Times.Once);
    }

    [Fact]
    public async Task ResolveWeek_YaCerrada_ConservaClosedAtYRecalculaCampeon()
    {
        var a = Member(1, "Ana", 10); var b = Member(2, "Beto", 30);
        var ctx = ClosingSetup(a, b, "DRAW", (2, 4)); // B gana la ultima
        var originalClose = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var tracked = new QuinielaDuelSeason { QuinielaId = QuinielaId, ChampionMemberId = 1, ClosedAt = originalClose };
        ctx.SeasonInfo = tracked;

        await ctx.Sut.ResolveWeekAsync(QuinielaId, WeekId);

        tracked.ChampionMemberId.Should().Be(2);
        tracked.ClosedAt.Should().Be(originalClose);
        ctx.Achievements.Verify(x => x.GrantAsync(a.UserId, It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        ctx.Achievements.Verify(x => x.GrantAsync(b.UserId, "INDISCUTIDO_DEL_COLISEO", QuinielaId, SeasonId), Times.Once);
    }

    [Fact]
    public async Task ResolveWeek_UltimaFecha_CorronaPorPuntosYOtorgaTrofeo()
    {
        var a = Member(1, "Ana", totalHits: 10);
        var b = Member(2, "Beto", totalHits: 30);
        var ctx = ClosingSetup(a, b, "DRAW", (4, 2)); // A gana la ultima: 4 pts vs 1 pt
        QuinielaDuelSeason? saved = null;
        ctx.Duels.Setup(d => d.UpsertSeasonInfoAsync(It.IsAny<QuinielaDuelSeason>()))
            .Callback<QuinielaDuelSeason>(s => saved = s).ReturnsAsync(true);

        await ctx.Sut.ResolveWeekAsync(QuinielaId, WeekId);

        saved.Should().NotBeNull();
        saved!.ChampionMemberId.Should().Be(1, "los puntos mandan sobre los aciertos de temporada");
        saved.ClosedAt.Should().NotBeNull();
        ctx.Achievements.Verify(x => x.GrantAsync(a.UserId, "INDISCUTIDO_DEL_COLISEO", QuinielaId, SeasonId), Times.Once);
        ctx.Achievements.Verify(x => x.GrantAsync(b.UserId, It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task ResolveWeek_UltimaFecha_EmpateEnTodo_DesempataPorAciertosDeTemporada()
    {
        var a = Member(1, "Ana", totalHits: 10);
        var b = Member(2, "Beto", totalHits: 30);
        var ctx = ClosingSetup(a, b, "DRAW", (3, 3)); // 2 empates: mismos puntos y victorias
        QuinielaDuelSeason? saved = null;
        ctx.Duels.Setup(d => d.UpsertSeasonInfoAsync(It.IsAny<QuinielaDuelSeason>()))
            .Callback<QuinielaDuelSeason>(s => saved = s).ReturnsAsync(true);

        await ctx.Sut.ResolveWeekAsync(QuinielaId, WeekId);

        saved!.ChampionMemberId.Should().Be(2);
        ctx.Achievements.Verify(x => x.GrantAsync(b.UserId, "INDISCUTIDO_DEL_COLISEO", QuinielaId, SeasonId), Times.Once);
    }

    [Fact]
    public async Task ResolveWeek_UltimaFecha_EmpateTotal_DesempataPorAlias()
    {
        var a = Member(1, "Zeta", totalHits: 20);
        var b = Member(2, "Alfa", totalHits: 20);
        var ctx = ClosingSetup(a, b, "DRAW", (3, 3));
        QuinielaDuelSeason? saved = null;
        ctx.Duels.Setup(d => d.UpsertSeasonInfoAsync(It.IsAny<QuinielaDuelSeason>()))
            .Callback<QuinielaDuelSeason>(s => saved = s).ReturnsAsync(true);

        await ctx.Sut.ResolveWeekAsync(QuinielaId, WeekId);

        saved!.ChampionMemberId.Should().Be(2, "Alfa va antes que Zeta");
        ctx.Achievements.Verify(x => x.GrantAsync(b.UserId, "INDISCUTIDO_DEL_COLISEO", QuinielaId, SeasonId), Times.Once);
    }

    [Fact]
    public async Task ResolveWeek_FechaIntermedia_NoCierraNiOtorga()
    {
        var ctx = new Ctx { WeekNumber = 3 }.WithMembers(2).WithWeekHits((1, 3), (2, 1));
        var fixture = Fx(1, 3, 1, 2);
        ctx.WeekFixtures = new() { fixture };
        ctx.AllFixtures = new() { fixture, Fx(2, 9, 1, 2) };

        await ctx.Sut.ResolveWeekAsync(QuinielaId, WeekId);

        ctx.Duels.Verify(d => d.UpsertSeasonInfoAsync(It.IsAny<QuinielaDuelSeason>()), Times.Never);
        ctx.Achievements.Verify(x => x.GrantAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    // ----------------------------------------------------------------- Lecturas: permisos

    [Fact]
    public async Task Lecturas_QuinielaInexistente_Falla()
    {
        var ctx = new Ctx { Quiniela = null! };

        (await ctx.Sut.GetScheduleAsync(QuinielaId, 1)).Message.Should().Be("La quiniela no existe.");
        (await ctx.Sut.GetFixturesAsync(QuinielaId, 1, null)).Message.Should().Be("La quiniela no existe.");
        (await ctx.Sut.GetStandingsAsync(QuinielaId, 1)).Message.Should().Be("La quiniela no existe.");
        (await ctx.Sut.GetMatchupAsync(QuinielaId, 1, 1)).Message.Should().Be("La quiniela no existe.");
    }

    [Fact]
    public async Task Lecturas_NoMiembro_Falla()
    {
        var ctx = new Ctx().WithUser(55, "USER");
        ctx.Uow.Setup(u => u.QuinielaMembers.GetMembershipAsync(QuinielaId, 55)).ReturnsAsync((QuinielaMember?)null);

        var schedule = await ctx.Sut.GetScheduleAsync(QuinielaId, 55);
        var fixtures = await ctx.Sut.GetFixturesAsync(QuinielaId, 55, null);
        var standings = await ctx.Sut.GetStandingsAsync(QuinielaId, 55);
        var matchup = await ctx.Sut.GetMatchupAsync(QuinielaId, 55, 1);

        foreach (var (ok, msg) in new[]
        {
            (schedule.isSuccess, schedule.Message), (fixtures.isSuccess, fixtures.Message),
            (standings.isSuccess, standings.Message), (matchup.isSuccess, matchup.Message),
        })
        {
            ok.Should().BeFalse();
            msg.Should().Be("No perteneces a esta quiniela.");
        }
    }

    [Fact]
    public async Task Lecturas_AdminDeSistema_Puede()
    {
        var ctx = new Ctx().WithUser(55, "ADMIN");

        var result = await ctx.Sut.GetFixturesAsync(QuinielaId, 55, null);

        result.isSuccess.Should().BeTrue();
    }

    // ----------------------------------------------------------------- Lecturas: contenido

    [Fact]
    public async Task GetSchedule_SinCalendario_ExitoConListasVacias()
    {
        var ctx = new Ctx().WithMembership(OwnerUserId, "MEMBER");

        var result = await ctx.Sut.GetScheduleAsync(QuinielaId, OwnerUserId);

        result.isSuccess.Should().BeTrue();
        result.Data.DuelWeeks.Should().BeEmpty();
        result.Data.RestWeeks.Should().BeEmpty();
        result.Data.GeneratedAt.Should().BeNull();
        result.Data.TotalWeeks.Should().Be(17);
    }

    [Fact]
    public async Task GetSchedule_ConCalendario_DerivaDescansosYCampeon()
    {
        var ctx = new Ctx().WithMembership(OwnerUserId, "MEMBER");
        ctx.AllFixtures = new() { Fx(1, 1, 1, 2), Fx(2, 3, 1, 3), Fx(3, 17, 2, 3) };
        ctx.SeasonInfo = new QuinielaDuelSeason
        {
            QuinielaId = QuinielaId, GeneratedAt = new DateTime(2026, 11, 5), ChampionMemberId = 2,
            ClosedAt = DateTime.UtcNow, CupName = "Copa",
        };

        var result = await ctx.Sut.GetScheduleAsync(QuinielaId, OwnerUserId);

        result.Data.DuelWeeks.Should().Equal(1, 3, 17);
        result.Data.RestWeeks.Should().HaveCount(14).And.NotContain(new[] { 1, 3, 17 });
        result.Data.Legs.Should().Be(1);
        result.Data.RoundsPerLeg.Should().Be(3);
        result.Data.IsClosed.Should().BeTrue();
        result.Data.ChampionMemberId.Should().Be(2);
        result.Data.CupName.Should().Be("Copa");
    }

    [Fact]
    public async Task GetFixtures_FiltraPorFechaYMapeaTitulo()
    {
        var ctx = new Ctx().WithMembership(OwnerUserId, "MEMBER");
        var f1 = Fx(1, 1, 1, 2);
        f1.HomeMember = Member(1, titleCode: "INDISCUTIDO_DEL_COLISEO");
        f1.AwayMember = Member(2, titleCode: "NO_EXISTE");
        ctx.AllFixtures = new() { f1, Fx(2, 2, 1, 2), Fx(3, 2, 3, null, "BYE") };

        var all = (await ctx.Sut.GetFixturesAsync(QuinielaId, OwnerUserId, null)).Data.ToList();
        var week2 = (await ctx.Sut.GetFixturesAsync(QuinielaId, OwnerUserId, 2)).Data.ToList();

        all.Should().HaveCount(3);
        week2.Should().HaveCount(2);
        all[0].Home.EquippedTitle.Should().Be("Gladiador Legendario");
        all[0].Away!.EquippedTitle.Should().BeNull();
        week2.Single(f => f.Id == 3).Away.Should().BeNull();
        all[0].IsResolved.Should().BeFalse();
    }

    [Fact]
    public async Task GetStandings_SoloResueltosYOrdenados()
    {
        var ctx = new Ctx().WithMembership(OwnerUserId, "MEMBER");
        var m1 = Member(1, "Ana", 10); var m2 = Member(2, "Beto", 20); var m3 = Member(3, "Caro", 5);
        ctx.Members = new() { m1, m2, m3 };
        DuelFixture F(int id, int w, QuinielaMember h, QuinielaMember? a, string? r)
        {
            var f = Fx(id, w, h.Id, a?.Id, r); f.HomeMember = h; f.AwayMember = a; return f;
        }
        ctx.AllFixtures = new()
        {
            F(1, 1, m1, m2, "HOME"),     // Ana 3
            F(2, 2, m3, m2, "DRAW"),     // Caro 1, Beto 1
            F(3, 3, m1, m3, null),       // pendiente: no cuenta
            F(4, 4, m3, null, "BYE"),    // descanso: no cuenta
        };

        var table = (await ctx.Sut.GetStandingsAsync(QuinielaId, OwnerUserId)).Data.ToList();

        table.Select(t => t.Participant.Alias).Should().Equal("Ana", "Beto", "Caro");
        table.Select(t => t.Rank).Should().Equal(1, 2, 3);
        table[0].Should().Match<DuelStandingDto>(t => t.Played == 1 && t.Won == 1 && t.Points == 3 && t.SeasonHits == 10);
        table[1].Should().Match<DuelStandingDto>(t => t.Played == 2 && t.Won == 0 && t.Drawn == 1 && t.Lost == 1 && t.Points == 1 && t.SeasonHits == 20);
        table[2].Should().Match<DuelStandingDto>(t => t.Played == 1 && t.Drawn == 1 && t.Points == 1);
    }

    // ----------------------------------------------------------------- Matchup

    private static Ctx MatchupCtx(DuelFixture fixture, int weekNumber = 3, string weekStatus = "LOCKED")
    {
        var ctx = new Ctx { WeekNumber = weekNumber }.WithMembership(OwnerUserId, "MEMBER");
        ctx.Duels.Setup(d => d.GetFixtureAsync(fixture.Id)).ReturnsAsync(fixture);
        ctx.Uow.Setup(u => u.Weeks.GetBySeasonIdAsync(SeasonId))
            .ReturnsAsync(new List<Week> { new() { Id = WeekId, WeekNumber = weekNumber, SeasonId = SeasonId, Status = weekStatus } });
        return ctx;
    }

    [Fact]
    public async Task GetMatchup_JornadaAbierta_NoRevelaElPickDelRival()
    {
        // Arrange: jornada PUBLISHED; quien consulta es el miembro 1 (local)
        var ctx = MatchupCtx(Fx(1, 3, 1, 2), weekStatus: "PUBLISHED");
        ctx.Uow.Setup(u => u.QuinielaMembers.GetMembershipAsync(QuinielaId, OwnerUserId))
            .ReturnsAsync(new QuinielaMember { Id = 1, UserId = OwnerUserId, QuinielaId = QuinielaId, Role = "MEMBER", Active = true });
        ctx.Uow.Setup(u => u.Matches.GetByWeekIdAsync(WeekId)).ReturnsAsync(new List<Match>
        {
            new() { Id = 1, WeekId = WeekId, StatusState = "pre", DateUtc = new DateTime(2026, 1, 1) },
        });
        ctx.WeekPicks = new()
        {
            new Pick { MemberId = 1, MatchId = 1, PickAbbr = "AME" },
            new Pick { MemberId = 2, MatchId = 1, PickAbbr = "CHV" },
        };

        // Act
        var match = (await ctx.Sut.GetMatchupAsync(QuinielaId, OwnerUserId, 1)).Data.Matches.Single();

        // Assert: ve el suyo, no el del rival, y no se delata el diferencial
        match.HomePick.Should().Be("AME");
        match.AwayPick.Should().BeNull();
        match.Differential.Should().BeFalse();
    }

    [Fact]
    public async Task GetMatchup_FixtureDeOtraQuiniela_NoEncontrado()
    {
        var fixture = Fx(1, 3, 1, 2);
        fixture.QuinielaId = 999;
        var ctx = MatchupCtx(fixture);

        var result = await ctx.Sut.GetMatchupAsync(QuinielaId, OwnerUserId, 1);

        result.isSuccess.Should().BeFalse();
        result.Message.Should().Be("Duelo no encontrado.");
    }

    [Fact]
    public async Task GetMatchup_FixtureInexistente_NoEncontrado()
    {
        var ctx = new Ctx().WithMembership(OwnerUserId, "MEMBER");
        ctx.Duels.Setup(d => d.GetFixtureAsync(77)).ReturnsAsync((DuelFixture?)null);

        var result = await ctx.Sut.GetMatchupAsync(QuinielaId, OwnerUserId, 77);

        result.Message.Should().Be("Duelo no encontrado.");
    }

    [Fact]
    public async Task GetMatchup_Bye_Falla()
    {
        var ctx = MatchupCtx(Fx(1, 3, 1, null, "BYE"));

        var result = await ctx.Sut.GetMatchupAsync(QuinielaId, OwnerUserId, 1);

        result.isSuccess.Should().BeFalse();
        result.Message.Should().Be("Ese duelo es un descanso.");
    }

    [Fact]
    public async Task GetMatchup_MarcaDiferencialesYGanadorPorPartido()
    {
        var ctx = MatchupCtx(Fx(1, 3, 1, 2));
        var teamA = new Team { Name = "America" }; var teamB = new Team { Name = "Chivas" };
        ctx.Uow.Setup(u => u.Matches.GetByWeekIdAsync(WeekId)).ReturnsAsync(new List<Match>
        {
            new() { Id = 1, WeekId = WeekId, StatusState = "post", HomeTeam = teamA, AwayTeam = teamB, DateUtc = new DateTime(2026, 1, 1) },
            new() { Id = 2, WeekId = WeekId, StatusState = "post", HomeTeam = teamA, AwayTeam = teamB, DateUtc = new DateTime(2026, 1, 2) },
            new() { Id = 3, WeekId = WeekId, StatusState = "in", HomeTeam = teamA, AwayTeam = teamB, DateUtc = new DateTime(2026, 1, 3) },
        });
        ctx.WeekPicks = new()
        {
            new Pick { MemberId = 1, MatchId = 1, PickAbbr = "AME", IsHit = true },
            new Pick { MemberId = 2, MatchId = 1, PickAbbr = "CHV", IsHit = false },   // diferencial, gana local
            new Pick { MemberId = 1, MatchId = 2, PickAbbr = "AME", IsHit = true },
            new Pick { MemberId = 2, MatchId = 2, PickAbbr = "AME", IsHit = true },    // igual, nadie gana
            new Pick { MemberId = 1, MatchId = 3, PickAbbr = "AME" },
            new Pick { MemberId = 2, MatchId = 3, PickAbbr = "CHV" },                  // diferencial, en juego
        };

        var data = (await ctx.Sut.GetMatchupAsync(QuinielaId, OwnerUserId, 1)).Data;

        data.Matches.Should().HaveCount(3);
        data.Matches[0].Differential.Should().BeTrue();
        data.Matches[0].Winner.Should().Be("HOME");
        data.Matches[0].IsFinished.Should().BeTrue();
        data.Matches[0].HomeTeam.Should().Be("America");
        data.Matches[1].Differential.Should().BeFalse();
        data.Matches[1].Winner.Should().BeNull();
        data.Matches[2].Differential.Should().BeTrue();
        data.Matches[2].Winner.Should().BeNull();
        data.Matches[2].IsFinished.Should().BeFalse();
        data.Fixture.HomeHits.Should().Be(2);
        data.Fixture.AwayHits.Should().Be(1);
    }

    [Theory]
    [InlineData(1, 4, "AWAY")]
    [InlineData(5, 2, "HOME")]
    [InlineData(2, 4, null)]
    public async Task GetMatchup_VentajaDelRetadorEnVivo(int home, int away, string? expected)
    {
        var ctx = MatchupCtx(Fx(1, 3, home, away)).WithMembers(6).WithPriorRanks(6);

        var result = await ctx.Sut.GetMatchupAsync(QuinielaId, OwnerUserId, 1);

        result.isSuccess.Should().BeTrue();
        result.Data.ChallengerAdvantage.Should().Be(expected);
    }

    private static Ctx HeatCtx(int fixtureWeek, bool gamification)
    {
        var ctx = MatchupCtx(Fx(1, fixtureWeek, 1, 2), weekNumber: fixtureWeek).WithMembers(2);
        ctx.Quiniela.GamificationEnabled = gamification;
        var team = new Team { Name = "T" };
        var matches = Enumerable.Range(1, 4).Select(i => new Match
        {
            Id = i, WeekId = WeekId, StatusState = "post", WinnerAbbr = "AME", HomeTeam = team, AwayTeam = team,
            DateUtc = new DateTime(2026, 1, i),
        }).ToList();
        ctx.Uow.Setup(u => u.Matches.GetByWeekIdAsync(WeekId)).ReturnsAsync(matches);
        ctx.WeekPicks = matches.SelectMany(m => new[]
        {
            new Pick { MemberId = 1, MatchId = m.Id, PickAbbr = "AME", IsHit = true },
            new Pick { MemberId = 2, MatchId = m.Id, PickAbbr = "CHV", IsHit = false },
        }).ToList();
        return ctx;
    }

    [Fact]
    public async Task GetMatchup_Gamificacion_LocalConCuatroAciertosFinales_EsFire()
    {
        var ctx = HeatCtx(3, gamification: true);

        var data = (await ctx.Sut.GetMatchupAsync(QuinielaId, OwnerUserId, 1)).Data;

        data.Fixture.Home.Heat.Should().Be("FIRE");
    }

    [Fact]
    public async Task GetMatchup_Gamificacion_VisitanteConCuatroFallosFinales_EsIce()
    {
        var ctx = HeatCtx(3, gamification: true);

        var data = (await ctx.Sut.GetMatchupAsync(QuinielaId, OwnerUserId, 1)).Data;

        data.Fixture.Away!.Heat.Should().Be("ICE");
    }

    [Fact]
    public async Task GetMatchup_GamificacionApagada_HeatEsNull()
    {
        var ctx = HeatCtx(3, gamification: false);

        var data = (await ctx.Sut.GetMatchupAsync(QuinielaId, OwnerUserId, 1)).Data;

        data.Fixture.Home.Heat.Should().BeNull();
        data.Fixture.Away!.Heat.Should().BeNull();
    }

    [Fact]
    public async Task GetMatchup_Liguilla_HeatEsNull()
    {
        var ctx = HeatCtx(18, gamification: true); // WeeksCount = 17: la 18 es Liguilla

        var data = (await ctx.Sut.GetMatchupAsync(QuinielaId, OwnerUserId, 1)).Data;

        data.Fixture.Home.Heat.Should().BeNull();
        data.Fixture.Away!.Heat.Should().BeNull();
    }

    [Fact]
    public async Task GetMatchup_JornadaUno_SinVentaja()
    {
        var ctx = MatchupCtx(Fx(1, 1, 1, 6), weekNumber: 1).WithMembers(6).WithPriorRanks(6);

        var result = await ctx.Sut.GetMatchupAsync(QuinielaId, OwnerUserId, 1);

        result.Data.ChallengerAdvantage.Should().BeNull();
    }

    // ----------------------------------------------------------------- Salon de la Fama y copa

    private static QuinielaDuelSeason ClosedInfo(QuinielaMember champion) => new()
    {
        QuinielaId = QuinielaId,
        ChampionMemberId = champion.Id,
        ChampionMember = champion,
        ClosedAt = DateTime.UtcNow,
        CupName = "Copa X",
        PunishmentText = "Castigo",
        Quiniela = new Quiniela { Id = QuinielaId, Name = "Liga", SeasonId = SeasonId, Season = new Season { Id = SeasonId, Name = "Apertura 2026" } },
    };

    [Fact]
    public async Task HallOfFame_ArmaEntradaConRecordYCanEditCup()
    {
        var champion = Member(1, "Ana");
        var ctx = new Ctx();
        ctx.Uow.Setup(u => u.Quinielas.GetUserQuinielasAsync(champion.UserId))
            .ReturnsAsync(new List<Quiniela> { new() { Id = QuinielaId } });
        ctx.Duels.Setup(d => d.GetClosedSeasonInfosAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync(new List<QuinielaDuelSeason> { ClosedInfo(champion) });
        ctx.AllFixtures = new()
        {
            Fx(1, 1, 1, 2, "HOME"), Fx(2, 2, 3, 1, "AWAY"), Fx(3, 3, 1, 2, "DRAW"),
            Fx(4, 4, 2, 1, "AWAY"), Fx(5, 5, 1, 3, "AWAY"), Fx(6, 6, 1, null, "BYE"),
        };

        var asChampion = (await ctx.Sut.GetHallOfFameAsync(champion.UserId)).Data.Single();

        asChampion.Record.Should().Be("3-1-1");
        asChampion.Points.Should().Be(10);
        asChampion.CanEditCup.Should().BeTrue();
        asChampion.SeasonName.Should().Be("Apertura 2026");
        asChampion.QuinielaName.Should().Be("Liga");
        asChampion.CupName.Should().Be("Copa X");
        asChampion.PunishmentText.Should().Be("Castigo");
        asChampion.Title.Should().Be("Gladiador Legendario");
        asChampion.Champion.MemberId.Should().Be(1);
    }

    [Fact]
    public async Task HallOfFame_OtroUsuario_NoPuedeEditarCopa()
    {
        var champion = Member(1, "Ana");
        var ctx = new Ctx();
        ctx.Uow.Setup(u => u.Quinielas.GetUserQuinielasAsync(555))
            .ReturnsAsync(new List<Quiniela> { new() { Id = QuinielaId } });
        ctx.Duels.Setup(d => d.GetClosedSeasonInfosAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync(new List<QuinielaDuelSeason> { ClosedInfo(champion) });

        var entry = (await ctx.Sut.GetHallOfFameAsync(555)).Data.Single();

        entry.CanEditCup.Should().BeFalse();
    }

    [Fact]
    public async Task HallOfFame_SinQuinielas_EsVacio()
    {
        var ctx = new Ctx();
        ctx.Uow.Setup(u => u.Quinielas.GetUserQuinielasAsync(9)).ReturnsAsync(new List<Quiniela>());

        var result = await ctx.Sut.GetHallOfFameAsync(9);

        result.isSuccess.Should().BeTrue();
        result.Data.Should().BeEmpty();
    }

    private static Ctx CupCtx(QuinielaDuelSeason? closed)
    {
        var ctx = new Ctx();
        ctx.Duels.Setup(d => d.GetClosedSeasonInfosAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync(closed == null ? new List<QuinielaDuelSeason>() : new List<QuinielaDuelSeason> { closed });
        return ctx;
    }

    [Fact]
    public async Task UpdateCup_NoEsCampeon_Falla()
    {
        var closed = ClosedInfo(Member(1, "Ana"));
        var ctx = CupCtx(closed);

        var result = await ctx.Sut.UpdateCupAsync(QuinielaId, 555, new UpdateDuelCupDto { CupName = "Mia" });

        result.isSuccess.Should().BeFalse();
        result.Message.Should().Be("Solo el campeón de duelos puede nombrar la copa.");
        ctx.Duels.Verify(d => d.UpsertSeasonInfoAsync(It.IsAny<QuinielaDuelSeason>()), Times.Never);
    }

    [Fact]
    public async Task UpdateCup_TemporadaAbierta_MismoMensaje()
    {
        var ctx = CupCtx(null);

        var result = await ctx.Sut.UpdateCupAsync(QuinielaId, OwnerUserId + 1, new UpdateDuelCupDto { CupName = "Mia" });

        result.isSuccess.Should().BeFalse();
        result.Message.Should().Be("Solo el campeón de duelos puede nombrar la copa.");
    }

    [Fact]
    public async Task UpdateCup_NombreLargo_Falla()
    {
        var champion = Member(1);
        var ctx = CupCtx(ClosedInfo(champion));

        var result = await ctx.Sut.UpdateCupAsync(QuinielaId, champion.UserId, new UpdateDuelCupDto { CupName = new string('a', 61) });

        result.isSuccess.Should().BeFalse();
        result.Message.Should().Be("El nombre de la copa no puede pasar de 60 caracteres.");
    }

    [Fact]
    public async Task UpdateCup_CastigoLargo_Falla()
    {
        var champion = Member(1);
        var ctx = CupCtx(ClosedInfo(champion));

        var result = await ctx.Sut.UpdateCupAsync(QuinielaId, champion.UserId, new UpdateDuelCupDto { PunishmentText = new string('a', 201) });

        result.isSuccess.Should().BeFalse();
        result.Message.Should().Be("El castigo no puede pasar de 200 caracteres.");
    }

    [Fact]
    public async Task UpdateCup_LimitesExactos_Aceptados()
    {
        var champion = Member(1);
        var ctx = CupCtx(ClosedInfo(champion));

        var result = await ctx.Sut.UpdateCupAsync(QuinielaId, champion.UserId,
            new UpdateDuelCupDto { CupName = new string('a', 60), PunishmentText = new string('b', 200) });

        result.isSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateCup_Campeon_RecortaVaciaANullYGuarda()
    {
        var champion = Member(1);
        var ctx = CupCtx(ClosedInfo(champion));
        var tracked = new QuinielaDuelSeason { QuinielaId = QuinielaId, ChampionMemberId = 1, ClosedAt = DateTime.UtcNow, CupName = "Vieja" };
        ctx.SeasonInfo = tracked;

        var result = await ctx.Sut.UpdateCupAsync(QuinielaId, champion.UserId,
            new UpdateDuelCupDto { CupName = "  Copa del Coliseo  ", PunishmentText = "   " });

        result.isSuccess.Should().BeTrue();
        tracked.CupName.Should().Be("Copa del Coliseo");
        tracked.PunishmentText.Should().BeNull();
        ctx.Duels.Verify(d => d.UpsertSeasonInfoAsync(tracked), Times.Once);
        result.Data.CupName.Should().Be("Copa del Coliseo");
        result.Data.PunishmentText.Should().BeNull();
        result.Data.CanEditCup.Should().BeTrue();
    }
}
