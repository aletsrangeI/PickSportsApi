using Domain.Entities;
using FluentAssertions;
using UseCases.Liguilla;
using Xunit;

namespace PickSportsApi.UnitTests.UseCasesTests;

public class FmfLiguillaEngineTests
{
    [Fact]
    public void CalculateFmfReSeed_Standard1234_Semi1Es1vs4_Semi2Es2vs3()
    {
        // Arrange: Avanzaron los favoritos 1, 2, 3 y 4
        var winners = new List<TeamSeedInfo>
        {
            new(TeamId: 101, Seed: 1), // Cruz Azul
            new(TeamId: 102, Seed: 2), // Toluca
            new(TeamId: 103, Seed: 3), // Tigres
            new(TeamId: 104, Seed: 4)  // Monterrey
        };

        // Act
        var (semi1, semi2) = FmfLiguillaEngine.CalculateFmfReSeed(winners);

        // Assert:
        // Semifinal 1: 1° vs 4°
        semi1.TeamA.Seed.Should().Be(1);
        semi1.TeamA.TeamId.Should().Be(101);
        semi1.TeamB.Seed.Should().Be(4);
        semi1.TeamB.TeamId.Should().Be(104);

        // Semifinal 2: 2° vs 3°
        semi2.TeamA.Seed.Should().Be(2);
        semi2.TeamA.TeamId.Should().Be(102);
        semi2.TeamB.Seed.Should().Be(3);
        semi2.TeamB.TeamId.Should().Be(103);
    }

    [Fact]
    public void CalculateFmfReSeed_UpsetWhere8Beats1_Semi1Es2vs8_Semi2Es3vs4()
    {
        // Arrange: El 8° dio la sorpresa y eliminó al 1°. Avanzaron 8, 2, 3, 4.
        var winners = new List<TeamSeedInfo>
        {
            new(TeamId: 108, Seed: 8), // Atlas
            new(TeamId: 102, Seed: 2), // Toluca
            new(TeamId: 103, Seed: 3), // Tigres
            new(TeamId: 104, Seed: 4)  // Monterrey
        };

        // Act
        var (semi1, semi2) = FmfLiguillaEngine.CalculateFmfReSeed(winners);

        // Assert:
        // En semifinales se reubican 1 al 4:
        // El mejor vivo es el 2° Toluca. El peor vivo es el 8° Atlas.
        // Semifinal 1: 2° vs 8°
        semi1.TeamA.Seed.Should().Be(2);
        semi1.TeamA.TeamId.Should().Be(102);
        semi1.TeamB.Seed.Should().Be(8);
        semi1.TeamB.TeamId.Should().Be(108);

        // Semifinal 2: 3° vs 4°
        semi2.TeamA.Seed.Should().Be(3);
        semi2.TeamA.TeamId.Should().Be(103);
        semi2.TeamB.Seed.Should().Be(4);
        semi2.TeamB.TeamId.Should().Be(104);
    }

    [Fact]
    public void CalculateFmfReSeed_WhenNot4Winners_ThrowsArgumentException()
    {
        var winners = new List<TeamSeedInfo>
        {
            new(TeamId: 101, Seed: 1),
            new(TeamId: 102, Seed: 2)
        };

        var act = () => FmfLiguillaEngine.CalculateFmfReSeed(winners);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*exactamente 4 ganadores*");
    }

