using System.Text.Json;
using Domain.Entities;
using DTO.Challenges;
using DTO.Scoring;

namespace UseCases.Challenges;

public class ChallengeScoringEngine
{
    /// <summary>
    /// Resuelve un reto completado de manera pura y determinista.
    /// Retorna un ChallengeResolutionResult con ganadores, perdedores, eventos de doble vida y resumen editorial.
    /// </summary>
    public ChallengeResolutionResult ResolveChallenge(
        QuinielaChallenge challenge,
        IEnumerable<Match> matches,
        IEnumerable<Pick> picks,
        IEnumerable<MemberStandingDto>? weeklyStandings = null,
        IEnumerable<MemberStandingDto>? generalStandings = null)
    {
        var acceptedParticipants = challenge.Participants
            .Where(p => p.Status == "ACCEPTED")
            .ToList();

        if (acceptedParticipants.Count < 2)
        {
            return new ChallengeResolutionResult
            {
                FinalStatus = "UNACCEPTED_EXPIRED",
                ResolutionSummary = "El reto no contó con suficientes participantes confirmados al cierre de la jornada.",
                ParticipantOutcomes = challenge.Participants.ToDictionary(p => p.MemberId, _ => "NONE")
            };
        }

        var matchesList = matches.ToList();
        var picksList = picks.ToList();

        return challenge.ChallengeType switch
        {
            "WEEKLY_RANK" => ResolveWeeklyRankChallenge(challenge, acceptedParticipants, weeklyStandings),
            "GENERAL_RANK" => ResolveGeneralRankChallenge(challenge, acceptedParticipants, generalStandings),
            "MATCH_WINNER" => ResolveMatchWinnerChallenge(challenge, acceptedParticipants, matchesList),
            "EXACT_SCORE" => ResolveExactScoreChallenge(challenge, acceptedParticipants, matchesList),
            "SUDDEN_DEATH" => ResolveSuddenDeathChallenge(challenge, acceptedParticipants, matchesList, picksList),
            "TOTAL_GOALS" => ResolveTotalGoalsChallenge(challenge, acceptedParticipants, matchesList),
            _ => throw new ArgumentException($"Tipo de reto no soportado: {challenge.ChallengeType}")
        };
    }

    /// <summary>
    /// Evalúa en vivo el reto sin modificar estado persistido, completando propiedades
    /// visuales para ChallengeDto y ChallengeParticipantDto (CurrentLiveValue, IsBusted, TakenOptions, etc.).
    /// </summary>
    public void EvaluateLiveState(
        ChallengeDto challengeDto,
        IEnumerable<Match> matches,
        IEnumerable<Pick> picks,
        IEnumerable<MemberStandingDto>? weeklyStandings = null,
        IEnumerable<MemberStandingDto>? generalStandings = null)
    {
        var matchesList = matches.ToList();
        var picksList = picks.ToList();
        var acceptedParticipants = challengeDto.Participants
            .Where(p => p.Status == "ACCEPTED")
            .ToList();

        // 1. Opciones ya ocupadas (TakenOptions)
        challengeDto.TakenOptions = CalculateTakenOptions(challengeDto.ChallengeType, acceptedParticipants);

        // 2. Partidos diferenciales entre participantes
        challengeDto.DifferentialMatches = CalculateDifferentialMatches(acceptedParticipants, picksList, matchesList);

        // 3. Evaluación en vivo por tipo
        switch (challengeDto.ChallengeType)
        {
            case "TOTAL_GOALS":
                EvaluateLiveTotalGoals(challengeDto, acceptedParticipants, matchesList);
                break;

            case "SUDDEN_DEATH":
                EvaluateLiveSuddenDeath(challengeDto, acceptedParticipants, matchesList, picksList);
                break;

            case "MATCH_WINNER":
                EvaluateLiveMatchWinner(challengeDto, acceptedParticipants, matchesList);
                break;

            case "EXACT_SCORE":
                EvaluateLiveExactScore(challengeDto, acceptedParticipants, matchesList);
                break;

            case "WEEKLY_RANK":
                EvaluateLiveWeeklyRank(acceptedParticipants, weeklyStandings);
                break;

            case "GENERAL_RANK":
                EvaluateLiveGeneralRank(acceptedParticipants, generalStandings);
                break;
        }
    }

    #region Resolutores Específicos

