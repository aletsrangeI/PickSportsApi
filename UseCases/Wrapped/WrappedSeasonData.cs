namespace UseCases.Wrapped;

/// <summary>
/// SPEC-014: proyeccion de solo lectura sobre la entrada del motor. Precalcula agregados (ranking,
/// jornadas, equipos, parejas) para que las historias y los arquetipos sean deterministas y no
/// dependan de EF, Random ni DateTime.Now.
/// </summary>
internal sealed class WrappedSeasonData
{
    /// <summary>Abreviatura con la que el dominio marca el empate.</summary>
    public const string DrawAbbr = "EMPATE";

    /// <summary>Las 6 abreviaturas que el plan fija como equipos "grandes" (fallback de S-14).</summary>
    public static readonly string[] BigTeams = { "AME", "GDL", "MTY", "UANL", "UNAM", "CAZ" };

    internal sealed record TeamUsage(string Abbr, int Picks, int Hits, int Misses)
    {
        public double Accuracy => Picks == 0 ? 0d : (double)Hits / Picks;
    }

    internal sealed record PairStat(
        WrappedMemberInfo Member,
        int Common,
        int Coincident,
        int SharedDraws,
        int PositionChanges)
    {
        public double CoincidenceRatio => Common == 0 ? 0d : (double)Coincident / Common;
        public double DiscrepancyRatio => 1d - CoincidenceRatio;
        public int Discrepant => Common - Coincident;
    }

    private readonly Dictionary<int, List<WrappedPickInfo>> _picksByMember = new();
    private readonly Dictionary<int, List<WrappedPickInfo>> _picksByMatch = new();
    private readonly Dictionary<(int MemberId, int Week), int> _hitsByWeek = new();
    private readonly Dictionary<(int MemberId, int Week), int> _upsetsByWeek = new();
    private readonly Dictionary<(int MemberId, int Week), int> _picksByWeek = new();
    private readonly Dictionary<(int MemberId, int Week), int> _weeklyRanks = new();
    private readonly Dictionary<int, int> _hitsTotal = new();
    private readonly Dictionary<string, TeamUsage> _groupTeams = new(StringComparer.OrdinalIgnoreCase);

    public WrappedSeasonData(WrappedEngineInput input)
    {
        Input = input;

        MatchesById = input.Matches
            .Where(m => !string.IsNullOrWhiteSpace(m.WinnerAbbr))
            .GroupBy(m => m.MatchId)
            .ToDictionary(g => g.Key, g => g.First());

        PlayedMatches = MatchesById.Values
            .OrderBy(m => m.DateUtc)
            .ThenBy(m => m.MatchId)
            .ToList();

        var members = input.Members.ToList();
        var target = members.FirstOrDefault(m => m.MemberId == input.TargetMemberId);
        if (target is null)
        {
            // Defensivo: el motor nunca debe reventar por un miembro ausente del listado.
            target = new WrappedMemberInfo(input.TargetMemberId, 0, "Jugador", null, 0, 0, 0, 0, 0);
            members.Add(target);
        }

        Members = members;
        Target = target;
        TotalMembers = Math.Max(1, members.Count);

        var ranking = members
            .OrderByDescending(m => m.TotalPoints)
            .ThenByDescending(m => m.TotalHits)
            .ThenByDescending(m => m.TotalUpsets)
            .ThenBy(m => m.TotalHumillaciones)
            .ThenBy(m => m.MemberId)
            .ToList();

        Ranking = ranking;
        FinalPosition = ranking.FindIndex(m => m.MemberId == target.MemberId) + 1;
        if (FinalPosition <= 0) FinalPosition = TotalMembers;

        // SPEC-014 robustez: un pick huerfano (MatchId ausente de input.Matches) o de un partido sin
        // resultado se DESCARTA antes de cualquier agregado. Nunca debe provocar KeyNotFoundException
        // ni sumar aciertos/sorpresas/rachas; el resultado debe ser identico al de la entrada sin ese pick.
        Picks = input.Picks
            .Where(p => p.IsHit.HasValue && MatchesById.ContainsKey(p.MatchId))
            .ToList();

        Weeks = MatchesById.Values
            .Select(m => m.WeekNumber)
            .Distinct()
            .OrderBy(w => w)
            .ToList();

        foreach (var pick in Picks)
        {
            if (!MatchesById.TryGetValue(pick.MatchId, out var match)) continue;

            AddToGroup(_picksByMember, pick.MemberId, pick);
            AddToGroup(_picksByMatch, pick.MatchId, pick);

            var week = match.WeekNumber;
            Bump(_picksByWeek, (pick.MemberId, week));

            if (pick.IsHit == true)
            {
                Bump(_hitsByWeek, (pick.MemberId, week));
                _hitsTotal[pick.MemberId] = _hitsTotal.GetValueOrDefault(pick.MemberId) + 1;
            }

            if (pick.IsUpsetHit) Bump(_upsetsByWeek, (pick.MemberId, week));
        }

        BuildWeeklyRanks();
        BuildGroupTeams();
    }

