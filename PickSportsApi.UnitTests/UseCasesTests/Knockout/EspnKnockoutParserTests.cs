using FluentAssertions;
using Domain.Common;
using Interface.UseCases.Knockout;
using UseCases.Liguilla.Espn;

namespace PickSportsApi.UnitTests.UseCasesTests.Knockout;

public class EspnKnockoutParserTests
{
    private const string Nhl = "NHL_BEST_OF_7";
    private const string Nfl = "NFL_RESEEDED_SINGLE_GAME";
    private const string Uefa = "UEFA_TWO_LEG";

    private readonly EspnKnockoutParser _parser = new();

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Espn", name + ".json"));

    [Fact]
    public void ParseScoreboard_NhlSeriesInProgress_ReturnsOneTiePerSeriesWithWins()
    {
        var json = Fixture("nhl-playoffs-20260420");

        var ties = _parser.ParseScoreboard(json, Nhl);

        ties.Should().HaveCount(4);
        var tie = ties.Single(t => t.GroupKey == "post-season:15-16");
        tie.Conference.Should().Be("east");
        tie.TotalLegs.Should().Be(7);
        tie.SeriesCompleted.Should().BeFalse();
        tie.WinnerEspnTeamId.Should().BeNull();
        tie.Wins.Should().Contain(new KeyValuePair<string, int>("15", 2));
        tie.Wins.Should().Contain(new KeyValuePair<string, int>("16", 0));
        ties.Where(t => t.Headline.StartsWith("West")).Should().OnlyContain(t => t.Conference == "west");
    }

    [Fact]
    public void ParseScoreboard_NhlSeriesCompleted_ReturnsWinnerWithFourWins()
    {
        var json = Fixture("nhl-series-completed-20260425");

        var ties = _parser.ParseScoreboard(json, Nhl);

        var tie = ties.Single(t => t.GroupKey == "post-season:14-7");
        tie.SeriesCompleted.Should().BeTrue();
        tie.WinnerEspnTeamId.Should().Be("7");
        tie.Wins["7"].Should().Be(4);
    }

    [Fact]
    public void ParseScoreboard_NflWildCard_ReturnsSingleGameWithConferenceAndWinner()
    {
        var json = Fixture("nfl-postseason-20260111");

        var ties = _parser.ParseScoreboard(json, Nfl);

        ties.Should().HaveCount(3);
        ties.Select(t => t.Conference).Should().BeSubsetOf(new[] { "afc", "nfc" });
        var buffalo = ties.Single(t => t.HomeEspnTeamId == "30" && t.AwayEspnTeamId == "2");
        buffalo.Conference.Should().Be("afc");
        buffalo.TotalLegs.Should().Be(1);
        buffalo.SeriesCompleted.Should().BeTrue();
        buffalo.WinnerEspnTeamId.Should().Be("2");
    }

    [Fact]
    public void ParseScoreboard_ChampionsSecondLeg_ReturnsAggregateAndWinner()
    {
        var json = Fixture("ucl-r16-leg2-20260318");

        var ties = _parser.ParseScoreboard(json, Uefa);

        var tie = ties.Single(t => t.GroupKey == "round-of-16:361-83");
        tie.SeriesCompleted.Should().BeTrue();
        tie.WinnerEspnTeamId.Should().Be("83");
        tie.LegNumber.Should().Be(2);
        tie.TotalLegs.Should().Be(2);
        tie.AggregateScore["83"].Should().Be(8.0);
        tie.AggregateScore["361"].Should().Be(3.0);
    }

    [Fact]
    public void ParseScoreboard_ChampionsFirstLeg_ReturnsUnresolvedTie()
    {
        var json = Fixture("ucl-r16-leg1-20260311");

        var ties = _parser.ParseScoreboard(json, Uefa);

        ties.Should().NotBeEmpty();
        ties.Should().OnlyContain(t => !t.SeriesCompleted && t.WinnerEspnTeamId == null && t.LegNumber == 1);
    }

