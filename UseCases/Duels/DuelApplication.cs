using Common;
using Domain.Entities;
using Domain.Gamification;
using DTO.Duels;
using Interface.Persistence;
using Interface.UseCases;
using UseCases.Scoring;

namespace UseCases.Duels;

/// <summary>SPEC-013 Etapa 2: calendario Round Robin, resolucion semanal, tabla de duelos, matchup y Salon de la Fama.</summary>
public class DuelApplication : IDuelApplication
{
    /// <summary>Diferencia minima de posiciones en la Tabla General para que aplique la Ventaja del Retador.</summary>
    public const int ChallengerAdvantageGap = 3;

    public const int MaxCupNameLength = 60;
    public const int MaxPunishmentLength = 200;

    private const int WinPoints = 3;
    private const int DrawPoints = 1;
    private const string HallOfFameTitle = "Gladiador Legendario";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IAchievementApplication _achievementApplication;
    private readonly ScoringEngine _scoringEngine;

    public DuelApplication(
        IUnitOfWork unitOfWork,
        IAchievementApplication achievementApplication,
        ScoringEngine? scoringEngine = null)
    {
        _unitOfWork = unitOfWork;
        _achievementApplication = achievementApplication;
        _scoringEngine = scoringEngine ?? new ScoringEngine();
    }

    // ---------------------------------------------------------------- Calendario

    public async Task<Response<DuelScheduleDto>> ScheduleAsync(int quinielaId, int userId)
    {
        var response = new Response<DuelScheduleDto>();

        Response<DuelScheduleDto> Fail(string message)
        {
            response.isSuccess = false;
            response.Message = message;
            return response;
        }

        var quiniela = await _unitOfWork.Quinielas.GetAsync(quinielaId);
        if (quiniela == null) return Fail("La quiniela no existe.");

        var membership = await _unitOfWork.QuinielaMembers.GetMembershipAsync(quinielaId, userId);
        var canManage = membership != null && (membership.Role == "OWNER" || membership.Role == "ADMIN");
        if (!canManage)
        {
            var user = await _unitOfWork.Users.GetAsync(userId);
            canManage = user != null && user.Role == "ADMIN";
        }
        if (!canManage) return Fail("Solo el Owner o un Admin puede generar los duelos.");

        if (!quiniela.GamificationEnabled) return Fail("La gamificación no está activada en esta quiniela.");
        if (quiniela.SeasonId == null) return Fail("La quiniela no tiene temporada asignada.");

        var league = quiniela.LeagueId.HasValue ? await _unitOfWork.Leagues.GetAsync(quiniela.LeagueId.Value) : null;
        if (league == null || league.WeeksCount <= 0) return Fail("La liga no tiene jornadas regulares definidas.");

        var members = (await _unitOfWork.QuinielaMembers.GetMembersAsync(quinielaId))
            .OrderBy(m => m.Id)
            .ToList();
        if (members.Count < 2) return Fail("Se necesitan al menos 2 miembros para generar duelos.");

        if (await _unitOfWork.Duels.AnyResolvedAsync(quinielaId))
            return Fail("Ya hay duelos resueltos; no se puede regenerar el calendario.");

        var seed = Random.Shared.Next();
        var schedule = RoundRobinScheduler.Generate(members.Select(m => m.Id).ToList(), league.WeeksCount, seed);

        foreach (var fixture in schedule.Fixtures) fixture.QuinielaId = quinielaId;

        // Regenerar conserva la copa y el castigo ya fijados; el campeon y el cierre se reinician.
        QuinielaDuelSeason info;
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var existing = await _unitOfWork.Duels.GetSeasonInfoAsync(quinielaId);
            info = new QuinielaDuelSeason
            {
                QuinielaId = quinielaId,
                Seed = seed,
                GeneratedAt = DateTime.UtcNow,
                ChampionMemberId = null,
                ClosedAt = null,
                CupName = existing?.CupName,
                PunishmentText = existing?.PunishmentText,
            };

            await _unitOfWork.Duels.DeleteFixturesAsync(quinielaId);
            await _unitOfWork.Duels.InsertFixturesAsync(schedule.Fixtures);
            await _unitOfWork.Duels.UpsertSeasonInfoAsync(info);
            await _unitOfWork.CommitTransactionAsync();
        }
        catch (Exception)
        {
            await _unitOfWork.RollbackTransactionAsync();
            return Fail("Error al generar el calendario de duelos. No se modificó el calendario anterior.");
        }