    public WrappedEngineInput Input { get; }
    public IReadOnlyDictionary<int, WrappedMatchInfo> MatchesById { get; }
    public IReadOnlyList<WrappedMatchInfo> PlayedMatches { get; }
    public IReadOnlyList<WrappedMemberInfo> Members { get; }
    public IReadOnlyList<WrappedMemberInfo> Ranking { get; }
    public WrappedMemberInfo Target { get; }
    public int TotalMembers { get; }
    public int FinalPosition { get; }
    public IReadOnlyList<WrappedPickInfo> Picks { get; }
    public IReadOnlyList<int> Weeks { get; }
    public IReadOnlyDictionary<string, TeamUsage> GroupTeams => _groupTeams;

    /// <summary>Normaliza abreviaturas/pronosticos para comparaciones estables.</summary>
    public static string Normalize(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();

    public IReadOnlyList<WrappedPickInfo> PicksOf(int memberId) =>
        _picksByMember.TryGetValue(memberId, out var list) ? list : Array.Empty<WrappedPickInfo>();

    public IReadOnlyList<WrappedPickInfo> HitsOf(int memberId) =>
        PicksOf(memberId).Where(p => p.IsHit == true).ToList();

    public int PickCountOf(int memberId) => PicksOf(memberId).Count;

    public int HitsOfTotal(int memberId) => _hitsTotal.GetValueOrDefault(memberId);

    /// <summary>
    /// Partido asociado a un pick. Precondicion garantizada por el constructor: <see cref="Picks"/> solo
    /// contiene MatchId presentes en <see cref="MatchesById"/>, por lo que los picks huerfanos jamas llegan aqui.
    /// </summary>
    public WrappedMatchInfo MatchOf(int matchId) => MatchesById[matchId];

    public int TotalGoals(WrappedMatchInfo match) => match.HomeScore + match.AwayScore;

    public bool IsDrawPick(WrappedPickInfo pick) => Normalize(pick.PickAbbr) == DrawAbbr;

    public int VotesInMatch(int matchId) =>
        _picksByMatch.TryGetValue(matchId, out var list) ? list.Count : 0;

    public int HitsInMatch(int matchId) =>
        _picksByMatch.TryGetValue(matchId, out var list) ? list.Count(p => p.IsHit == true) : 0;

    public double HitRateInMatch(int matchId)
    {
        var votes = VotesInMatch(matchId);
        return votes == 0 ? 0d : (double)HitsInMatch(matchId) / votes;
    }

    public int HitsInWeek(int memberId, int week) => _hitsByWeek.GetValueOrDefault((memberId, week));

    public int PicksInWeek(int memberId, int week) => _picksByWeek.GetValueOrDefault((memberId, week));

    public int WeeklyRank(int memberId, int week) => _weeklyRanks.GetValueOrDefault((memberId, week), TotalMembers);

    public IReadOnlyList<int> PlayedWeeksOf(int memberId) =>
        Weeks.Where(w => PicksInWeek(memberId, w) > 0).ToList();

    public int WeeksPlayed(int memberId) => PlayedWeeksOf(memberId).Count;

    public int WeeksInTop5(int memberId) => Weeks.Count(w => WeeklyRank(memberId, w) <= 5);

    public int BestWeekNumber(int memberId)
    {
        var weeks = PlayedWeeksOf(memberId);
        if (weeks.Count == 0) return 0;
        return weeks
            .OrderByDescending(w => HitsInWeek(memberId, w))
            .ThenBy(w => w)
            .First();
    }

    public int BestWeekHits(int memberId)
    {
        var week = BestWeekNumber(memberId);
        return week == 0 ? 0 : HitsInWeek(memberId, week);
    }

    public int WorstWeekNumber(int memberId)
    {
        var weeks = PlayedWeeksOf(memberId);
        if (weeks.Count == 0) return 0;
        return weeks
            .OrderBy(w => HitsInWeek(memberId, w))
            .ThenBy(w => w)
            .First();
    }

    public double WeeklyStdDev(int memberId)
    {
        if (Weeks.Count < 2) return 0d;
        var values = Weeks.Select(w => (double)HitsInWeek(memberId, w)).ToList();
        var mean = values.Average();
        return Math.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / values.Count);
    }

