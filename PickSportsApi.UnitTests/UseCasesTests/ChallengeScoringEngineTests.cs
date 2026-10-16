using Domain.Entities;
using DTO.Challenges;
using DTO.Scoring;
using FluentAssertions;
using UseCases.Challenges;
using Xunit;

namespace PickSportsApi.UnitTests.UseCasesTests;

public class ChallengeScoringEngineTests
{
    private readonly ChallengeScoringEngine _engine = new();

    #region Helper Builders

    private static QuinielaChallenge BuildBaseChallenge(string type, string payoutMode = "WINNER_TAKES_ALL")
    {
        return new QuinielaChallenge
        {
            Id = 1,
            QuinielaId = 10,
            WeekId = 100,
            CreatorMemberId = 1,
            ChallengeType = type,
            PayoutMode = payoutMode,
            StakesDescription = "Unos tacos de pastor y refresco",
            Status = "ACTIVE"
        };
    }

    private static QuinielaChallengeParticipant BuildParticipant(int memberId, string alias, string status = "ACCEPTED")
    {
        return new QuinielaChallengeParticipant
        {
            Id = memberId,
            ChallengeId = 1,
            MemberId = memberId,
            Status = status,
            Member = new QuinielaMember { Id = memberId, Alias = alias }
        };
    }

    #endregion

    #region Validación de Participantes Mínimos

    [Fact]
    public void ResolveChallenge_MenosDe2ParticipantesAceptados_RetornaUnacceptedExpired()
    {
        // ARRANGE
        var challenge = BuildBaseChallenge("WEEKLY_RANK");
        challenge.Participants.Add(BuildParticipant(1, "Alex", status: "ACCEPTED"));
        challenge.Participants.Add(BuildParticipant(2, "Fer", status: "DECLINED"));

        // ACT
        var result = _engine.ResolveChallenge(challenge, Enumerable.Empty<Match>(), Enumerable.Empty<Pick>());

        // ASSERT
        result.FinalStatus.Should().Be("UNACCEPTED_EXPIRED");
        result.ResolutionSummary.Should().Contain("no contó con suficientes participantes confirmados");
    }

    #endregion

    #region WEEKLY_RANK & GENERAL_RANK

    [Fact]
    public void ResolveChallenge_WeeklyRank_WinnerTakesAll_GanaMejorPosicion()
    {
        // ARRANGE
        var challenge = BuildBaseChallenge("WEEKLY_RANK", "WINNER_TAKES_ALL");
        var p1 = BuildParticipant(1, "Alex");
        var p2 = BuildParticipant(2, "Fer");
        var p3 = BuildParticipant(3, "Carlos");
        challenge.Participants = new List<QuinielaChallengeParticipant> { p1, p2, p3 };

        var weeklyStandings = new List<MemberStandingDto>
        {
            new() { MemberId = 1, Alias = "Alex", Rank = 3, Hits = 6 },
            new() { MemberId = 2, Alias = "Fer", Rank = 1, Hits = 8 },
            new() { MemberId = 3, Alias = "Carlos", Rank = 5, Hits = 5 }
        };

        // ACT
        var result = _engine.ResolveChallenge(challenge, Enumerable.Empty<Match>(), Enumerable.Empty<Pick>(), weeklyStandings: weeklyStandings);

        // ASSERT
        result.FinalStatus.Should().Be("RESOLVED_WITH_WINNER");
        result.WinnerMemberIds.Should().ContainSingle().Which.Should().Be(2); // Fer es Rank 1
        result.LoserMemberIds.Should().BeEquivalentTo(new[] { 1, 3 });
        result.ParticipantOutcomes[2].Should().Be("WINNER");
        result.ParticipantOutcomes[1].Should().Be("LOSER");
        result.ParticipantOutcomes[3].Should().Be("LOSER");
        result.ResolutionSummary.Should().Contain("El Rey Cobra");
        result.ResolutionSummary.Should().Contain("Fer");
    }

