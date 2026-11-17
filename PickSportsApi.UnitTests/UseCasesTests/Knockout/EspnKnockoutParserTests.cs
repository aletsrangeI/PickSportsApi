using FluentAssertions;
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

        var ties = _parser.ParseScoreboard(json, "FMF_LIGUILLA");

        ties.Should().BeEmpty();
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
}