    [Fact]
    public void EvaluateBracket_CalculatesPoints_AndDetectsShattered()
    {
        // Arrange: Usuario seleccionó
        // QF: 101, 102, 103, 104
        // SF: 101, 102
        // Champ: 101
        var bracket = new LiguillaBracket
        {
            QfWinner1TeamId = 101,
            QfWinner2TeamId = 102,
            QfWinner3TeamId = 103,
            QfWinner4TeamId = 104,
            SfWinner1TeamId = 101,
            SfWinner2TeamId = 102,
            ChampionTeamId = 101
        };

        // Nodos resueltos:
        // Node 1: 101 vs 108 -> Ganó 101 (Acierto QF)
        // Node 2: 102 vs 107 -> Ganó 107 (Fallo QF, 102 eliminado)
        // Node 3: 103 vs 106 -> Ganó 103 (Acierto QF)
        // Node 4: 104 vs 105 -> Ganó 104 (Acierto QF)
        // Node 5 (SF): 101 vs 107 -> Ganó 101 (Acierto SF)
        // Node 6 (SF): 103 vs 104 -> Ganó 103 (Fallo SF, usuario tenía 102)
        // Node 7 (Final): 101 vs 103 -> Ganó 101 (Acierto Campeón)
        var nodes = new List<LiguillaNode>
        {
            new() { NodeNumber = 1, TeamAId = 101, TeamBId = 108, WinnerTeamId = 101, IsResolved = true },
            new() { NodeNumber = 2, TeamAId = 102, TeamBId = 107, WinnerTeamId = 107, IsResolved = true },
            new() { NodeNumber = 3, TeamAId = 103, TeamBId = 106, WinnerTeamId = 103, IsResolved = true },
            new() { NodeNumber = 4, TeamAId = 104, TeamBId = 105, WinnerTeamId = 104, IsResolved = true },
            new() { NodeNumber = 5, TeamAId = 101, TeamBId = 107, WinnerTeamId = 101, IsResolved = true },
            new() { NodeNumber = 6, TeamAId = 103, TeamBId = 104, WinnerTeamId = 103, IsResolved = true },
            new() { NodeNumber = 7, TeamAId = 101, TeamBId = 103, WinnerTeamId = 101, IsResolved = true }
        };

        // Act
        var (qfPts, sfPts, champPts, bonusPts, totalPts, shattered) = FmfLiguillaEngine.EvaluateBracket(bracket, nodes);

        // Assert:
        // QF: 3 aciertos (101, 103, 104) * 10 = 30 pts
        qfPts.Should().Be(30);
        // SF: 1 acierto (101) * 25 = 25 pts
        sfPts.Should().Be(25);
        // Campeón: 101 * 50 = 50 pts
        champPts.Should().Be(50);
        // Bono: no perfecto (falló QF2 y SF2) -> 0 pts
        bonusPts.Should().Be(0);
        totalPts.Should().Be(105);

        // Shattered: 102 fue eliminado en QF2 y estaba en el bracket del usuario
        shattered.Should().Contain(102);
    }

    [Fact]
    public void EvaluateBracket_Awards20PtsBonusForPerfect7Hits()
    {
        var bracket = new LiguillaBracket
        {
            QfWinner1TeamId = 101,
            QfWinner2TeamId = 102,
            QfWinner3TeamId = 103,
            QfWinner4TeamId = 104,
            SfWinner1TeamId = 101,
            SfWinner2TeamId = 102,
            ChampionTeamId = 101
        };

        var nodes = new List<LiguillaNode>
        {
            new() { NodeNumber = 1, WinnerTeamId = 101, IsResolved = true },
            new() { NodeNumber = 2, WinnerTeamId = 102, IsResolved = true },
            new() { NodeNumber = 3, WinnerTeamId = 103, IsResolved = true },
            new() { NodeNumber = 4, WinnerTeamId = 104, IsResolved = true },
            new() { NodeNumber = 5, WinnerTeamId = 101, IsResolved = true },
            new() { NodeNumber = 6, WinnerTeamId = 102, IsResolved = true },
            new() { NodeNumber = 7, WinnerTeamId = 101, IsResolved = true }
        };

        var (qfPts, sfPts, champPts, bonusPts, totalPts, _) = FmfLiguillaEngine.EvaluateBracket(bracket, nodes);

        qfPts.Should().Be(40);
        sfPts.Should().Be(50);
        champPts.Should().Be(50);
        bonusPts.Should().Be(20);
        totalPts.Should().Be(160); // 40 + 50 + 50 + 20 = 160
    }
}