    [Fact]
    public void ResolveChallenge_WeeklyRank_LastPlacePays_PierdePeorPosicion()
    {
        // ARRANGE
        var challenge = BuildBaseChallenge("WEEKLY_RANK", "LAST_PLACE_PAYS");
        var p1 = BuildParticipant(1, "Alex");
        var p2 = BuildParticipant(2, "Fer");
        var p3 = BuildParticipant(3, "Carlos");
        challenge.Participants = new List<QuinielaChallengeParticipant> { p1, p2, p3 };

        var weeklyStandings = new List<MemberStandingDto>
        {
            new() { MemberId = 1, Alias = "Alex", Rank = 2, Hits = 7 },
            new() { MemberId = 2, Alias = "Fer", Rank = 1, Hits = 8 },
            new() { MemberId = 3, Alias = "Carlos", Rank = 10, Hits = 3 } // Carlos peor
        };

        // ACT
        var result = _engine.ResolveChallenge(challenge, Enumerable.Empty<Match>(), Enumerable.Empty<Pick>(), weeklyStandings: weeklyStandings);

        // ASSERT
        result.FinalStatus.Should().Be("RESOLVED_WITH_WINNER");
        result.LoserMemberIds.Should().ContainSingle().Which.Should().Be(3); // Carlos paga
        result.WinnerMemberIds.Should().BeEquivalentTo(new[] { 1, 2 });
        result.ParticipantOutcomes[3].Should().Be("LOSER");
        result.ResolutionSummary.Should().Contain("El Último Paga");
        result.ResolutionSummary.Should().Contain("Carlos");
    }

    [Fact]
    public void ResolveChallenge_GeneralRank_WinnerTakesAll_ResuelveCorrectamente()
    {
        // ARRANGE
        var challenge = BuildBaseChallenge("GENERAL_RANK", "WINNER_TAKES_ALL");
        var p1 = BuildParticipant(1, "Alex");
        var p2 = BuildParticipant(2, "Fer");
        challenge.Participants = new List<QuinielaChallengeParticipant> { p1, p2 };

        var generalStandings = new List<MemberStandingDto>
        {
            new() { MemberId = 1, Alias = "Alex", Rank = 1, Hits = 50 },
            new() { MemberId = 2, Alias = "Fer", Rank = 4, Hits = 44 }
        };

        // ACT
        var result = _engine.ResolveChallenge(challenge, Enumerable.Empty<Match>(), Enumerable.Empty<Pick>(), generalStandings: generalStandings);

        // ASSERT
        result.FinalStatus.Should().Be("RESOLVED_WITH_WINNER");
        result.WinnerMemberIds.Should().ContainSingle().Which.Should().Be(1);
        result.LoserMemberIds.Should().ContainSingle().Which.Should().Be(2);
        result.ResolutionSummary.Should().Contain("Tabla General");
    }

    #endregion

    #region MATCH_WINNER

    [Fact]
    public void ResolveChallenge_MatchWinner_GanadorAcierta_GanaReto()
    {
        // ARRANGE
        var challenge = BuildBaseChallenge("MATCH_WINNER");
        challenge.TargetMatchId = 10;
        var p1 = BuildParticipant(1, "Alex");
        p1.PredictedWinnerAbbr = "AME";
        var p2 = BuildParticipant(2, "Fer");
        p2.PredictedWinnerAbbr = "CAZ";
        challenge.Participants = new List<QuinielaChallengeParticipant> { p1, p2 };

        var match = new Match
        {
            Id = 10,
            WinnerAbbr = "AME",
            StatusState = "post",
            HomeTeam = new Team { Abbreviation = "AME" },
            AwayTeam = new Team { Abbreviation = "CAZ" }
        };

        // ACT
        var result = _engine.ResolveChallenge(challenge, new[] { match }, Enumerable.Empty<Pick>());

        // ASSERT
        result.FinalStatus.Should().Be("RESOLVED_WITH_WINNER");
        result.WinnerMemberIds.Should().ContainSingle().Which.Should().Be(1);
        result.LoserMemberIds.Should().ContainSingle().Which.Should().Be(2);
        result.ResolutionSummary.Should().Contain("Alex");
        result.ResolutionSummary.Should().Contain("AME");
    }