        response.isSuccess = true;
        response.Message = "Calendario de duelos generado.";
        response.Data = BuildScheduleDto(
            quinielaId, league.WeeksCount, schedule.RoundsPerLeg, schedule.Legs,
            schedule.DuelWeeks, schedule.RestWeeks, info);
        return response;
    }

    public async Task<Response<DuelScheduleDto>> GetScheduleAsync(int quinielaId, int userId)
    {
        var response = new Response<DuelScheduleDto>();
        var (quiniela, error) = await EnsureAccessAsync(quinielaId, userId);
        if (quiniela == null)
        {
            response.isSuccess = false;
            response.Message = error!;
            return response;
        }

        var fixtures = (await _unitOfWork.Duels.GetFixturesAsync(quinielaId)).ToList();
        var info = await _unitOfWork.Duels.GetSeasonInfoAsync(quinielaId);
        var league = quiniela.LeagueId.HasValue ? await _unitOfWork.Leagues.GetAsync(quiniela.LeagueId.Value) : null;
        var totalWeeks = league?.WeeksCount ?? 0;

        var duelWeeks = fixtures.Select(f => f.WeekNumber).Distinct().OrderBy(w => w).ToList();
        var restWeeks = fixtures.Count == 0
            ? new List<int>()
            : Enumerable.Range(1, totalWeeks).Where(w => !duelWeeks.Contains(w)).ToList();
        var roundsPerLeg = fixtures.Where(f => f.Leg == 1).Select(f => f.WeekNumber).Distinct().Count();
        var legs = fixtures.Count == 0 ? 0 : fixtures.Max(f => f.Leg);

        response.isSuccess = true;
        response.Data = BuildScheduleDto(quinielaId, totalWeeks, roundsPerLeg, legs, duelWeeks, restWeeks, fixtures.Count == 0 ? null : info);
        return response;
    }

    private static DuelScheduleDto BuildScheduleDto(
        int quinielaId, int totalWeeks, int roundsPerLeg, int legs,
        List<int> duelWeeks, List<int> restWeeks, QuinielaDuelSeason? info) => new()
    {
        QuinielaId = quinielaId,
        TotalWeeks = totalWeeks,
        RoundsPerLeg = roundsPerLeg,
        Legs = legs,
        DuelWeeks = duelWeeks,
        RestWeeks = restWeeks,
        GeneratedAt = info?.GeneratedAt,
        IsClosed = info?.ClosedAt != null,
        ChampionMemberId = info?.ChampionMemberId,
        CupName = info?.CupName,
        PunishmentText = info?.PunishmentText,
    };

    // ---------------------------------------------------------------- Resolucion

    public async Task ResolveWeekAsync(int quinielaId, int weekId)
    {
        var quiniela = await _unitOfWork.Quinielas.GetAsync(quinielaId);
        if (quiniela == null || quiniela.SeasonId == null) return;

        var week = await _unitOfWork.Weeks.GetAsync(weekId);
        if (week == null) return;
        var weekNumber = week.WeekNumber;

        var weekFixtures = (await _unitOfWork.Duels.GetFixturesByWeekNumberAsync(quinielaId, weekNumber)).ToList();
        if (weekFixtures.Count == 0) return; // semana de descanso de duelos

        var weekPicks = (await _unitOfWork.Picks.GetAllPicksForWeekAsync(quinielaId, weekId)).ToList();
        // Jornada sin ningun pick calificado: no se resuelve (evita 0-0 y ventajas espurias).
        if (!weekPicks.Any(p => p.IsHit.HasValue)) return;

        var hitsByMember = weekPicks
            .Where(p => p.IsHit == true)
            .GroupBy(p => p.MemberId)
            .ToDictionary(g => g.Key, g => g.Count());

        var ranks = weekFixtures.Any(f => f.AwayMemberId != null)
            ? await GetRanksBeforeAsync(quiniela, weekNumber)
            : new Dictionary<int, int>();

        var now = DateTime.UtcNow;
        foreach (var fixture in weekFixtures)
        {
            fixture.WonByAdvantage = false;
            fixture.ResolvedAt = now;

            if (fixture.AwayMemberId == null)
            {
                fixture.HomeHits = 0;
                fixture.AwayHits = 0;
                fixture.HomeRankBefore = null;
                fixture.AwayRankBefore = null;
                fixture.Result = "BYE";
            }
            else
            {
                fixture.HomeHits = hitsByMember.GetValueOrDefault(fixture.HomeMemberId);
                fixture.AwayHits = hitsByMember.GetValueOrDefault(fixture.AwayMemberId.Value);
                fixture.HomeRankBefore = ranks.TryGetValue(fixture.HomeMemberId, out var hr) ? hr : null;
                fixture.AwayRankBefore = ranks.TryGetValue(fixture.AwayMemberId.Value, out var ar) ? ar : null;

                if (fixture.HomeHits > fixture.AwayHits) fixture.Result = "HOME";
                else if (fixture.HomeHits < fixture.AwayHits) fixture.Result = "AWAY";
                else
                {
                    fixture.Result = "DRAW";
                    var advantage = GetAdvantageSide(weekNumber, fixture.HomeRankBefore, fixture.AwayRankBefore);
                    if (advantage != null)
                    {
                        fixture.Result = advantage;
                        fixture.WonByAdvantage = true;
                    }
                }
            }

            await _unitOfWork.Duels.UpdateFixtureAsync(fixture);
        }

        var allFixtures = (await _unitOfWork.Duels.GetFixturesAsync(quinielaId)).ToList();
        // Cierra solo cuando TODOS los duelos estan resueltos (los Bye nacen con "BYE").
        var merged = allFixtures.Where(f => f.WeekNumber != weekNumber).Concat(weekFixtures).ToList();
        if (merged.All(f => f.Result != null))
        {
            await CloseSeasonAsync(quiniela, merged, allFixtures);
        }
    }

    /// <summary>
    /// Corona al primero de la tabla. <paramref name="fixtures"/> ya trae la fecha recien resuelta (instancias actualizadas).
    /// Si ya estaba cerrada conserva el ClosedAt original y solo recalcula el campeon.
    /// </summary>
    private async Task CloseSeasonAsync(Quiniela quiniela, List<DuelFixture> fixtures, List<DuelFixture> allFixtures)
    {
        var members = (await _unitOfWork.QuinielaMembers.GetMembersAsync(quiniela.Id)).ToList();
        var membersById = BuildMemberLookup(members, allFixtures);

        var participantIds = fixtures
            .SelectMany(f => f.AwayMemberId.HasValue ? new[] { f.HomeMemberId, f.AwayMemberId.Value } : new[] { f.HomeMemberId })
            .Distinct()
            .ToList();

        var table = BuildTable(fixtures, participantIds, membersById);
        if (table.Count == 0) return;
        var champion = table[0];

        var info = await _unitOfWork.Duels.GetSeasonInfoAsync(quiniela.Id)
                   ?? new QuinielaDuelSeason { QuinielaId = quiniela.Id, GeneratedAt = DateTime.UtcNow };
        info.ChampionMemberId = champion.MemberId;
        info.ClosedAt ??= DateTime.UtcNow;
        await _unitOfWork.Duels.UpsertSeasonInfoAsync(info);

        if (membersById.TryGetValue(champion.MemberId, out var championMember) && quiniela.SeasonId.HasValue)
        {
            await _achievementApplication.GrantAsync(
                championMember.UserId, AchievementCatalog.IndiscutidoDelColiseo, quiniela.Id, quiniela.SeasonId.Value);
        }
    }

    /// <summary>Lado ganador por Ventaja del Retador ("HOME"/"AWAY") o null si no aplica.</summary>
    private static string? GetAdvantageSide(int weekNumber, int? homeRank, int? awayRank)
    {
        if (weekNumber <= 1 || homeRank == null || awayRank == null) return null;
        if (Math.Abs(homeRank.Value - awayRank.Value) < ChallengerAdvantageGap) return null;
        return homeRank.Value > awayRank.Value ? "HOME" : "AWAY";
    }

    private async Task<Dictionary<int, int>> GetRanksBeforeAsync(Quiniela quiniela, int weekNumber)
    {
        if (weekNumber <= 1 || quiniela.SeasonId == null) return new Dictionary<int, int>();

        var members = await _unitOfWork.QuinielaMembers.GetMembersAsync(quiniela.Id);
        var allPicks = await _unitOfWork.Picks.GetAllPicksForQuinielaAsync(quiniela.Id);
        var awards = await _unitOfWork.WeeklyAwards.GetAllAwardsForQuinielaAsync(quiniela.Id);

        var standings = _scoringEngine.CalculateGeneralStandings(members, allPicks, quiniela.SeasonId.Value, weekNumber - 1, awards);
        return standings.ToDictionary(s => s.MemberId, s => s.Rank);
    }

    // ---------------------------------------------------------------- Tabla de duelos

    private sealed record DuelRow(int MemberId, int Played, int Won, int Drawn, int Lost, int Points, int SeasonHits, string Alias);

    private static Dictionary<int, QuinielaMember> BuildMemberLookup(IEnumerable<QuinielaMember> members, IEnumerable<DuelFixture> fixtures)
    {
        var lookup = new Dictionary<int, QuinielaMember>();
        foreach (var m in members) lookup[m.Id] = m;
        foreach (var f in fixtures)
        {
            if (f.HomeMember != null) lookup.TryAdd(f.HomeMember.Id, f.HomeMember);
            if (f.AwayMember != null) lookup.TryAdd(f.AwayMember.Id, f.AwayMember);
        }
        return lookup;
    }

    /// <summary>Tabla ordenada: Pts desc, victorias desc, aciertos de la temporada desc, Alias asc.</summary>
    private static List<DuelRow> BuildTable(
        IEnumerable<DuelFixture> fixtures,
        IEnumerable<int> participantIds,
        IReadOnlyDictionary<int, QuinielaMember> membersById)
    {
        var played = new Dictionary<int, int>();
        var won = new Dictionary<int, int>();
        var drawn = new Dictionary<int, int>();
        var lost = new Dictionary<int, int>();

        void Add(Dictionary<int, int> d, int id) => d[id] = d.GetValueOrDefault(id) + 1;

        foreach (var f in fixtures)
        {
            if (f.AwayMemberId == null || f.Result == null || f.Result == "BYE") continue;
            var awayId = f.AwayMemberId.Value;
            Add(played, f.HomeMemberId);
            Add(played, awayId);
            switch (f.Result)
            {
                case "HOME": Add(won, f.HomeMemberId); Add(lost, awayId); break;
                case "AWAY": Add(won, awayId); Add(lost, f.HomeMemberId); break;
                case "DRAW": Add(drawn, f.HomeMemberId); Add(drawn, awayId); break;
            }
        }

        var rows = participantIds.Distinct().Select(id =>
        {
            membersById.TryGetValue(id, out var m);
            var w = won.GetValueOrDefault(id);
            var d = drawn.GetValueOrDefault(id);
            return new DuelRow(id, played.GetValueOrDefault(id), w, d, lost.GetValueOrDefault(id),
                w * WinPoints + d * DrawPoints, m?.TotalHits ?? 0, m?.Alias ?? string.Empty);
        }).ToList();

        rows.Sort((a, b) =>
        {
            var c = b.Points.CompareTo(a.Points);
            if (c != 0) return c;
            c = b.Won.CompareTo(a.Won);
            if (c != 0) return c;
            c = b.SeasonHits.CompareTo(a.SeasonHits);
            if (c != 0) return c;
            return string.Compare(a.Alias, b.Alias, StringComparison.OrdinalIgnoreCase);
        });
        return rows;
    }

    public async Task<Response<IEnumerable<DuelStandingDto>>> GetStandingsAsync(int quinielaId, int userId)
    {
        var response = new Response<IEnumerable<DuelStandingDto>>();
        var (quiniela, error) = await EnsureAccessAsync(quinielaId, userId);
        if (quiniela == null)
        {
            response.isSuccess = false;
            response.Message = error!;
            return response;
        }

        var fixtures = (await _unitOfWork.Duels.GetFixturesAsync(quinielaId)).ToList();
        var members = await _unitOfWork.QuinielaMembers.GetMembersAsync(quinielaId);
        var membersById = BuildMemberLookup(members, fixtures);
        var participantIds = fixtures
            .SelectMany(f => f.AwayMemberId.HasValue ? new[] { f.HomeMemberId, f.AwayMemberId.Value } : new[] { f.HomeMemberId })
            .Distinct();

        var table = BuildTable(fixtures, participantIds, membersById);
        var result = new List<DuelStandingDto>();
        var rank = 1;
        foreach (var row in table)
        {
            result.Add(new DuelStandingDto
            {
                Rank = rank++,
                Participant = ToParticipant(row.MemberId, membersById),
                Played = row.Played,
                Won = row.Won,
                Drawn = row.Drawn,
                Lost = row.Lost,
                Points = row.Points,
                SeasonHits = row.SeasonHits,
            });
        }

        response.isSuccess = true;
        response.Data = result;
        return response;
    }

    // ---------------------------------------------------------------- Lecturas

    public async Task<Response<IEnumerable<DuelFixtureDto>>> GetFixturesAsync(int quinielaId, int userId, int? weekNumber)
    {
        var response = new Response<IEnumerable<DuelFixtureDto>>();
        var (quiniela, error) = await EnsureAccessAsync(quinielaId, userId);
        if (quiniela == null)
        {
            response.isSuccess = false;
            response.Message = error!;
            return response;
        }

        var fixtures = (await _unitOfWork.Duels.GetFixturesAsync(quinielaId)).ToList();
        if (weekNumber.HasValue) fixtures = fixtures.Where(f => f.WeekNumber == weekNumber.Value).ToList();

        response.isSuccess = true;
        response.Data = fixtures.Select(ToFixtureDto).ToList();
        return response;
    }

    public async Task<Response<DuelMatchupDto>> GetMatchupAsync(int quinielaId, int userId, int fixtureId)
    {
        var response = new Response<DuelMatchupDto>();

        Response<DuelMatchupDto> Fail(string message)
        {
            response.isSuccess = false;
            response.Message = message;
            return response;
        }

        var (quiniela, error) = await EnsureAccessAsync(quinielaId, userId);
        if (quiniela == null) return Fail(error!);

        var fixture = await _unitOfWork.Duels.GetFixtureAsync(fixtureId);
        if (fixture == null || fixture.QuinielaId != quinielaId) return Fail("Duelo no encontrado.");
        if (fixture.AwayMemberId == null) return Fail("Ese duelo es un descanso.");

        var dto = new DuelMatchupDto { Fixture = ToFixtureDto(fixture) };

        if (quiniela.SeasonId.HasValue)
        {
            var weeks = await _unitOfWork.Weeks.GetBySeasonIdAsync(quiniela.SeasonId.Value);
            var week = weeks.FirstOrDefault(w => w.WeekNumber == fixture.WeekNumber);
            if (week != null)
            {
                var matches = (await _unitOfWork.Matches.GetByWeekIdAsync(week.Id)).OrderBy(m => m.DateUtc).ToList();
                var picks = (await _unitOfWork.Picks.GetAllPicksForWeekAsync(quinielaId, week.Id)).ToList();
                var homeId = fixture.HomeMemberId;
                var awayId = fixture.AwayMemberId.Value;

                // Igual que PickApplication: los picks ajenos solo se revelan con la jornada bloqueada o calificada.
                // Antes del cierre cada participante ve unicamente el suyo (si no, un rival podria copiarlo).
                bool revealed = week.Status == "LOCKED" || week.Status == "SCORED";
                int? viewerMemberId = null;
                if (!revealed)
                {
                    viewerMemberId = (await _unitOfWork.QuinielaMembers.GetMembershipAsync(quinielaId, userId))?.Id;
                }
                bool showHome = revealed || viewerMemberId == homeId;
                bool showAway = revealed || viewerMemberId == awayId;

                foreach (var match in matches)
                {
                    var homePick = picks.FirstOrDefault(p => p.MatchId == match.Id && p.MemberId == homeId);
                    var awayPick = picks.FirstOrDefault(p => p.MatchId == match.Id && p.MemberId == awayId);
                    var finished = match.StatusState == "post";
                    var homeHit = homePick?.IsHit == true;
                    var awayHit = awayPick?.IsHit == true;

                    string? winner = null;
                    if (finished && homeHit != awayHit) winner = homeHit ? "HOME" : "AWAY";

                    dto.Matches.Add(new DuelMatchupMatchDto
                    {
                        MatchId = match.Id,
                        HomeTeam = match.HomeTeam?.Name ?? string.Empty,
                        AwayTeam = match.AwayTeam?.Name ?? string.Empty,
                        HomePick = showHome ? homePick?.PickAbbr : null,
                        AwayPick = showAway ? awayPick?.PickAbbr : null,
                        Differential = revealed && homePick?.PickAbbr != awayPick?.PickAbbr,
                        IsFinished = finished,
                        Winner = winner,
                    });
                }

                // Racha visual (FIRE/ICE) solo con gamificacion activa y fuera de Liguilla; sale de picks ya calificados.
                var league = quiniela.LeagueId.HasValue ? await _unitOfWork.Leagues.GetAsync(quiniela.LeagueId.Value) : null;
                if (quiniela.GamificationEnabled && league?.IsPlayoffWeek(fixture.WeekNumber) != true)
                {
                    var members = await _unitOfWork.QuinielaMembers.GetMembersAsync(quinielaId);
                    var standings = _scoringEngine.CalculateWeeklyStandings(members, picks, matches, includeHeat: true);
                    dto.Fixture.Home.Heat = standings.FirstOrDefault(s => s.MemberId == homeId)?.Heat;
                    dto.Fixture.Away!.Heat = standings.FirstOrDefault(s => s.MemberId == awayId)?.Heat;
                }

                if (fixture.Result == null)
                {
                    // En vivo: los aciertos aun no estan guardados en el duelo.
                    dto.Fixture.HomeHits = picks.Count(p => p.MemberId == homeId && p.IsHit == true);
                    dto.Fixture.AwayHits = picks.Count(p => p.MemberId == awayId && p.IsHit == true);
                }
            }
        }

        int? homeRank = fixture.HomeRankBefore, awayRank = fixture.AwayRankBefore;
        if (fixture.Result == null)
        {
            var ranks = await GetRanksBeforeAsync(quiniela, fixture.WeekNumber);
            homeRank = ranks.TryGetValue(fixture.HomeMemberId, out var hr) ? hr : null;
            awayRank = ranks.TryGetValue(fixture.AwayMemberId.Value, out var ar) ? ar : null;
            dto.Fixture.HomeRankBefore = homeRank;
            dto.Fixture.AwayRankBefore = awayRank;
        }
        dto.ChallengerAdvantage = GetAdvantageSide(fixture.WeekNumber, homeRank, awayRank);

        response.isSuccess = true;
        response.Data = dto;
        return response;
    }

    // ---------------------------------------------------------------- Salon de la Fama y copa

    public async Task<Response<IEnumerable<HallOfFameEntryDto>>> GetHallOfFameAsync(int userId)
    {
        var response = new Response<IEnumerable<HallOfFameEntryDto>>();

        var quinielas = await _unitOfWork.Quinielas.GetUserQuinielasAsync(userId);
        var ids = quinielas.Select(q => q.Id).ToList();
        var infos = ids.Count == 0
            ? new List<QuinielaDuelSeason>()
            : (await _unitOfWork.Duels.GetClosedSeasonInfosAsync(ids)).ToList();

        var entries = new List<HallOfFameEntryDto>();
        foreach (var info in infos)
        {
            if (info.ChampionMember == null) continue;
            var fixtures = (await _unitOfWork.Duels.GetFixturesAsync(info.QuinielaId)).ToList();
            entries.Add(BuildHallEntry(info, fixtures, userId));
        }

        response.isSuccess = true;
        response.Data = entries;
        return response;
    }

    public async Task<Response<HallOfFameEntryDto>> UpdateCupAsync(int quinielaId, int userId, UpdateDuelCupDto request)
    {
        var response = new Response<HallOfFameEntryDto>();

        Response<HallOfFameEntryDto> Fail(string message)
        {
            response.isSuccess = false;
            response.Message = message;
            return response;
        }

        // Solo existe en la lista de cerradas si la temporada de duelos ya tiene campeon.
        var closed = (await _unitOfWork.Duels.GetClosedSeasonInfosAsync(new[] { quinielaId }))
            .FirstOrDefault(i => i.QuinielaId == quinielaId);
        if (closed?.ChampionMember == null || closed.ChampionMember.UserId != userId)
            return Fail("Solo el campeón de duelos puede nombrar la copa.");

        var cupName = NormalizeText(request?.CupName);
        var punishment = NormalizeText(request?.PunishmentText);
        if (cupName != null && cupName.Length > MaxCupNameLength)
            return Fail("El nombre de la copa no puede pasar de 60 caracteres.");
        if (punishment != null && punishment.Length > MaxPunishmentLength)
            return Fail("El castigo no puede pasar de 200 caracteres.");

        var info = await _unitOfWork.Duels.GetSeasonInfoAsync(quinielaId) ?? closed;
        info.CupName = cupName;
        info.PunishmentText = punishment;
        await _unitOfWork.Duels.UpsertSeasonInfoAsync(info);

        closed.CupName = cupName;
        closed.PunishmentText = punishment;
        var fixtures = (await _unitOfWork.Duels.GetFixturesAsync(quinielaId)).ToList();

        response.isSuccess = true;
        response.Message = "Copa actualizada.";
        response.Data = BuildHallEntry(closed, fixtures, userId);
        return response;
    }

    private static string? NormalizeText(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static HallOfFameEntryDto BuildHallEntry(QuinielaDuelSeason info, List<DuelFixture> fixtures, int userId)
    {
        var champion = info.ChampionMember!;
        var participantIds = new[] { champion.Id };
        var row = BuildTable(fixtures, participantIds, new Dictionary<int, QuinielaMember> { [champion.Id] = champion })[0];
        var stats = CalculateChampionStats(fixtures, champion.Id);

        return new HallOfFameEntryDto
        {
            QuinielaId = info.QuinielaId,
            QuinielaName = info.Quiniela?.Name ?? string.Empty,
            SeasonId = info.Quiniela?.SeasonId,
            SeasonName = info.Quiniela?.Season?.Name,
            Champion = ToParticipant(champion),
            Record = $"{row.Won}-{row.Drawn}-{row.Lost}",
            Points = row.Points,
            TotalHits = champion.TotalHits,
            TotalUpsets = champion.TotalUpsets,
            AdvantageWins = stats.AdvantageWins,
            BestWinStreak = stats.BestWinStreak,
            BestDuelHits = stats.BestDuelHits,
            BestDuelWeekNumber = stats.BestDuelWeekNumber,
            CupName = info.CupName,
            PunishmentText = info.PunishmentText,
            Title = HallOfFameTitle,
            CanEditCup = champion.UserId == userId,
        };
    }

    /// <summary>Datos extra de la carta del campeon, calculados solo con sus duelos resueltos (sin Bye).</summary>
    public static (int AdvantageWins, int BestWinStreak, int BestDuelHits, int? BestDuelWeekNumber)
        CalculateChampionStats(IEnumerable<DuelFixture> fixtures, int championMemberId)
    {
        var played = fixtures
            .Where(f => f.AwayMemberId != null && f.Result != null && f.Result != "BYE"
                        && (f.HomeMemberId == championMemberId || f.AwayMemberId == championMemberId))
            .OrderBy(f => f.WeekNumber)
            .ToList();

        int advantageWins = 0, streak = 0, bestStreak = 0, bestHits = 0;
        int? bestWeek = null;

        foreach (var f in played)
        {
            var isHome = f.HomeMemberId == championMemberId;
            var won = (isHome && f.Result == "HOME") || (!isHome && f.Result == "AWAY");

            if (won)
            {
                streak++;
                bestStreak = Math.Max(bestStreak, streak);
                if (f.WonByAdvantage) advantageWins++;
            }
            else
            {
                streak = 0;
            }

            var hits = isHome ? f.HomeHits : f.AwayHits;
            if (hits > bestHits)
            {
                bestHits = hits;
                bestWeek = f.WeekNumber;
            }
        }

        return (advantageWins, bestStreak, bestHits, bestWeek);
    }

    // ---------------------------------------------------------------- Auxiliares

    /// <summary>Devuelve la quiniela si el usuario es miembro activo (o ADMIN de sistema); si no, el mensaje de error.</summary>
    private async Task<(Quiniela? Quiniela, string? Error)> EnsureAccessAsync(int quinielaId, int userId)
    {
        var quiniela = await _unitOfWork.Quinielas.GetAsync(quinielaId);
        if (quiniela == null) return (null, "La quiniela no existe.");

        var membership = await _unitOfWork.QuinielaMembers.GetMembershipAsync(quinielaId, userId);
        if (membership == null)
        {
            var user = await _unitOfWork.Users.GetAsync(userId);
            if (user == null || user.Role != "ADMIN") return (null, "No perteneces a esta quiniela.");
        }

        return (quiniela, null);
    }

    private static DuelParticipantDto ToParticipant(QuinielaMember member) => new()
    {
        MemberId = member.Id,
        UserId = member.UserId,
        Alias = member.Alias,
        DisplayName = member.User?.DisplayName,
        AvatarUrl = member.User?.AvatarUrl,
        EquippedTitle = member.User?.EquippedTitleCode == null
            ? null
            : AchievementCatalog.Find(member.User.EquippedTitleCode)?.TitleName,
    };

    private static DuelParticipantDto ToParticipant(int memberId, IReadOnlyDictionary<int, QuinielaMember> membersById) =>
        membersById.TryGetValue(memberId, out var m) ? ToParticipant(m) : new DuelParticipantDto { MemberId = memberId };

    private static DuelFixtureDto ToFixtureDto(DuelFixture f) => new()
    {
        Id = f.Id,
        WeekNumber = f.WeekNumber,
        Round = f.Round,
        Leg = f.Leg,
        Home = ToParticipant(f.HomeMember),
        Away = f.AwayMember == null ? null : ToParticipant(f.AwayMember),
        HomeHits = f.HomeHits,
        AwayHits = f.AwayHits,
        Result = f.Result,
        IsResolved = f.Result != null,
        WonByAdvantage = f.WonByAdvantage,
        HomeRankBefore = f.HomeRankBefore,
        AwayRankBefore = f.AwayRankBefore,
    };
}
