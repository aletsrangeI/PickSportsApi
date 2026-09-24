using UseCases.Espn;

namespace PickSportsApi.UnitTests.UseCasesTests;

public class EspnScoreboardParserTests
{
    private readonly EspnScoreboardParser _parser = new();

    // ─── JSON fixtures ────────────────────────────────────────────────────────

    private const string PostponedJson = """
    {
      "events": [
        {
          "id": "600001",
          "date": "2026-03-15T02:00:00Z",
          "week": { "number": 10 },
          "competitions": [
            {
              "status": {
                "type": {
                  "name": "STATUS_POSTPONED",
                  "state": "pre",
                  "detail": "Postponed"
                }
              },
              "venue": { "fullName": "Estadio Azteca", "address": { "city": "Ciudad de México" } },
              "competitors": [
                {
                  "homeAway": "home", "score": "0", "winner": false,
                  "team": { "id": "10", "name": "América", "abbreviation": "AME", "displayName": "Club América" }
                },
                {
                  "homeAway": "away", "score": "0", "winner": false,
                  "team": { "id": "11", "name": "Chivas", "abbreviation": "GDL", "displayName": "Chivas Guadalajara" }
                }
              ]
            }
          ]
        }
      ]
    }
    """;

    private const string FinalJson = """
    {
      "events": [
        {
          "id": "600002",
          "date": "2026-03-22T02:00:00Z",
          "week": { "number": 11 },
          "competitions": [
            {
              "status": {
                "type": { "name": "STATUS_FINAL", "state": "post", "detail": "Final" }
              },
              "competitors": [
                {
                  "homeAway": "home", "score": "2", "winner": true,
                  "team": { "id": "10", "name": "América", "abbreviation": "AME", "displayName": "Club América" }
                },
                {
                  "homeAway": "away", "score": "1", "winner": false,
                  "team": { "id": "11", "name": "Chivas", "abbreviation": "GDL", "displayName": "Chivas Guadalajara" }
                }
              ]
            }
          ]
        }
      ]
    }
    """;

    private const string TieJson = """
    {
      "events": [
        {
          "id": "600003",
          "date": "2026-03-22T02:00:00Z",
          "competitions": [
            {
              "status": {
                "type": { "name": "STATUS_FINAL", "state": "post", "detail": "Final" }
              },
              "competitors": [
                {
                  "homeAway": "home", "score": "1", "winner": false,
                  "team": { "id": "10", "name": "A", "abbreviation": "AAA", "displayName": "Equipo A" }
                },
                {
                  "homeAway": "away", "score": "1", "winner": false,
                  "team": { "id": "11", "name": "B", "abbreviation": "BBB", "displayName": "Equipo B" }
                }
              ]
            }
          ]
        }
      ]
    }
    """;

    private const string CanceledJson = """
    {
      "events": [
        {
          "id": "600004",
          "date": "2026-04-01T02:00:00Z",
          "competitions": [
            {
              "status": {
                "type": { "name": "STATUS_CANCELED", "state": "pre", "detail": "Canceled" }
              },
              "competitors": [
                {
                  "homeAway": "home", "score": "0", "winner": false,
                  "team": { "id": "10", "name": "A", "abbreviation": "AAA", "displayName": "Equipo A" }
                },
                {
                  "homeAway": "away", "score": "0", "winner": false,
                  "team": { "id": "11", "name": "B", "abbreviation": "BBB", "displayName": "Equipo B" }
                }
              ]
            }
          ]
        }
      ]
    }
    """;

    // ─── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public void ParseScoreboard_WithPostponedStatus_SetsCorrectState()
    {
        var games = _parser.ParseScoreboard(PostponedJson);

        Assert.Single(games);
        var game = games[0];
        Assert.Equal("postponed", game.StatusState);
        // CRÍTICO: partido pospuesto NUNCA debe tener ganador asignado
        Assert.Null(game.WinnerAbbr);
    }

    [Fact]
    public void ParseScoreboard_WithPostponedStatus_KeepsOriginalWeekNumber()
    {
        // El partido pospuesto DEBE mantener su semana original (no moverse)
        var games = _parser.ParseScoreboard(PostponedJson);
        Assert.Single(games);
        Assert.Equal(10, games[0].EspnWeekNumber);
    }

    [Fact]
    public void ParseScoreboard_WithFinalStatus_MapsScoresAndWinnerCorrectly()
    {
        var games = _parser.ParseScoreboard(FinalJson);

        Assert.Single(games);
        var game = games[0];
        Assert.Equal("post", game.StatusState);
        Assert.Equal(2, game.HomeScore);
        Assert.Equal(1, game.AwayScore);
        Assert.Equal("AME", game.WinnerAbbr);
    }

    [Fact]
    public void ParseScoreboard_TieGame_SetsWinnerAsEmpate()
    {
        var games = _parser.ParseScoreboard(TieJson);

        Assert.Single(games);
        Assert.Equal("EMPATE", games[0].WinnerAbbr);
    }

    [Fact]
    public void ParseScoreboard_CanceledStatus_TreatsAsPostponed()
    {
        // STATUS_CANCELED debe tratarse como postponed, nunca como post
        var games = _parser.ParseScoreboard(CanceledJson);

        Assert.Single(games);
        Assert.Equal("postponed", games[0].StatusState);
        Assert.Null(games[0].WinnerAbbr);
    }

    [Fact]
    public void ParseScoreboard_EmptyEvents_ReturnsEmptyList()
    {
        var games = _parser.ParseScoreboard("{\"events\":[]}");
        Assert.Empty(games);
    }

    [Fact]
    public void ParseScoreboard_InvalidJson_ReturnsEmptyList()
    {
        // Cuando events no existe, retorna vacío sin excepción
        var games = _parser.ParseScoreboard("{\"other\":\"data\"}");
        Assert.Empty(games);
    }

    [Fact]
    public void GroupByWeek_UsesEspnWeekNumberWhenAvailable()
    {
        var games = _parser.ParseScoreboard(FinalJson);
        var grouped = _parser.GroupByWeek(games);

        // El partido de FinalJson tiene week.number = 11
        Assert.True(grouped.ContainsKey(11));
        Assert.Single(grouped[11]);
    }

    [Theory]
    [InlineData("401877045", 1)]  // Necaxa vs Atlante (Jornada 1)
    [InlineData("401876984", 7)]  // Toluca at Puebla (Jornada 7 reprogramado)
    [InlineData("401876966", 9)]  // Santos at Toluca (Jornada 9)
    [InlineData("401876964", 10)] // Jornada 10
    [InlineData("401876955", 11)] // Jornada 11
    [InlineData("401877054", 17)] // Jornada 17
    [InlineData("401840815", 1)]  // Clausura Jornada 1
    public void ResolveLigaMxJornada_MapsToOfficialJornadaCorrectly(string espnGameId, int expectedJornada)
    {
        var jornada = EspnScoreboardParser.ResolveLigaMxJornada(espnGameId);
        Assert.Equal(expectedJornada, jornada);
    }
}