    [Fact]
    public void ResolveChallenge_MatchWinner_NadieAcierta_TerminaEnResolvedNoWinnerConBurla()
    {
        // ARRANGE
        var challenge = BuildBaseChallenge("MATCH_WINNER");
        challenge.TargetMatchId = 10;
        var p1 = BuildParticipant(1, "Alex");
        p1.PredictedWinnerAbbr = "AME";
        var p2 = BuildParticipant(2, "Fer");
        p2.PredictedWinnerAbbr = "CAZ";
        challenge.Participants = new List<QuinielaChallengeParticipant> { p1, p2 };

        // El partido terminó en EMPATE
        var match = new Match
        {
            Id = 10,
            WinnerAbbr = "EMPATE",
            StatusState = "post",
            HomeTeam = new Team { Abbreviation = "AME" },
            AwayTeam = new Team { Abbreviation = "CAZ" }
        };

        // ACT
        var result = _engine.ResolveChallenge(challenge, new[] { match }, Enumerable.Empty<Pick>());

        // ASSERT
        result.FinalStatus.Should().Be("RESOLVED_NO_WINNER");
        result.WinnerMemberIds.Should().BeEmpty();
        result.ParticipantOutcomes.Values.Should().AllBeEquivalentTo("NO_WINNER_BUST");
        result.ResolutionSummary.Should().Contain("🤡 Nadie le atinó");
    }

    #endregion

    #region EXACT_SCORE

    [Fact]
    public void ResolveChallenge_ExactScore_AciertoExacto_GanaReto()
    {
        // ARRANGE
        var challenge = BuildBaseChallenge("EXACT_SCORE");
        challenge.TargetMatchId = 20;
        var p1 = BuildParticipant(1, "Alex");
        p1.PredictedHomeScore = 2;
        p1.PredictedAwayScore = 1;

        var p2 = BuildParticipant(2, "Fer");
        p2.PredictedHomeScore = 1;
        p2.PredictedAwayScore = 1;

        challenge.Participants = new List<QuinielaChallengeParticipant> { p1, p2 };

        var match = new Match
        {
            Id = 20,
            HomeScore = 2,
            AwayScore = 1,
            StatusState = "post",
            HomeTeam = new Team { Abbreviation = "TIG" },
            AwayTeam = new Team { Abbreviation = "MTY" }
        };

        // ACT
        var result = _engine.ResolveChallenge(challenge, new[] { match }, Enumerable.Empty<Pick>());

        // ASSERT
        result.FinalStatus.Should().Be("RESOLVED_WITH_WINNER");
        result.WinnerMemberIds.Should().ContainSingle().Which.Should().Be(1);
        result.LoserMemberIds.Should().ContainSingle().Which.Should().Be(2);
        result.ResolutionSummary.Should().Contain("Ojo de francotirador");
        result.ResolutionSummary.Should().Contain("Alex");
    }

    [Fact]
    public void ResolveChallenge_ExactScore_NadieAtinaMarcador_TerminaEnResolvedNoWinnerConBurla()
    {
        // ARRANGE
        var challenge = BuildBaseChallenge("EXACT_SCORE");
        challenge.TargetMatchId = 20;
        var p1 = BuildParticipant(1, "Alex");
        p1.PredictedHomeScore = 2;
        p1.PredictedAwayScore = 0;

        var p2 = BuildParticipant(2, "Fer");
        p2.PredictedHomeScore = 0;
        p2.PredictedAwayScore = 0;

        challenge.Participants = new List<QuinielaChallengeParticipant> { p1, p2 };

        var match = new Match
        {
            Id = 20,
            HomeScore = 3,
            AwayScore = 2,
            StatusState = "post",
            HomeTeam = new Team { Abbreviation = "TIG" },
            AwayTeam = new Team { Abbreviation = "MTY" }
        };

        // ACT
        var result = _engine.ResolveChallenge(challenge, new[] { match }, Enumerable.Empty<Pick>());

        // ASSERT
        result.FinalStatus.Should().Be("RESOLVED_NO_WINNER");
        result.WinnerMemberIds.Should().BeEmpty();
        result.ParticipantOutcomes.Values.Should().AllBeEquivalentTo("NO_WINNER_BUST");
        result.ResolutionSummary.Should().Contain("Marcador imposible");
    }

    #endregion

    #region SUDDEN_DEATH & DOBLE VIDA