    private ChallengeResolutionResult ResolveWeeklyRankChallenge(
        QuinielaChallenge challenge,
        List<QuinielaChallengeParticipant> participants,
        IEnumerable<MemberStandingDto>? weeklyStandings)
    {
        var standingsDict = (weeklyStandings ?? Enumerable.Empty<MemberStandingDto>())
            .ToDictionary(s => s.MemberId, s => s);

        var ordered = participants
            .Select(p => new
            {
                Participant = p,
                Rank = standingsDict.TryGetValue(p.MemberId, out var st) ? st.Rank : int.MaxValue,
                Hits = standingsDict.TryGetValue(p.MemberId, out var stHits) ? stHits.Hits : 0,
                Alias = p.Member?.Alias ?? $"Jugador {p.MemberId}"
            })
            .OrderBy(x => x.Rank)
            .ThenByDescending(x => x.Hits)
            .ToList();

        return DistributeRankOutcomes(challenge, ordered.Select(x => (x.Participant.MemberId, x.Alias, x.Rank, x.Hits)).ToList(), "Tabla Semanal");
    }

    private ChallengeResolutionResult ResolveGeneralRankChallenge(
        QuinielaChallenge challenge,
        List<QuinielaChallengeParticipant> participants,
        IEnumerable<MemberStandingDto>? generalStandings)
    {
        var standingsDict = (generalStandings ?? Enumerable.Empty<MemberStandingDto>())
            .ToDictionary(s => s.MemberId, s => s);

        var ordered = participants
            .Select(p => new
            {
                Participant = p,
                Rank = standingsDict.TryGetValue(p.MemberId, out var st) ? st.Rank : int.MaxValue,
                Hits = standingsDict.TryGetValue(p.MemberId, out var stHits) ? stHits.Hits : 0,
                Alias = p.Member?.Alias ?? $"Jugador {p.MemberId}"
            })
            .OrderBy(x => x.Rank)
            .ThenByDescending(x => x.Hits)
            .ToList();

        return DistributeRankOutcomes(challenge, ordered.Select(x => (x.Participant.MemberId, x.Alias, x.Rank, x.Hits)).ToList(), "Tabla General");
    }

    private ChallengeResolutionResult DistributeRankOutcomes(
        QuinielaChallenge challenge,
        List<(int MemberId, string Alias, int Rank, int Hits)> ordered,
        string tableLabel)
    {
        var outcomes = new Dictionary<int, string>();
        var winners = new List<int>();
        var losers = new List<int>();
        string summary;

        if (challenge.PayoutMode == "LAST_PLACE_PAYS")
        {
            int worstRank = ordered.Max(x => x.Rank);
            var worstParticipants = ordered.Where(x => x.Rank == worstRank).ToList();
            var nonWorstParticipants = ordered.Where(x => x.Rank != worstRank).ToList();

            foreach (var p in worstParticipants)
            {
                outcomes[p.MemberId] = "LOSER";
                losers.Add(p.MemberId);
            }

            foreach (var p in nonWorstParticipants)
            {
                outcomes[p.MemberId] = "WINNER";
                winners.Add(p.MemberId);
            }

            string loserNames = string.Join(", ", worstParticipants.Select(x => x.Alias));
            summary = $"🤡 Modalidad 'El Último Paga': {loserNames} quedó en el fondo en la {tableLabel} y le corresponde pagar la apuesta ({challenge.StakesDescription}).";
        }
        else // WINNER_TAKES_ALL
        {
            int bestRank = ordered.Min(x => x.Rank);
            var bestParticipants = ordered.Where(x => x.Rank == bestRank).ToList();
            var nonBestParticipants = ordered.Where(x => x.Rank != bestRank).ToList();

            foreach (var p in bestParticipants)
            {
                outcomes[p.MemberId] = "WINNER";
                winners.Add(p.MemberId);
            }

            foreach (var p in nonBestParticipants)
            {
                outcomes[p.MemberId] = "LOSER";
                losers.Add(p.MemberId);
            }

            string winnerNames = string.Join(", ", bestParticipants.Select(x => x.Alias));
            summary = $"👑 Modalidad 'El Rey Cobra': {winnerNames} dominó la {tableLabel} (Posición #{bestRank}) y se lleva la apuesta completa: {challenge.StakesDescription}.";
        }

        return new ChallengeResolutionResult
        {
            FinalStatus = "RESOLVED_WITH_WINNER",
            WinnerMemberIds = winners,
            LoserMemberIds = losers,
            ParticipantOutcomes = outcomes,
            ResolutionSummary = summary
        };
    }