    [Fact]
    public void ParseScoreboard_ChampionsFinalDecidedOnPenalties_ReturnsWinnerFlaggedByEspn()
    {
        var json = Fixture("ucl-final-20260530");

        var ties = _parser.ParseScoreboard(json, Uefa);

        var tie = ties.Should().ContainSingle().Subject;
        tie.RoundSlug.Should().Be("final");
        tie.TotalLegs.Should().Be(1);
        tie.WinnerEspnTeamId.Should().Be("160");
    }

    [Fact]
    public void ParseScoreboard_ChampionsKnockoutPlayoffs_KeepsInformativeSlug()
    {
        var json = Fixture("ucl-playoffs-leg2-20260225");

        var ties = _parser.ParseScoreboard(json, Uefa);

        ties.Should().NotBeEmpty();
        ties.Should().OnlyContain(t => t.RoundSlug == "knockout-round-playoffs");
    }

    [Fact]
    public void ParseScoreboard_RegularSeasonEvents_AreIgnored()
    {
        var json = Fixture("nhl-playoffs-20260420").Replace("\"slug\":\"post-season\"", "\"slug\":\"regular-season\"");

        var ties = _parser.ParseScoreboard(json, Nhl);

        ties.Should().BeEmpty();
    }

    [Theory]
    [InlineData("{no", Nhl)]
    [InlineData("", Nhl)]
    [InlineData("{\"events\":[{\"season\":{\"slug\":\"post-season\"}}]}", Nhl)]
    [InlineData("{}", Uefa)]
    public void ParseScoreboard_InvalidOrIncompleteJson_ReturnsEmptyWithoutThrowing(string json, string format)
    {
        var act = () => _parser.ParseScoreboard(json, format);

        act.Should().NotThrow();
        act().Should().BeEmpty();
    }

    [Fact]
    public void ParseScoreboard_UnsupportedFormat_ReturnsEmpty()
    {
        var json = Fixture("nhl-playoffs-20260420");

        var ties = _parser.ParseScoreboard(json, "UNKNOWN_FORMAT");

        ties.Should().BeEmpty();
    }

    // ---- Liga MX (FMF_LIGUILLA): fixtures minimos derivados de scoreboard mex.1 Clausura 2026 ----

    private static string FmfEvent(string slug, int leg, string homeId, string awayId, bool completed,
        string headline, string? winnerId, double aggHome, double aggAway, string date)
    {
        string Winner(string id) => winnerId == null ? "false" : (id == winnerId ? "true" : "false");
        return $$"""
            {
              "date": "{{date}}",
              "season": { "slug": "{{slug}}" },
              "competitions": [{
                "leg": { "value": {{leg}} },
                "notes": [{ "headline": "{{headline}}" }],
                "series": {
                  "completed": {{(completed ? "true" : "false")}},
                  "totalCompetitions": 2,
                  "competitors": [
                    { "id": "{{homeId}}", "winner": {{Winner(homeId)}}, "aggregateScore": {{aggHome.ToString(System.Globalization.CultureInfo.InvariantCulture)}} },
                    { "id": "{{awayId}}", "winner": {{Winner(awayId)}}, "aggregateScore": {{aggAway.ToString(System.Globalization.CultureInfo.InvariantCulture)}} }
                  ]
                },
                "competitors": [
                  { "homeAway": "home", "id": "{{homeId}}", "team": { "id": "{{homeId}}" } },
                  { "homeAway": "away", "id": "{{awayId}}", "team": { "id": "{{awayId}}" } }
                ]
              }]
            }
            """;
    }

    private static string FmfScoreboard(params string[] events) => "{\"events\":[" + string.Join(",", events) + "]}";

    private const string FmfQf = "clausura---quarterfinals";

    [Fact]
    public void ParseScoreboard_FmfSupportedFormat_IsListed()
    {
        _parser.SupportedFormatCodes.Should().Contain(KnockoutFormatCodes.FmfLiguilla);
    }