    [Fact]
    public void ResolveChallenge_SuddenDeath_DobleVidaActivada_NadieMuereYRetoContinua()
    {
        // ARRANGE
        // Partido 1: Viernes 19:00 - Ambos aciertan
        // Partido 2: Viernes 21:00 - AMBOS fallan -> ¡Doble Vida! Nadie es eliminado
        // Partido 3: Sábado 17:00 - Alex acierta, Fer falla -> Alex gana, Fer eliminado en Partido 3
        var challenge = BuildBaseChallenge("SUDDEN_DEATH");
        var p1 = BuildParticipant(1, "Alex");
        var p2 = BuildParticipant(2, "Fer");
        challenge.Participants = new List<QuinielaChallengeParticipant> { p1, p2 };

        var m1 = new Match { Id = 1, DateUtc = new DateTime(2026, 10, 16, 19, 0, 0, DateTimeKind.Utc), WinnerAbbr = "AME", StatusState = "post", HomeTeam = new Team { Abbreviation = "AME" }, AwayTeam = new Team { Abbreviation = "PUM" } };
        var m2 = new Match { Id = 2, DateUtc = new DateTime(2026, 10, 16, 21, 0, 0, DateTimeKind.Utc), WinnerAbbr = "TOL", StatusState = "post", HomeTeam = new Team { Abbreviation = "TOL" }, AwayTeam = new Team { Abbreviation = "LEO" } };
        var m3 = new Match { Id = 3, DateUtc = new DateTime(2026, 10, 17, 17, 0, 0, DateTimeKind.Utc), WinnerAbbr = "MTY", StatusState = "post", HomeTeam = new Team { Abbreviation = "MTY" }, AwayTeam = new Team { Abbreviation = "SAN" } };

        var matches = new List<Match> { m3, m1, m2 }; // Desordenados intencionalmente para probar orden cronológico

        var picks = new List<Pick>
        {
            // Partido 1: ambos AME (aciertan)
            new() { MemberId = 1, MatchId = 1, PickAbbr = "AME" },
            new() { MemberId = 2, MatchId = 1, PickAbbr = "AME" },

            // Partido 2: ambos fallan (pronostican LEO y EMPATE, pero ganó TOL)
            new() { MemberId = 1, MatchId = 2, PickAbbr = "LEO" },
            new() { MemberId = 2, MatchId = 2, PickAbbr = "EMPATE" },

            // Partido 3: Alex pronostica MTY (acierta), Fer pronostica SAN (falla)
            new() { MemberId = 1, MatchId = 3, PickAbbr = "MTY" },
            new() { MemberId = 2, MatchId = 3, PickAbbr = "SAN" }
        };

        // ACT
        var result = _engine.ResolveChallenge(challenge, matches, picks);

        // ASSERT
        result.FinalStatus.Should().Be("RESOLVED_WITH_WINNER");
        result.WinnerMemberIds.Should().ContainSingle().Which.Should().Be(1); // Alex gana
        result.LoserMemberIds.Should().ContainSingle().Which.Should().Be(2); // Fer pierde
        result.EliminatedAtMatchIds[2].Should().Be(3); // Fer eliminado en el partido 3
        result.DoubleLifeEvents.Should().ContainSingle()
            .Which.Should().Contain("❤️‍🔥 Doble Vida en TOL vs LEO");
        result.ResolutionSummary.Should().Contain("Alex");
        result.ResolutionSummary.Should().Contain("Sobreviviente de Muerte Súbita");
    }

    [Fact]
    public void ResolveChallenge_SuddenDeath_LastPlacePays_PrimerEliminadoPaga()
    {
        // ARRANGE: 3 participantes: Alex, Fer, Carlos
        // Partido 1: Alex y Fer aciertan, Carlos falla -> Carlos eliminado primero
        // Partido 2: Alex acierta, Fer falla
        var challenge = BuildBaseChallenge("SUDDEN_DEATH", "LAST_PLACE_PAYS");
        var p1 = BuildParticipant(1, "Alex");
        var p2 = BuildParticipant(2, "Fer");
        var p3 = BuildParticipant(3, "Carlos");
        challenge.Participants = new List<QuinielaChallengeParticipant> { p1, p2, p3 };

        var m1 = new Match { Id = 1, DateUtc = new DateTime(2026, 10, 16, 19, 0, 0, DateTimeKind.Utc), WinnerAbbr = "AME", StatusState = "post", HomeTeam = new Team { Abbreviation = "AME" }, AwayTeam = new Team { Abbreviation = "PUM" } };
        var m2 = new Match { Id = 2, DateUtc = new DateTime(2026, 10, 16, 21, 0, 0, DateTimeKind.Utc), WinnerAbbr = "TOL", StatusState = "post", HomeTeam = new Team { Abbreviation = "TOL" }, AwayTeam = new Team { Abbreviation = "LEO" } };

        var picks = new List<Pick>
        {
            // Partido 1
            new() { MemberId = 1, MatchId = 1, PickAbbr = "AME" },
            new() { MemberId = 2, MatchId = 1, PickAbbr = "AME" },
            new() { MemberId = 3, MatchId = 1, PickAbbr = "PUM" }, // Carlos falla

            // Partido 2
            new() { MemberId = 1, MatchId = 2, PickAbbr = "TOL" },
            new() { MemberId = 2, MatchId = 2, PickAbbr = "LEO" } // Fer falla
        };

        // ACT
        var result = _engine.ResolveChallenge(challenge, new[] { m1, m2 }, picks);

        // ASSERT
        result.FinalStatus.Should().Be("RESOLVED_WITH_WINNER");
        result.LoserMemberIds.Should().ContainSingle().Which.Should().Be(3); // Carlos fue el primero en caer
        result.WinnerMemberIds.Should().BeEquivalentTo(new[] { 1, 2 });
        result.ResolutionSummary.Should().Contain("Carlos fue el primero en caer");
    }

