using FluentAssertions;
using UseCases.Duels;

namespace PickSportsApi.UnitTests.UseCasesTests;

public class RoundRobinSchedulerTests
{
    private static List<int> Ids(int n) => Enumerable.Range(101, n).ToList();

    private static string Pair(int a, int b) => a < b ? $"{a}-{b}" : $"{b}-{a}";

    [Fact]
    public void Generate_Nueve_17Semanas_UnaVueltaConDescansosRepartidos()
    {
        var s = RoundRobinScheduler.Generate(Ids(9), 17, 1);

        s.Legs.Should().Be(1);
        s.RoundsPerLeg.Should().Be(9);
        s.DuelWeeks.Should().Equal(1, 3, 5, 7, 10, 12, 14, 16, 17);
        s.RestWeeks.Should().Equal(2, 4, 6, 8, 9, 11, 13, 15);
    }

    [Fact]
    public void Generate_Cuatro_17Semanas_CincoVueltasYDosDescansos()
    {
        var s = RoundRobinScheduler.Generate(Ids(4), 17, 7);

        s.Legs.Should().Be(5);
        s.RoundsPerLeg.Should().Be(3);
        s.DuelWeeks.Should().HaveCount(15);
        s.RestWeeks.Should().HaveCount(2);
        s.DuelWeeks.Should().Contain(17);
        s.RestWeeks.Should().NotContain(17);

        var ids = Ids(4);
        var counts = s.Fixtures.GroupBy(f => Pair(f.HomeMemberId, f.AwayMemberId!.Value)).ToDictionary(g => g.Key, g => g.Count());
        counts.Should().HaveCount(6);
        counts.Values.Should().OnlyContain(c => c == 5);
    }

    [Fact]
    public void Generate_Dieciocho_17Semanas_UnaVueltaSinDescansos()
    {
        var s = RoundRobinScheduler.Generate(Ids(18), 17, 3);

        s.Legs.Should().Be(1);
        s.RoundsPerLeg.Should().Be(17);
        s.RestWeeks.Should().BeEmpty();
        s.DuelWeeks.Should().Equal(Enumerable.Range(1, 17));
    }

    [Fact]
    public void Generate_Diecinueve_17Semanas_SoloPrimerasRondasSinDescansos()
    {
        var s = RoundRobinScheduler.Generate(Ids(19), 17, 3);

        s.Legs.Should().Be(1);
        s.RoundsPerLeg.Should().Be(19);
        s.RestWeeks.Should().BeEmpty();
        s.DuelWeeks.Should().Equal(Enumerable.Range(1, 17));
        s.Fixtures.Max(f => f.Round).Should().Be(17);
    }

    [Fact]
    public void Generate_Dos_17Semanas_DiecisieteVueltasConLocaliaAlterna()
    {
        var s = RoundRobinScheduler.Generate(Ids(2), 17, 5);

        s.RoundsPerLeg.Should().Be(1);
        s.Legs.Should().Be(17);
        s.RestWeeks.Should().BeEmpty();
        s.Fixtures.Should().HaveCount(17);
        for (int i = 1; i < s.Fixtures.Count; i++)
            s.Fixtures[i].HomeMemberId.Should().NotBe(s.Fixtures[i - 1].HomeMemberId);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(10)]
    public void Generate_Par_NoHayByes_YCadaParJuegaUnaVezPorVuelta(int n)
    {
        var s = RoundRobinScheduler.Generate(Ids(n), 30, 11);

        s.Fixtures.Should().OnlyContain(f => f.AwayMemberId != null && f.Result == null);
        foreach (var leg in s.Fixtures.GroupBy(f => f.Leg))
        {
            var pairs = leg.Select(f => Pair(f.HomeMemberId, f.AwayMemberId!.Value)).ToList();
            pairs.Should().OnlyHaveUniqueItems();
            pairs.Should().HaveCount(n * (n - 1) / 2);
        }
    }

    [Theory]
    [InlineData(5)]
    [InlineData(9)]
    public void Generate_Impar_CadaMiembroTieneUnByePorVueltaCompleta(int n)
    {
        var ids = Ids(n);
        var s = RoundRobinScheduler.Generate(ids, 40, 2);

        s.Legs.Should().BeGreaterThan(1);
        foreach (var leg in s.Fixtures.GroupBy(f => f.Leg))
        {
            var byes = leg.Where(f => f.AwayMemberId == null).ToList();
            byes.Should().OnlyContain(f => f.Result == "BYE" && f.HomeHits == 0 && f.AwayHits == 0);
            byes.Select(f => f.HomeMemberId).Should().BeEquivalentTo(ids);
        }
    }