    public int PositionChanges(int memberId, int otherMemberId)
    {
        var changes = 0;
        int? previous = null;

        foreach (var week in Weeks)
        {
            var sign = Math.Sign(WeeklyRank(memberId, week) - WeeklyRank(otherMemberId, week));
            if (sign == 0) continue;
            if (previous.HasValue && previous.Value != sign) changes++;
            previous = sign;
        }

        return changes;
    }

    public int MidSeasonRank(int memberId)
    {
        if (Weeks.Count == 0) return TotalMembers;

        var half = Math.Max(1, (int)Math.Ceiling(Weeks.Count / 2d));
        var firstHalf = Weeks.Take(half).ToList();

        var ordered = Members
            .Select(m => new
            {
                m.MemberId,
                Score = firstHalf.Sum(w => HitsInWeek(m.MemberId, w))
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.MemberId)
            .ToList();

        var index = ordered.FindIndex(x => x.MemberId == memberId);
        return index < 0 ? TotalMembers : index + 1;
    }

    public int AutofilledWeeks(int memberId) =>
        PicksOf(memberId)
            .GroupBy(p => MatchOf(p.MatchId).WeekNumber)
            .Count(g => g.All(p => p.IsAutoFilled));

    public int AutofilledWeeksWithSuccess(int memberId) =>
        PicksOf(memberId)
            .GroupBy(p => MatchOf(p.MatchId).WeekNumber)
            .Count(g => g.All(p => p.IsAutoFilled) && g.Any(p => p.IsHit == true));

    public int AwardCount(int memberId) => Input.Awards.Count(a => a.MemberId == memberId);

    /// <summary>Consumo de un miembro por equipo (solo equipos, nunca EMPATE).</summary>
    public Dictionary<string, TeamUsage> TeamsOf(int memberId)
    {
        var accumulator = new Dictionary<string, (int Picks, int Hits, int Misses)>(StringComparer.OrdinalIgnoreCase);

        foreach (var pick in PicksOf(memberId))
        {
            var abbr = Normalize(pick.PickAbbr);
            if (abbr.Length == 0 || abbr == DrawAbbr) continue;

            var current = accumulator.GetValueOrDefault(abbr);
            current.Picks++;
            if (pick.IsHit == true) current.Hits++;
            else if (pick.IsHit == false) current.Misses++;
            accumulator[abbr] = current;
        }

        return accumulator.ToDictionary(
            kv => kv.Key,
            kv => new TeamUsage(kv.Key, kv.Value.Picks, kv.Value.Hits, kv.Value.Misses),
            StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<PairStat> PairsOf(int memberId)
    {
        var mine = PicksOf(memberId)
            .GroupBy(p => p.MatchId)
            .ToDictionary(g => g.Key, g => g.First());

        var result = new List<PairStat>();

        foreach (var other in Members.Where(m => m.MemberId != memberId))
        {
            var theirs = PicksOf(other.MemberId)
                .GroupBy(p => p.MatchId)
                .ToDictionary(g => g.Key, g => g.First());

            var common = 0;
            var coincident = 0;
            var sharedDraws = 0;

            foreach (var (matchId, myPick) in mine)
            {
                if (!theirs.TryGetValue(matchId, out var otherPick)) continue;

                common++;
                var a = Normalize(myPick.PickAbbr);
                var b = Normalize(otherPick.PickAbbr);

                if (a == b) coincident++;
                if (a == DrawAbbr && b == DrawAbbr) sharedDraws++;
            }

            result.Add(new PairStat(other, common, coincident, sharedDraws, PositionChanges(memberId, other.MemberId)));
        }

        return result;
    }

    /// <summary>Promedio grupal de empates apostados (proporcion 0..1) sobre miembros con picks.</summary>
    public double AverageDrawShare()
    {
        var shares = new List<double>();

        foreach (var member in Members)
        {
            var picks = PicksOf(member.MemberId);
            if (picks.Count == 0) continue;
            shares.Add(picks.Count(IsDrawPick) / (double)picks.Count);
        }

        return shares.Count == 0 ? 0d : shares.Average();
    }

    public double AverageDrawHits() =>
        Members.Count == 0 ? 0d : Members.Average(m => DrawHits(m.MemberId));

    public int DrawHits(int memberId) =>
        PicksOf(memberId).Count(p => p.IsHit == true && IsDrawPick(p));

    public int DrawsPicked(int memberId) => PicksOf(memberId).Count(IsDrawPick);

    /// <summary>Percentil 90 (nearest-index sobre posiciones, 0-based) de empates acertados por miembro.</summary>
    public int P90DrawHits()
    {
        var values = Members.Select(m => DrawHits(m.MemberId)).OrderBy(v => v).ToList();
        if (values.Count == 0) return 0;

        var index = (int)Math.Floor(0.9 * (values.Count - 1));
        return values[index];
    }

    public int TargetHomeHits()
    {
        var memberId = Target.MemberId;
        return HitsOf(memberId).Count(p => string.Equals(
            Normalize(p.PickAbbr), Normalize(MatchOf(p.MatchId).HomeAbbr), StringComparison.Ordinal));
    }

    public int TargetAwayHits()
    {
        var memberId = Target.MemberId;
        return HitsOf(memberId).Count(p => string.Equals(
            Normalize(p.PickAbbr), Normalize(MatchOf(p.MatchId).AwayAbbr), StringComparison.Ordinal));
    }

    /// <summary>Racha mas larga de aciertos consecutivos (orden por DateUtc y luego MatchId).</summary>
    public int LongestStreak(int memberId)
    {
        var ordered = PicksOf(memberId)
            .OrderBy(p => MatchOf(p.MatchId).DateUtc)
            .ThenBy(p => p.MatchId)
            .ToList();

        var best = 0;
        var current = 0;

        foreach (var pick in ordered)
        {
            if (pick.IsHit == true)
            {
                current++;
                if (current > best) best = current;
            }
            else
            {
                current = 0;
            }
        }

        return best;
    }

    private void BuildWeeklyRanks()
    {
        foreach (var week in Weeks)
        {
            var ordered = Members
                .OrderByDescending(m => HitsInWeek(m.MemberId, week))
                .ThenByDescending(m => _upsetsByWeek.GetValueOrDefault((m.MemberId, week)))
                .ThenBy(m => m.MemberId)
                .ToList();

            for (var i = 0; i < ordered.Count; i++)
            {
                _weeklyRanks[(ordered[i].MemberId, week)] = i + 1;
            }
        }
    }

    private void BuildGroupTeams()
    {
        var accumulator = new Dictionary<string, (int Picks, int Hits, int Misses)>(StringComparer.OrdinalIgnoreCase);

        foreach (var pick in Picks)
        {
            var abbr = Normalize(pick.PickAbbr);
            if (abbr.Length == 0 || abbr == DrawAbbr) continue;

            var current = accumulator.GetValueOrDefault(abbr);
            current.Picks++;
            if (pick.IsHit == true) current.Hits++;
            else if (pick.IsHit == false) current.Misses++;
            accumulator[abbr] = current;
        }

        foreach (var kv in accumulator)
        {
            _groupTeams[kv.Key] = new TeamUsage(kv.Key, kv.Value.Picks, kv.Value.Hits, kv.Value.Misses);
        }
    }

    private static void AddToGroup<TKey>(Dictionary<TKey, List<WrappedPickInfo>> groups, TKey key, WrappedPickInfo pick)
        where TKey : notnull
    {
        if (!groups.TryGetValue(key, out var list))
        {
            list = new List<WrappedPickInfo>();
            groups[key] = list;
        }

        list.Add(pick);
    }

    private static void Bump(Dictionary<(int MemberId, int Week), int> counters, (int MemberId, int Week) key) =>
        counters[key] = counters.GetValueOrDefault(key) + 1;
}