    #endregion

    #region TOTAL_GOALS & PRICE IS RIGHT ESTRICTO

    [Fact]
    public void ResolveChallenge_TotalGoals_PriceIsRightEstricto_QuienSePasaQuedaDescalificado()
    {
        // ARRANGE:
        // Total real de goles en la jornada: 25 goles (partido 1: 15 goles, partido 2: 10 goles)
        // Alex: 22 goles (cumple <= 25, diff = 3)
        // Fer: 26 goles (se pasa > 25 -> Busted)
        // Carlos: 28 goles (se pasa > 25 -> Busted)
        var challenge = BuildBaseChallenge("TOTAL_GOALS");
        var p1 = BuildParticipant(1, "Alex");
        p1.PredictedTotalGoals = 22;
        var p2 = BuildParticipant(2, "Fer");
        p2.PredictedTotalGoals = 26;
        var p3 = BuildParticipant(3, "Carlos");
        p3.PredictedTotalGoals = 28;
        challenge.Participants = new List<QuinielaChallengeParticipant> { p1, p2, p3 };

        var matches = new List<Match>
        {
            new() { Id = 1, HomeScore = 10, AwayScore = 5 }, // 15
            new() { Id = 2, HomeScore = 6, AwayScore = 4 }   // 10 -> Total 25
        };

        // ACT
        var result = _engine.ResolveChallenge(challenge, matches, Enumerable.Empty<Pick>());

        // ASSERT
        result.FinalStatus.Should().Be("RESOLVED_WITH_WINNER");
        result.ActualTotalGoals.Should().Be(25);
        result.WinnerMemberIds.Should().ContainSingle().Which.Should().Be(1); // Alex gana con 22
        result.LoserMemberIds.Should().BeEquivalentTo(new[] { 2, 3 });
        result.ResolutionSummary.Should().Contain("Alex");
        result.ResolutionSummary.Should().Contain("22 goles sin pasarse");
    }

    [Fact]
    public void ResolveChallenge_TotalGoals_TodosSePasan_TerminaEnResolvedNoWinnerConBurla()
    {
        // ARRANGE: Total real = 20 goles.
        // Alex predijo 22, Fer predijo 25. Ambos se pasaron.
        var challenge = BuildBaseChallenge("TOTAL_GOALS");
        var p1 = BuildParticipant(1, "Alex");
        p1.PredictedTotalGoals = 22;
        var p2 = BuildParticipant(2, "Fer");
        p2.PredictedTotalGoals = 25;
        challenge.Participants = new List<QuinielaChallengeParticipant> { p1, p2 };

        var matches = new List<Match>
        {
            new() { Id = 1, HomeScore = 12, AwayScore = 8 } // Total = 20
        };

        // ACT
        var result = _engine.ResolveChallenge(challenge, matches, Enumerable.Empty<Pick>());

        // ASSERT
        result.FinalStatus.Should().Be("RESOLVED_NO_WINNER");
        result.WinnerMemberIds.Should().BeEmpty();
        result.ParticipantOutcomes.Values.Should().AllBeEquivalentTo("NO_WINNER_BUST");
        result.ResolutionSummary.Should().Contain("🤡 Todos se pasaron de optimistas");
        result.ResolutionSummary.Should().Contain("20 goles");
    }