    [Fact]
    public void ParseScoreboard_FmfSecondLegWithWinnerFlag_ReturnsGroupedTieWithWinner()
    {
        var json = FmfScoreboard(
            FmfEvent(FmfQf, 1, "232", "219", false, "1st Leg", null, 1, 3, "2026-05-02T01:00Z"),
            FmfEvent(FmfQf, 2, "219", "232", true,
                "2nd Leg - Tied on aggregate - Guadalajara advanced for being the higher seed", "219", 3, 3, "2026-05-09T01:00Z"));

        var ties = _parser.ParseScoreboard(json, KnockoutFormatCodes.FmfLiguilla);

        var tie = ties.Should().ContainSingle().Subject;
        tie.RoundSlug.Should().Be(FmfQf);
        tie.GroupKey.Should().Be("clausura---quarterfinals:219-232");
        tie.TotalLegs.Should().Be(2);
        tie.SeriesCompleted.Should().BeTrue();
        tie.WinnerEspnTeamId.Should().Be("219");
        tie.KickoffUtc.Should().Be(new DateTime(2026, 5, 2, 1, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ParseScoreboard_FmfAggregateWinnerFlagged_ReturnsFlaggedWinner()
    {
        var json = FmfScoreboard(
            FmfEvent(FmfQf, 2, "216", "218", true, "2nd Leg - Cruz Azul advance 4-2 on aggregate", "218", 2, 4, "2026-05-10T01:00Z"));

        var ties = _parser.ParseScoreboard(json, KnockoutFormatCodes.FmfLiguilla);

        ties.Single().WinnerEspnTeamId.Should().Be("218");
    }

    [Fact]
    public void ParseScoreboard_FmfTiedWithoutWinnerFlag_ReturnsHomeTeamOfSecondLegAsHigherSeed()
    {
        // Caso real UNAM (233, local de la vuelta) vs America (227): ESPN deja winner=false en ambos y el headline
        // dice "AME win on away goals" aunque avanzo UNAM; el criterio es el local de la vuelta (mejor sembrado).
        var json = FmfScoreboard(
            FmfEvent(FmfQf, 1, "227", "233", false, "1st Leg", null, 3, 3, "2026-05-03T23:00Z"),
            FmfEvent(FmfQf, 2, "233", "227", true,
                "2nd Leg - Tied on aggregate - AME win on away goals", null, 6, 6, "2026-05-11T01:15Z"));

        var ties = _parser.ParseScoreboard(json, KnockoutFormatCodes.FmfLiguilla);

        var tie = ties.Should().ContainSingle().Subject;
        tie.SeriesCompleted.Should().BeTrue();
        tie.WinnerEspnTeamId.Should().Be("233");
    }

    [Fact]
    public void ParseScoreboard_FmfCompletedWithoutFlagButDifferentAggregate_ReturnsHigherAggregate()
    {
        var json = FmfScoreboard(
            FmfEvent(FmfQf, 2, "234", "223", true, "2nd Leg - Pachuca advance 3-0 on aggregate", null, 0, 3, "2026-05-10T01:00Z"));

        var ties = _parser.ParseScoreboard(json, KnockoutFormatCodes.FmfLiguilla);

        ties.Single().WinnerEspnTeamId.Should().Be("223");
    }

    [Fact]
    public void ParseScoreboard_FmfFirstLegOnly_ReturnsUnresolvedTie()
    {
        var json = FmfScoreboard(
            FmfEvent(FmfQf, 1, "227", "233", false, "1st Leg", null, 3, 3, "2026-05-03T23:00Z"));

        var ties = _parser.ParseScoreboard(json, KnockoutFormatCodes.FmfLiguilla);

        ties.Single().SeriesCompleted.Should().BeFalse();
        ties.Single().WinnerEspnTeamId.Should().BeNull();
    }

    [Theory]
    [InlineData("clausura---quarterfinals")]
    [InlineData("apertura---semifinals")]
    [InlineData("clausura---finals")]
    public void ParseScoreboard_FmfKnockoutSlugs_AreRecognizedAndKeptInRoundSlug(string slug)
    {
        var json = FmfScoreboard(FmfEvent(slug, 1, "1", "2", false, "1st Leg", null, 0, 0, "2026-05-03T23:00Z"));

        var ties = _parser.ParseScoreboard(json, KnockoutFormatCodes.FmfLiguilla);

        ties.Single().RoundSlug.Should().Be(slug);
    }

    [Fact]
    public void ParseScoreboard_FmfRegularSeasonEvent_IsIgnored()
    {
        var json = FmfScoreboard(
            FmfEvent("torneo-clausura", 1, "1", "2", false, "", null, 0, 0, "2026-02-03T23:00Z"));

        var ties = _parser.ParseScoreboard(json, KnockoutFormatCodes.FmfLiguilla);

        ties.Should().BeEmpty();
    }

    [Fact]
    public void ParseScoreboard_FmfSlugForOtherFormat_IsNotParsedAsUefa()
    {
        var json = FmfScoreboard(FmfEvent(FmfQf, 1, "1", "2", false, "1st Leg", null, 0, 0, "2026-05-03T23:00Z"));

        var ties = _parser.ParseScoreboard(json, KnockoutFormatCodes.UefaTwoLeg);

        ties.Should().BeEmpty();
    }

    private static string FmfStandingsEntry(string teamId, int? rank, int points)
    {
        var rankStat = rank.HasValue ? $"{{ \"name\": \"rank\", \"value\": {rank.Value} }}," : string.Empty;
        return $"{{ \"team\": {{ \"id\": \"{teamId}\" }}, \"stats\": [ {rankStat} {{ \"name\": \"points\", \"value\": {points} }} ] }}";
    }

    [Fact]
    public void ParseFmfPlayoffSeeds_GeneralTable_ReturnsOnlyTopEightByRank()
    {
        // Entradas desordenadas a proposito: el seed sale de la estadistica rank, no del orden del JSON.
        var entries = new List<string>();
        for (var rank = 10; rank >= 1; rank--) entries.Add(FmfStandingsEntry((200 + rank).ToString(), rank, 40 - rank));
        var json = "{\"children\":[{\"name\":\"2026 Torneo Apertura\",\"standings\":{\"entries\":[" + string.Join(",", entries) + "]}}]}";

        var seeds = _parser.ParseFmfPlayoffSeeds(json);

        seeds.Should().HaveCount(8);
        seeds["201"].Should().Be(1);
        seeds["204"].Should().Be(4);
        seeds["208"].Should().Be(8);
        seeds.Should().NotContainKey("209");
        seeds.Should().NotContainKey("210");
    }

    [Fact]
    public void ParseFmfPlayoffSeeds_EntriesWithoutRankStat_UseTableOrder()
    {
        var json = "{\"children\":[{\"standings\":{\"entries\":[" +
                   FmfStandingsEntry("218", null, 32) + "," + FmfStandingsEntry("227", null, 30) + "]}}]}";

        var seeds = _parser.ParseFmfPlayoffSeeds(json);

        seeds["218"].Should().Be(1);
        seeds["227"].Should().Be(2);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{no")]
    [InlineData("{}")]
    [InlineData("{\"children\":[]}")]
    public void ParseFmfPlayoffSeeds_InvalidOrMissingTable_ReturnsEmptyWithoutThrowing(string json)
    {
        var act = () => _parser.ParseFmfPlayoffSeeds(json);

        act.Should().NotThrow();
        act().Should().BeEmpty();
    }

    [Fact]
    public void ParseNflPlayoffSeeds_RealStandings_ReturnsOnlySeedsOneToSeven()
    {
        var json = Fixture("nfl-standings-2025");

        var seeds = _parser.ParseNflPlayoffSeeds(json);

        seeds["7"].Should().Be(new NflPlayoffSeed("afc", 1));
        seeds["17"].Should().Be(new NflPlayoffSeed("afc", 2));
        seeds["30"].Should().Be(new NflPlayoffSeed("afc", 3));
        seeds["26"].Should().Be(new NflPlayoffSeed("nfc", 1));
        seeds["3"].Should().Be(new NflPlayoffSeed("nfc", 2));
        seeds.Values.Should().OnlyContain(s => s.Seed >= 1 && s.Seed <= 7);
    }

    [Fact]
    public void ParseNflPlayoffSeeds_InvalidJson_ReturnsEmpty()
    {
        var seeds = _parser.ParseNflPlayoffSeeds("{no");

        seeds.Should().BeEmpty();
    }

    [Fact]
    public void ParseNhlPlayoffSeeds_RealStandings_ReturnsSixteenSeededTeamsWithEightPerConference()
    {
        var json = Fixture("nhl-standings-2026-level3");

        var seeds = _parser.ParseNhlPlayoffSeeds(json);

        seeds.Should().HaveCount(16);
        seeds.Values.Count(s => s.Conference == "east").Should().Be(8);
        seeds.Values.Count(s => s.Conference == "west").Should().Be(8);
        seeds.Values.Should().OnlyContain(s => s.Seed >= 1 && s.Seed <= 8);
    }

    [Fact]
    public void ParseNhlPlayoffSeeds_RealStandings_MapsConferenceDivisionAndSeedForTopSeeds()
    {
        var json = Fixture("nhl-standings-2026-level3");

        var seeds = _parser.ParseNhlPlayoffSeeds(json);

        seeds["7"].Should().Be(new NhlPlayoffSeed("east", "metropolitan", 1, true));
        seeds["17"].Should().Be(new NhlPlayoffSeed("west", "central", 1, true));
    }

    [Fact]
    public void ParseNhlPlayoffSeeds_RealStandings_IdentifiesExactFourDivisionWinners()
    {
        var json = Fixture("nhl-standings-2026-level3");

        var seeds = _parser.ParseNhlPlayoffSeeds(json);

        var divisionWinners = seeds.Where(kv => kv.Value.IsDivisionWinner).Select(kv => kv.Key).ToList();
        divisionWinners.Should().HaveCount(4);
        divisionWinners.Should().BeEquivalentTo(new[] { "2", "7", "17", "37" });

        seeds["2"].Should().Be(new NhlPlayoffSeed("east", "atlantic", 2, true));
        seeds["7"].Should().Be(new NhlPlayoffSeed("east", "metropolitan", 1, true));
        seeds["17"].Should().Be(new NhlPlayoffSeed("west", "central", 1, true));
        seeds["37"].Should().Be(new NhlPlayoffSeed("west", "pacific", 4, true));
        seeds["9"].Should().Be(new NhlPlayoffSeed("west", "central", 2, false));
    }

    [Fact]
    public void ParseNhlPlayoffSeeds_TiedPointsInSameDivision_PicksLowestSeedAsWinner()
    {
        const string json = """
            {
              "children": [
                {
                  "name": "Eastern Conference",
                  "children": [
                    {
                      "name": "Atlantic Division",
                      "standings": {
                        "entries": [
                          {
                            "team": { "id": "20" },
                            "stats": [
                              { "name": "playoffSeed", "value": 5 },
                              { "name": "points", "value": 100 }
                            ]
                          },
                          {
                            "team": { "id": "10" },
                            "stats": [
                              { "name": "playoffSeed", "value": 3 },
                              { "name": "points", "value": 100 }
                            ]
                          }
                        ]
                      }
                    }
                  ]
                }
              ]
            }
            """;

        var seeds = _parser.ParseNhlPlayoffSeeds(json);

        seeds.Should().HaveCount(2);
        seeds["10"].Should().Be(new NhlPlayoffSeed("east", "atlantic", 3, true));
        seeds["20"].Should().Be(new NhlPlayoffSeed("east", "atlantic", 5, false));
    }

    [Fact]
    public void ParseNhlPlayoffSeeds_InvalidJson_ReturnsEmpty()
    {
        var act = () => _parser.ParseNhlPlayoffSeeds("{no");

        act.Should().NotThrow();
        act().Should().BeEmpty();
    }
}