    private ChallengeResolutionResult ResolveMatchWinnerChallenge(
        QuinielaChallenge challenge,
        List<QuinielaChallengeParticipant> participants,
        List<Match> matches)
    {
        var targetMatch = matches.FirstOrDefault(m => m.Id == challenge.TargetMatchId);
        string home = targetMatch?.HomeTeam?.Abbreviation ?? "Local";
        string away = targetMatch?.AwayTeam?.Abbreviation ?? "Visita";
        string actualWinner = (targetMatch?.WinnerAbbr ?? string.Empty).Trim().ToUpperInvariant();

        var correctParticipants = participants
            .Where(p => !string.IsNullOrWhiteSpace(p.PredictedWinnerAbbr)
                        && string.Equals(p.PredictedWinnerAbbr.Trim(), actualWinner, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Si nadie acertó al ganador -> Burla editorial y RESOLVED_NO_WINNER
        if (!correctParticipants.Any())
        {
            return new ChallengeResolutionResult
            {
                FinalStatus = "RESOLVED_NO_WINNER",
                ParticipantOutcomes = participants.ToDictionary(p => p.MemberId, _ => "NO_WINNER_BUST"),
                ResolutionSummary = $"🤡 Nadie le atinó en {home} vs {away}: el resultado oficial fue '{actualWinner}' y todos los retadores fallaron su pronóstico. Nadie gana."
            };
        }

        var winners = new List<int>();
        var losers = new List<int>();
        var outcomes = new Dictionary<int, string>();

        if (challenge.PayoutMode == "LAST_PLACE_PAYS")
        {
            var incorrectParticipants = participants.Except(correctParticipants).ToList();
            foreach (var p in incorrectParticipants)
            {
                losers.Add(p.MemberId);
                outcomes[p.MemberId] = "LOSER";
            }
            foreach (var p in correctParticipants)
            {
                winners.Add(p.MemberId);
                outcomes[p.MemberId] = "WINNER";
            }
        }
        else // WINNER_TAKES_ALL
        {
            foreach (var p in correctParticipants)
            {
                winners.Add(p.MemberId);
                outcomes[p.MemberId] = "WINNER";
            }
            foreach (var p in participants.Except(correctParticipants))
            {
                losers.Add(p.MemberId);
                outcomes[p.MemberId] = "LOSER";
            }
        }

        string winnerAliases = string.Join(", ", correctParticipants.Select(p => p.Member?.Alias ?? $"Jugador {p.MemberId}"));
        string summary = $"🥊 ¡Victoria en el Clásico! {winnerAliases} acertó al ganador '{actualWinner}' en {home} vs {away} y gana el reto: {challenge.StakesDescription}.";

        return new ChallengeResolutionResult
        {
            FinalStatus = "RESOLVED_WITH_WINNER",
            WinnerMemberIds = winners,
            LoserMemberIds = losers,
            ParticipantOutcomes = outcomes,
            ResolutionSummary = summary
        };
    }

    private ChallengeResolutionResult ResolveExactScoreChallenge(
        QuinielaChallenge challenge,
        List<QuinielaChallengeParticipant> participants,
        List<Match> matches)
    {
        var targetMatch = matches.FirstOrDefault(m => m.Id == challenge.TargetMatchId);
        string home = targetMatch?.HomeTeam?.Abbreviation ?? "Local";
        string away = targetMatch?.AwayTeam?.Abbreviation ?? "Visita";
        int actualHome = targetMatch?.HomeScore ?? 0;
        int actualAway = targetMatch?.AwayScore ?? 0;

        var correctParticipants = participants
            .Where(p => p.PredictedHomeScore == actualHome && p.PredictedAwayScore == actualAway)
            .ToList();

        // Nadie atinó el marcador exacto -> RESOLVED_NO_WINNER + burla
        if (!correctParticipants.Any())
        {
            return new ChallengeResolutionResult
            {
                FinalStatus = "RESOLVED_NO_WINNER",
                ParticipantOutcomes = participants.ToDictionary(p => p.MemberId, _ => "NO_WINNER_BUST"),
                ResolutionSummary = $"🎯 Marcador imposible: {home} {actualHome} - {actualAway} {away}. ¡Ningún participante le atinó al marcador exacto! Nadie cobra la apuesta."
            };
        }

        var winners = new List<int>();
        var losers = new List<int>();
        var outcomes = new Dictionary<int, string>();

        foreach (var p in correctParticipants)
        {
            winners.Add(p.MemberId);
            outcomes[p.MemberId] = "WINNER";
        }
        foreach (var p in participants.Except(correctParticipants))
        {
            losers.Add(p.MemberId);
            outcomes[p.MemberId] = "LOSER";
        }

        string winnerAliases = string.Join(", ", correctParticipants.Select(p => p.Member?.Alias ?? $"Jugador {p.MemberId}"));
        string summary = $"🎯 ¡Ojo de francotirador! {winnerAliases} clavó el marcador exacto {home} {actualHome} - {actualAway} {away} y se lleva la apuesta: {challenge.StakesDescription}.";

        return new ChallengeResolutionResult
        {
            FinalStatus = "RESOLVED_WITH_WINNER",
            WinnerMemberIds = winners,
            LoserMemberIds = losers,
            ParticipantOutcomes = outcomes,
            ResolutionSummary = summary
        };
    }

    private ChallengeResolutionResult ResolveSuddenDeathChallenge(
        QuinielaChallenge challenge,
        List<QuinielaChallengeParticipant> participants,
        List<Match> matches,
        List<Pick> picks)
    {
        // Orden cronológico estricto de partidos finalizados
        var chronologicalMatches = matches
            .Where(m => string.Equals(m.StatusState, "post", StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(m.WinnerAbbr))
            .OrderBy(m => m.DateUtc)
            .ThenBy(m => m.Id)
            .ToList();

        var picksByMemberAndMatch = picks
            .ToDictionary(p => (p.MemberId, p.MatchId), p => (p.PickAbbr ?? string.Empty).Trim().ToUpperInvariant());

        var aliveMembers = new HashSet<int>(participants.Select(p => p.MemberId));
        var doubleLifeEvents = new List<string>();
        var eliminatedAtMatchIds = new Dictionary<int, int?>();
        var eliminationOrder = new List<(int MatchId, List<int> EliminatedMemberIds)>();

        foreach (var match in chronologicalMatches)
        {
            if (aliveMembers.Count <= 1)
            {
                break;
            }

            string winner = (match.WinnerAbbr ?? string.Empty).Trim().ToUpperInvariant();
            string home = match.HomeTeam?.Abbreviation ?? "LOC";
            string away = match.AwayTeam?.Abbreviation ?? "VIS";

            var hitsInMatch = new List<int>();
            var missesInMatch = new List<int>();

            foreach (var memberId in aliveMembers)
            {
                picksByMemberAndMatch.TryGetValue((memberId, match.Id), out var memberPick);
                if (string.Equals(memberPick, winner, StringComparison.OrdinalIgnoreCase))
                {
                    hitsInMatch.Add(memberId);
                }
                else
                {
                    missesInMatch.Add(memberId);
                }
            }

            // Regla de "❤️‍🔥 Doble Vida": Si TODOS los sobrevivientes fallan el mismo partido, nadie muere
            if (hitsInMatch.Count == 0 && missesInMatch.Count > 0)
            {
                doubleLifeEvents.Add($"❤️‍🔥 Doble Vida en {home} vs {away}: Todos los participantes fallaron y siguen vivos.");
                continue;
            }

            // Si algunos aciertan y otros fallan, los que fallan son eliminados en este partido
            if (hitsInMatch.Count > 0 && missesInMatch.Count > 0)
            {
                foreach (var eliminatedId in missesInMatch)
                {
                    aliveMembers.Remove(eliminatedId);
                    eliminatedAtMatchIds[eliminatedId] = match.Id;
                }
                eliminationOrder.Add((match.Id, missesInMatch));
            }
        }

        var outcomes = new Dictionary<int, string>();
        var winners = new List<int>();
        var losers = new List<int>();
        string summary;

        var memberAliasMap = participants.ToDictionary(p => p.MemberId, p => p.Member?.Alias ?? $"Jugador {p.MemberId}");

        if (aliveMembers.Count == 1)
        {
            int soleSurvivor = aliveMembers.First();
            winners.Add(soleSurvivor);
            outcomes[soleSurvivor] = "WINNER";

            foreach (var p in participants.Where(p => p.MemberId != soleSurvivor))
            {
                losers.Add(p.MemberId);
                outcomes[p.MemberId] = "LOSER";
            }

            string doubleLifeNote = doubleLifeEvents.Any() ? $" (tras resistir {doubleLifeEvents.Count} Doble(s) Vida)" : string.Empty;
            summary = $"💀 ¡Sobreviviente de Muerte Súbita! {memberAliasMap[soleSurvivor]} aguantó hasta el final{doubleLifeNote} y gana la apuesta: {challenge.StakesDescription}.";
        }
        else if (aliveMembers.Count > 1)
        {
            // Varios vivos al final de la jornada: desempatar por aciertos totales en la semana
            var survivorPicks = participants
                .Where(p => aliveMembers.Contains(p.MemberId))
                .Select(p => new
                {
                    MemberId = p.MemberId,
                    Hits = picks.Count(pk => pk.MemberId == p.MemberId && pk.IsHit == true)
                })
                .OrderByDescending(x => x.Hits)
                .ToList();

            int maxHits = survivorPicks.First().Hits;
            var topSurvivors = survivorPicks.Where(x => x.Hits == maxHits).Select(x => x.MemberId).ToList();

            foreach (var topId in topSurvivors)
            {
                winners.Add(topId);
                outcomes[topId] = "WINNER";
            }

            foreach (var p in participants.Where(p => !topSurvivors.Contains(p.MemberId)))
            {
                losers.Add(p.MemberId);
                outcomes[p.MemberId] = "LOSER";
            }

            string winnersNames = string.Join(", ", topSurvivors.Select(id => memberAliasMap[id]));
            summary = $"💀 Muerte Súbita extendida: {winnersNames} sobrevivieron y desempataron con {maxHits} aciertos totales, llevándose la apuesta: {challenge.StakesDescription}.";
        }
        else
        {
            // Caso borde defensivo
            foreach (var p in participants)
            {
                outcomes[p.MemberId] = "NO_WINNER_BUST";
            }
            summary = "💀 Duelo fatal: Todos cayeron al mismo tiempo. Nadie gana la apuesta.";
            return new ChallengeResolutionResult
            {
                FinalStatus = "RESOLVED_NO_WINNER",
                ParticipantOutcomes = outcomes,
                DoubleLifeEvents = doubleLifeEvents,
                EliminatedAtMatchIds = eliminatedAtMatchIds,
                ResolutionSummary = summary
            };
        }

        // Si la modalidad es LAST_PLACE_PAYS, el primero en caer es el único perdedor
        if (challenge.PayoutMode == "LAST_PLACE_PAYS" && eliminationOrder.Any())
        {
            var firstEliminatedBatch = eliminationOrder.First().EliminatedMemberIds;
            losers.Clear();
            winners.Clear();

            foreach (var p in participants)
            {
                if (firstEliminatedBatch.Contains(p.MemberId))
                {
                    losers.Add(p.MemberId);
                    outcomes[p.MemberId] = "LOSER";
                }
                else
                {
                    winners.Add(p.MemberId);
                    outcomes[p.MemberId] = "WINNER";
                }
            }

            string loserNames = string.Join(", ", firstEliminatedBatch.Select(id => memberAliasMap[id]));
            summary = $"🤡 Modalidad 'El Último Paga': {loserNames} fue el primero en caer en Muerte Súbita y debe pagar la apuesta ({challenge.StakesDescription}).";
        }

        return new ChallengeResolutionResult
        {
            FinalStatus = "RESOLVED_WITH_WINNER",
            WinnerMemberIds = winners,
            LoserMemberIds = losers,
            ParticipantOutcomes = outcomes,
            EliminatedAtMatchIds = eliminatedAtMatchIds,
            DoubleLifeEvents = doubleLifeEvents,
            ResolutionSummary = summary
        };
    }

    private ChallengeResolutionResult ResolveTotalGoalsChallenge(
        QuinielaChallenge challenge,
        List<QuinielaChallengeParticipant> participants,
        List<Match> matches)
    {
        int actualTotalGoals = matches.Sum(m => m.HomeScore + m.AwayScore);

        var evaluations = participants.Select(p =>
        {
            int pred = p.PredictedTotalGoals ?? 0;
            bool isBusted = pred > actualTotalGoals;
            int diff = actualTotalGoals - pred; // diff >= 0 si no se pasó
            return new
            {
                Participant = p,
                PredictedGoals = pred,
                IsBusted = isBusted,
                Diff = diff
            };
        }).ToList();

        // Regla estricta Price is Right: si TODOS se pasaron -> Burla y RESOLVED_NO_WINNER
        if (evaluations.All(e => e.IsBusted))
        {
            return new ChallengeResolutionResult
            {
                FinalStatus = "RESOLVED_NO_WINNER",
                ParticipantOutcomes = participants.ToDictionary(p => p.MemberId, _ => "NO_WINNER_BUST"),
                ResolutionSummary = $"🤡 Todos se pasaron de optimistas: Hubo {actualTotalGoals} goles en la jornada y todos los participantes predijeron más goles. Nadie gana la apuesta.",
                ActualTotalGoals = actualTotalGoals
            };
        }

        var validContenders = evaluations.Where(e => !e.IsBusted).OrderBy(e => e.Diff).ToList();
        int minDiff = validContenders.First().Diff;
        var bestContenders = validContenders.Where(e => e.Diff == minDiff).ToList();

        var winners = new List<int>();
        var losers = new List<int>();
        var outcomes = new Dictionary<int, string>();

        if (challenge.PayoutMode == "LAST_PLACE_PAYS")
        {
            // El perdedor es el que más se pasó, o el que más lejos quedó
            var worstContender = evaluations.OrderByDescending(e => e.IsBusted ? 1 : 0)
                                            .ThenByDescending(e => e.IsBusted ? (e.PredictedGoals - actualTotalGoals) : e.Diff)
                                            .First();

            losers.Add(worstContender.Participant.MemberId);
            outcomes[worstContender.Participant.MemberId] = "LOSER";

            foreach (var e in evaluations.Where(x => x.Participant.MemberId != worstContender.Participant.MemberId))
            {
                winners.Add(e.Participant.MemberId);
                outcomes[e.Participant.MemberId] = "WINNER";
            }

            string loserAlias = worstContender.Participant.Member?.Alias ?? $"Jugador {worstContender.Participant.MemberId}";
            string reason = worstContender.IsBusted ? $"se pasó con {worstContender.PredictedGoals} goles" : $"se quedó muy lejos con {worstContender.PredictedGoals} goles";
            string summary = $"🤡 Modalidad 'El Último Paga': {loserAlias} {reason} (Total real: {actualTotalGoals} goles) y le toca pagar la apuesta: {challenge.StakesDescription}.";

            return new ChallengeResolutionResult
            {
                FinalStatus = "RESOLVED_WITH_WINNER",
                WinnerMemberIds = winners,
                LoserMemberIds = losers,
                ParticipantOutcomes = outcomes,
                ResolutionSummary = summary,
                ActualTotalGoals = actualTotalGoals
            };
        }
        else // WINNER_TAKES_ALL
        {
            foreach (var b in bestContenders)
            {
                winners.Add(b.Participant.MemberId);
                outcomes[b.Participant.MemberId] = "WINNER";
            }

            foreach (var e in evaluations.Except(bestContenders))
            {
                losers.Add(e.Participant.MemberId);
                outcomes[e.Participant.MemberId] = "LOSER";
            }

            string winnerNames = string.Join(", ", bestContenders.Select(b => b.Participant.Member?.Alias ?? $"Jugador {b.Participant.MemberId}"));
            string summary = $"⚽ ¡Cálculo de cirujano! Con un total de {actualTotalGoals} goles en la jornada, {winnerNames} ganó con {bestContenders.First().PredictedGoals} goles sin pasarse. Se lleva la apuesta: {challenge.StakesDescription}.";

            return new ChallengeResolutionResult
            {
                FinalStatus = "RESOLVED_WITH_WINNER",
                WinnerMemberIds = winners,
                LoserMemberIds = losers,
                ParticipantOutcomes = outcomes,
                ResolutionSummary = summary,
                ActualTotalGoals = actualTotalGoals
            };
        }
    }

    #endregion

    #region Evaluación en Vivo (Live State)

    private void EvaluateLiveTotalGoals(
        ChallengeDto challengeDto,
        List<ChallengeParticipantDto> participants,
        List<Match> matches)
    {
        int currentGoals = matches.Sum(m => m.HomeScore + m.AwayScore);
        challengeDto.ActualTotalGoals = currentGoals;

        foreach (var p in participants)
        {
            int pred = p.PredictedTotalGoals ?? 0;
            if (currentGoals > pred)
            {
                p.IsBusted = true;
                p.CurrentLiveValue = $"💥 Se pasó ({pred} goles, van {currentGoals})";
            }
            else
            {
                p.IsBusted = false;
                p.CurrentLiveValue = $"Predijo {pred} goles (Faltan {pred - currentGoals})";
            }
        }
    }

    private void EvaluateLiveSuddenDeath(
        ChallengeDto challengeDto,
        List<ChallengeParticipantDto> participants,
        List<Match> matches,
        List<Pick> picks)
    {
        var chronologicalMatches = matches
            .OrderBy(m => m.DateUtc)
            .ThenBy(m => m.Id)
            .ToList();

        var picksByMemberAndMatch = picks
            .ToDictionary(p => (p.MemberId, p.MatchId), p => (p.PickAbbr ?? string.Empty).Trim().ToUpperInvariant());

        var aliveMembers = new HashSet<int>(participants.Select(p => p.MemberId));
        var doubleLifeEvents = new List<string>();

        foreach (var match in chronologicalMatches)
        {
            if (!string.Equals(match.StatusState, "post", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(match.WinnerAbbr))
            {
                continue;
            }

            if (aliveMembers.Count <= 1) break;

            string winner = match.WinnerAbbr.Trim().ToUpperInvariant();
            string home = match.HomeTeam?.Abbreviation ?? "LOC";
            string away = match.AwayTeam?.Abbreviation ?? "VIS";

            var hits = new List<int>();
            var misses = new List<int>();

            foreach (var mId in aliveMembers)
            {
                picksByMemberAndMatch.TryGetValue((mId, match.Id), out var pPick);
                if (string.Equals(pPick, winner, StringComparison.OrdinalIgnoreCase))
                    hits.Add(mId);
                else
                    misses.Add(mId);
            }

            if (hits.Count == 0 && misses.Count > 0)
            {
                doubleLifeEvents.Add($"❤️‍🔥 Doble Vida en {home} vs {away}");
                continue;
            }

            if (hits.Count > 0 && misses.Count > 0)
            {
                foreach (var eliminatedId in misses)
                {
                    aliveMembers.Remove(eliminatedId);
                    var pDto = participants.FirstOrDefault(p => p.MemberId == eliminatedId);
                    if (pDto != null)
                    {
                        pDto.EliminatedAtMatchId = match.Id;
                        pDto.EliminatedAtMatchLabel = $"{home} vs {away}";
                    }
                }
            }
        }

        challengeDto.DoubleLifeEvents = doubleLifeEvents;

        foreach (var p in participants)
        {
            if (aliveMembers.Contains(p.MemberId))
            {
                p.CurrentLiveValue = "❤️ Vivo en el duelo";
            }
            else
            {
                p.CurrentLiveValue = $"💀 Eliminado en {p.EliminatedAtMatchLabel ?? "partido anterior"}";
            }
        }
    }

    private void EvaluateLiveMatchWinner(
        ChallengeDto challengeDto,
        List<ChallengeParticipantDto> participants,
        List<Match> matches)
    {
        var targetMatch = matches.FirstOrDefault(m => m.Id == challengeDto.TargetMatchId);
        if (targetMatch != null)
        {
            challengeDto.TargetMatchLabel = $"{targetMatch.HomeTeam?.Abbreviation ?? "LOC"} vs {targetMatch.AwayTeam?.Abbreviation ?? "VIS"}";
            challengeDto.TargetMatchScore = targetMatch.StatusState != "pre"
                ? $"{targetMatch.HomeScore} - {targetMatch.AwayScore}"
                : "vs";

            string? currentWinner = targetMatch.WinnerAbbr?.Trim().ToUpperInvariant();

            foreach (var p in participants)
            {
                string pred = (p.PredictedWinnerAbbr ?? string.Empty).Trim().ToUpperInvariant();
                if (targetMatch.StatusState == "post" && !string.IsNullOrWhiteSpace(currentWinner))
                {
                    p.CurrentLiveValue = string.Equals(pred, currentWinner, StringComparison.OrdinalIgnoreCase)
                        ? "✅ Acertó al ganador"
                        : "❌ Falló el pronóstico";
                }
                else if (targetMatch.StatusState == "in")
                {
                    p.CurrentLiveValue = $"⚡ En juego ({pred})";
                }
                else
                {
                    p.CurrentLiveValue = $"Pronóstico: {pred}";
                }
            }
        }
    }

    private void EvaluateLiveExactScore(
        ChallengeDto challengeDto,
        List<ChallengeParticipantDto> participants,
        List<Match> matches)
    {
        var targetMatch = matches.FirstOrDefault(m => m.Id == challengeDto.TargetMatchId);
        if (targetMatch != null)
        {
            challengeDto.TargetMatchLabel = $"{targetMatch.HomeTeam?.Abbreviation ?? "LOC"} vs {targetMatch.AwayTeam?.Abbreviation ?? "VIS"}";
            challengeDto.TargetMatchScore = targetMatch.StatusState != "pre"
                ? $"{targetMatch.HomeScore} - {targetMatch.AwayScore}"
                : "vs";

            foreach (var p in participants)
            {
                if (targetMatch.StatusState == "post")
                {
                    bool exact = p.PredictedHomeScore == targetMatch.HomeScore && p.PredictedAwayScore == targetMatch.AwayScore;
                    p.CurrentLiveValue = exact ? "🎯 ¡Marcador Exacto!" : "❌ No acertó el marcador";
                }
                else
                {
                    p.CurrentLiveValue = $"Pronóstico: {p.PredictedHomeScore} - {p.PredictedAwayScore}";
                }
            }
        }
    }

    private void EvaluateLiveWeeklyRank(
        List<ChallengeParticipantDto> participants,
        IEnumerable<MemberStandingDto>? weeklyStandings)
    {
        var dict = (weeklyStandings ?? Enumerable.Empty<MemberStandingDto>()).ToDictionary(s => s.MemberId, s => s);
        foreach (var p in participants)
        {
            if (dict.TryGetValue(p.MemberId, out var st))
            {
                p.CurrentLiveValue = $"Posición #{st.Rank} ({st.Hits} aciertos)";
            }
        }
    }

    private void EvaluateLiveGeneralRank(
        List<ChallengeParticipantDto> participants,
        IEnumerable<MemberStandingDto>? generalStandings)
    {
        var dict = (generalStandings ?? Enumerable.Empty<MemberStandingDto>()).ToDictionary(s => s.MemberId, s => s);
        foreach (var p in participants)
        {
            if (dict.TryGetValue(p.MemberId, out var st))
            {
                p.CurrentLiveValue = $"General #{st.Rank} ({st.Hits} aciertos acumulados)";
            }
        }
    }

    #endregion

    #region Utilidades de Bloqueo y Diferenciales

    public List<string> CalculateTakenOptions(string challengeType, IEnumerable<ChallengeParticipantDto> participants)
    {
        var list = new List<string>();
        foreach (var p in participants.Where(p => p.Status == "ACCEPTED"))
        {
            switch (challengeType)
            {
                case "MATCH_WINNER" when !string.IsNullOrWhiteSpace(p.PredictedWinnerAbbr):
                    list.Add(p.PredictedWinnerAbbr.Trim().ToUpperInvariant());
                    break;
                case "EXACT_SCORE" when p.PredictedHomeScore != null && p.PredictedAwayScore != null:
                    list.Add($"{p.PredictedHomeScore}-{p.PredictedAwayScore}");
                    break;
                case "TOTAL_GOALS" when p.PredictedTotalGoals != null:
                    list.Add(p.PredictedTotalGoals.Value.ToString());
                    break;
            }
        }
        return list;
    }

    public List<string> CalculateDifferentialMatches(
        List<ChallengeParticipantDto> participants,
        List<Pick> picks,
        List<Match> matches)
    {
        if (participants.Count < 2) return new List<string>();

        var memberIds = participants.Select(p => p.MemberId).ToHashSet();
        var relevantPicks = picks.Where(p => memberIds.Contains(p.MemberId)).ToList();
        var picksByMatch = relevantPicks.GroupBy(p => p.MatchId);

        var differentialLabels = new List<string>();
        var matchesDict = matches.ToDictionary(m => m.Id, m => m);

        foreach (var group in picksByMatch)
        {
            var distinctChoices = group.Select(p => (p.PickAbbr ?? string.Empty).Trim().ToUpperInvariant()).Distinct().ToList();
            if (distinctChoices.Count > 1 && matchesDict.TryGetValue(group.Key, out var match))
            {
                string home = match.HomeTeam?.Abbreviation ?? "LOC";
                string away = match.AwayTeam?.Abbreviation ?? "VIS";
                differentialLabels.Add($"{home} vs {away}");
            }
        }

        return differentialLabels;
    }

    #endregion
}