    [Fact]
    public void ResolveChallenge_TotalGoals_LastPlacePays_QuienMasSePasaPaga()
    {
        // ARRANGE: Total real = 24 goles.
        // Alex: 22 (no se pasó)
        // Fer: 26 (se pasó por 2)
        // Carlos: 30 (se pasó por 6) -> Carlos paga por ser el más alejado/busted
        var challenge = BuildBaseChallenge("TOTAL_GOALS", "LAST_PLACE_PAYS");
        var p1 = BuildParticipant(1, "Alex");
        p1.PredictedTotalGoals = 22;
        var p2 = BuildParticipant(2, "Fer");
        p2.PredictedTotalGoals = 26;
        var p3 = BuildParticipant(3, "Carlos");
        p3.PredictedTotalGoals = 30;
        challenge.Participants = new List<QuinielaChallengeParticipant> { p1, p2, p3 };

        var matches = new List<Match>
        {
            new() { Id = 1, HomeScore = 14, AwayScore = 10 } // Total 24
        };

        // ACT
        var result = _engine.ResolveChallenge(challenge, matches, Enumerable.Empty<Pick>());

        // ASSERT
        result.FinalStatus.Should().Be("RESOLVED_WITH_WINNER");
        result.LoserMemberIds.Should().ContainSingle().Which.Should().Be(3); // Carlos pierde
        result.WinnerMemberIds.Should().BeEquivalentTo(new[] { 1, 2 });
        result.ResolutionSummary.Should().Contain("Carlos");
        result.ResolutionSummary.Should().Contain("se pasó con 30 goles");
    }

    #endregion

    #region Evaluación en Vivo y Utilidades

    [Fact]
    public void EvaluateLiveState_TotalGoals_MarcaIsBustedEnTiempoReal()
    {
        // ARRANGE
        var challengeDto = new ChallengeDto
        {
            Id = 1,
            ChallengeType = "TOTAL_GOALS",
            Participants = new List<ChallengeParticipantDto>
            {
                new() { MemberId = 1, Alias = "Alex", Status = "ACCEPTED", PredictedTotalGoals = 18 },
                new() { MemberId = 2, Alias = "Fer", Status = "ACCEPTED", PredictedTotalGoals = 25 }
            }
        };

        var matches = new List<Match>
        {
            new() { Id = 1, HomeScore = 12, AwayScore = 8 } // van 20 goles
        };

        // ACT
        _engine.EvaluateLiveState(challengeDto, matches, Enumerable.Empty<Pick>());

        // ASSERT
        challengeDto.ActualTotalGoals.Should().Be(20);
        var p1 = challengeDto.Participants.First(p => p.MemberId == 1);
        var p2 = challengeDto.Participants.First(p => p.MemberId == 2);

        p1.IsBusted.Should().BeTrue();
        p1.CurrentLiveValue.Should().Contain("💥 Se pasó");

        p2.IsBusted.Should().BeFalse();
        p2.CurrentLiveValue.Should().Contain("Faltan 5");
    }

    [Fact]
    public void CalculateTakenOptions_RetornaOpcionesOcupadasParaEvitarDuplicados()
    {
        // ARRANGE
        var participantsWinner = new List<ChallengeParticipantDto>
        {
            new() { Status = "ACCEPTED", PredictedWinnerAbbr = "AME" },
            new() { Status = "ACCEPTED", PredictedWinnerAbbr = "CAZ" }
        };

        var participantsScore = new List<ChallengeParticipantDto>
        {
            new() { Status = "ACCEPTED", PredictedHomeScore = 2, PredictedAwayScore = 1 },
            new() { Status = "ACCEPTED", PredictedHomeScore = 0, PredictedAwayScore = 0 }
        };

        var participantsGoals = new List<ChallengeParticipantDto>
        {
            new() { Status = "ACCEPTED", PredictedTotalGoals = 24 },
            new() { Status = "ACCEPTED", PredictedTotalGoals = 28 }
        };

        // ACT
        var takenWinners = _engine.CalculateTakenOptions("MATCH_WINNER", participantsWinner);
        var takenScores = _engine.CalculateTakenOptions("EXACT_SCORE", participantsScore);
        var takenGoals = _engine.CalculateTakenOptions("TOTAL_GOALS", participantsGoals);

        // ASSERT
        takenWinners.Should().BeEquivalentTo(new[] { "AME", "CAZ" });
        takenScores.Should().BeEquivalentTo(new[] { "2-1", "0-0" });
        takenGoals.Should().BeEquivalentTo(new[] { "24", "28" });
    }

    #endregion
}