    [Theory]
    [InlineData(4, 17)]
    [InlineData(9, 17)]
    [InlineData(7, 20)]
    [InlineData(19, 17)]
    public void Generate_CadaMiembroJuegaUnaVezPorFechaDeDuelo(int n, int weeks)
    {
        var ids = Ids(n);
        var s = RoundRobinScheduler.Generate(ids, weeks, 9);

        foreach (var week in s.Fixtures.GroupBy(f => f.WeekNumber))
        {
            var participants = week.SelectMany(f => f.AwayMemberId == null
                ? new[] { f.HomeMemberId }
                : new[] { f.HomeMemberId, f.AwayMemberId.Value }).ToList();
            participants.Should().BeEquivalentTo(ids);
        }
        s.Fixtures.Select(f => f.WeekNumber).Distinct().Should().BeEquivalentTo(s.DuelWeeks);
    }

    [Fact]
    public void Generate_NadieEnfrentaDosVecesAlMismoRivalDentroDeUnaVuelta()
    {
        var s = RoundRobinScheduler.Generate(Ids(9), 17, 4);

        var pairs = s.Fixtures.Where(f => f.AwayMemberId != null)
            .Select(f => Pair(f.HomeMemberId, f.AwayMemberId!.Value)).ToList();
        pairs.Should().OnlyHaveUniqueItems();
        pairs.Should().HaveCount(36);
    }

    [Fact]
    public void Generate_VueltaDos_InvierteLaLocaliaDeLaVueltaUno()
    {
        var s = RoundRobinScheduler.Generate(Ids(4), 17, 6);

        var leg1 = s.Fixtures.Where(f => f.Leg == 1).ToList();
        var leg2 = s.Fixtures.Where(f => f.Leg == 2).ToList();
        leg2.Should().HaveCount(leg1.Count);
        for (int i = 0; i < leg1.Count; i++)
        {
            leg2[i].Round.Should().Be(leg1[i].Round);
            leg2[i].HomeMemberId.Should().Be(leg1[i].AwayMemberId!.Value);
            leg2[i].AwayMemberId.Should().Be(leg1[i].HomeMemberId);
        }
    }

    [Fact]
    public void Generate_MismasEntradasYSemilla_MismoCalendario()
    {
        var a = RoundRobinScheduler.Generate(Ids(9), 17, 42);
        var b = RoundRobinScheduler.Generate(Ids(9), 17, 42);

        a.Fixtures.Select(f => (f.WeekNumber, f.Round, f.Leg, f.HomeMemberId, f.AwayMemberId))
            .Should().Equal(b.Fixtures.Select(f => (f.WeekNumber, f.Round, f.Leg, f.HomeMemberId, f.AwayMemberId)));
    }

    [Fact]
    public void Generate_SemillasDistintas_PuedenDiferir()
    {
        var calendars = Enumerable.Range(1, 10)
            .Select(seed => string.Join("|", RoundRobinScheduler.Generate(Ids(8), 17, seed).Fixtures
                .Select(f => $"{f.WeekNumber}:{f.HomeMemberId}:{f.AwayMemberId}")))
            .Distinct().Count();

        calendars.Should().BeGreaterThan(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Generate_MenosDeDosMiembros_LanzaArgumentException(int n)
    {
        var act = () => RoundRobinScheduler.Generate(Ids(n), 17, 1);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Generate_Barrido_LaUltimaFechaNuncaEsDescansoYTodoCuadra()
    {
        for (int weeks = 10; weeks <= 20; weeks++)
        for (int n = 2; n <= 24; n++)
        {
            var s = RoundRobinScheduler.Generate(Ids(n), weeks, n * 31 + weeks);

            s.RestWeeks.Should().NotContain(weeks, $"n={n}, weeks={weeks}");
            s.DuelWeeks.Should().Contain(weeks, $"n={n}, weeks={weeks}");
            (s.DuelWeeks.Count + s.RestWeeks.Count).Should().Be(weeks, $"n={n}, weeks={weeks}");
            s.DuelWeeks.Should().BeInAscendingOrder();
            s.RestWeeks.Should().BeInAscendingOrder();
            s.RestWeeks.Should().OnlyHaveUniqueItems();
            s.Fixtures.Select(f => f.WeekNumber).Should().BeInAscendingOrder();
        }
    }
}
