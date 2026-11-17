using Domain.Entities;
using FluentAssertions;
using Interface.UseCases.Knockout;
using UseCases.Liguilla.Strategies;
using Xunit;

namespace PickSportsApi.UnitTests.UseCasesTests.Knockout;

public class FmfLiguillaStrategyTests
{
    private readonly FmfLiguillaStrategy _sut = new();

    private static Dictionary<string, IReadOnlyList<int>> Picks(int[] qf, int[] sf, int[] final) => new()
    {
        ["QF"] = qf.ToList(),
        ["SF"] = sf.ToList(),
        ["FINAL"] = final.ToList()
    };

    private static List<LiguillaNode> QuarterFinals(bool withTeams = true) =>
        Enumerable.Range(1, 4).Select(i => new LiguillaNode
        {
            Id = i, Round = "QF", NodeNumber = i,
            TeamAId = withTeams ? i : null, TeamBId = withTeams ? 9 - i : null,
            SeedA = i, SeedB = 9 - i
        }).ToList();

    [Fact]
    public void Definition_MatchesFmfRules()
    {
        // Assert
        _sut.FormatCode.Should().Be("FMF_LIGUILLA");
        _sut.FirstPickableRoundCode.Should().Be("QF");
        _sut.PerfectBonusPoints.Should().Be(20);
        _sut.Rounds.Select(r => (r.Code, r.WinnersToPick, r.NodeCount, r.PointsPerHit))
            .Should().Equal(("QF", 4, 4, 10), ("SF", 2, 2, 25), ("FINAL", 1, 1, 50));
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    public void IsFieldReady_RequiresRegularSeasonAndFourCompleteQuarterFinals(bool regularFinished, bool withTeams, bool expected)
    {
        // Arrange
        var nodes = QuarterFinals(withTeams);

        // Act
        var result = _sut.IsFieldReady(nodes, regularFinished);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ValidatePicks_ValidBracket_DoesNotThrow()
    {
        // Act
        var act = () => _sut.ValidatePicks(new List<LiguillaNode>(), Picks(new[] { 1, 2, 3, 4 }, new[] { 1, 2 }, new[] { 1 }));

        // Assert
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(new[] { 1, 2, 3, 3 }, new[] { 1, 2 }, new[] { 1 }, "Debes elegir 4 ganadores distintos en Cuartos de Final.")]
    [InlineData(new[] { 1, 2, 3, 4 }, new[] { 1, 9 }, new[] { 1 }, "Los finalistas de Semifinales deben haber sido seleccionados como ganadores en Cuartos de Final.")]
    [InlineData(new[] { 1, 2, 3, 4 }, new[] { 1, 1 }, new[] { 1 }, "Los dos finalistas deben ser equipos distintos.")]
    [InlineData(new[] { 1, 2, 3, 4 }, new[] { 1, 2 }, new[] { 3 }, "El Campeón debe ser uno de los dos equipos finalistas seleccionados.")]
    public void ValidatePicks_Incoherent_ThrowsArgumentExceptionWithExactMessage(int[] qf, int[] sf, int[] final, string message)
    {
        // Act
        var act = () => _sut.ValidatePicks(new List<LiguillaNode>(), Picks(qf, sf, final));

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage(message);
    }

    [Fact]
    public void EligibleTeamIds_FollowsPreviousRoundPicks()
    {
        // Arrange
        var nodes = QuarterFinals();
        var previous = Picks(new[] { 1, 2, 3, 4 }, new[] { 1, 2 }, Array.Empty<int>());

        // Act / Assert
        _sut.EligibleTeamIds("QF", nodes, previous).Should().HaveCount(8);
        _sut.EligibleTeamIds("SF", nodes, previous).Should().Equal(1, 2, 3, 4);
        _sut.EligibleTeamIds("FINAL", nodes, previous).Should().Equal(1, 2);
    }

    [Fact]
    public void AdvanceWinners_WhenQuarterFinalsResolved_ReSeedsSemifinalsByArticle10()
    {
        // Arrange: ganan 8 (seed 8), 2, 3, 4 -> por siembra 2,3,4,8: semi 2 vs 8 y semi 3 vs 4
        var nodes = QuarterFinals();
        nodes[0].WinnerTeamId = 8; // nodo 1: equipo 1 (seed 1) vs 8 (seed 8) -> gana 8
        nodes[1].WinnerTeamId = 2;
        nodes[2].WinnerTeamId = 3;
        nodes[3].WinnerTeamId = 4;
        nodes.ForEach(n => n.IsResolved = true);
        var sf1 = new LiguillaNode { Id = 5, Round = "SF", NodeNumber = 5 };
        var sf2 = new LiguillaNode { Id = 6, Round = "SF", NodeNumber = 6 };
        nodes.AddRange(new[] { sf1, sf2 });

        // Act
        var modified = _sut.AdvanceWinners(nodes);

        // Assert
        modified.Should().BeEquivalentTo(new[] { sf1, sf2 });
        sf1.TeamAId.Should().Be(2);
        sf1.TeamBId.Should().Be(8);
        sf1.SeedB.Should().Be(8);
        sf2.TeamAId.Should().Be(3);
        sf2.TeamBId.Should().Be(4);
    }

    [Fact]
    public void AdvanceWinners_WhenSemifinalsResolved_SeedsFinal()
    {
        // Arrange
        var sf1 = new LiguillaNode { Round = "SF", NodeNumber = 5, WinnerTeamId = 11, IsResolved = true };
        var sf2 = new LiguillaNode { Round = "SF", NodeNumber = 6, WinnerTeamId = 22, IsResolved = true };
        var final = new LiguillaNode { Round = "FINAL", NodeNumber = 7 };

        // Act
        var modified = _sut.AdvanceWinners(new List<LiguillaNode> { sf1, sf2, final });

        // Assert
        modified.Should().ContainSingle().Which.Should().BeSameAs(final);
        final.TeamAId.Should().Be(11);
        final.TeamBId.Should().Be(22);
    }

    [Fact]
    public void AdvanceWinners_WhenNothingResolved_ReturnsEmpty()
    {
        // Act
        var modified = _sut.AdvanceWinners(QuarterFinals());

        // Assert
        modified.Should().BeEmpty();
    }

    [Theory]
    [InlineData("A1", true, 1)]
    [InlineData("B1", true, 2)]
    [InlineData("ZZ", true, null)]
    [InlineData("A1", false, null)]
    public void ResolveFromEspn_MapsWinnerEspnIdToNodeTeam(string winnerEspnId, bool completed, int? expectedTeamId)
    {
        // Arrange
        var node = new LiguillaNode
        {
            NodeNumber = 1, TeamAId = 1, TeamBId = 2,
            TeamA = new Team { Id = 1, EspnTeamId = "A1" },
            TeamB = new Team { Id = 2, EspnTeamId = "B1" }
        };
        var tie = new EspnKnockoutTie { SeriesCompleted = completed, WinnerEspnTeamId = winnerEspnId };

        // Act
        var outcome = _sut.ResolveFromEspn(node, tie);

        // Assert
        outcome.IsResolved.Should().Be(expectedTeamId.HasValue);
        outcome.WinnerTeamId.Should().Be(expectedTeamId);
        outcome.NodeNumber.Should().Be(1);
    }
}
