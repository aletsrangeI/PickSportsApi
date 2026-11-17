using Domain.Entities;
using DTO.Liguilla;
using Interface.Persistence;
using Interface.UseCases;
using Interface.UseCases.Knockout;
using UseCases.Liguilla.Strategies;
using UseCases.Scoring;

namespace UseCases.Liguilla;

public class LiguillaApplication : ILiguillaApplication
{
    private const string HiddenStatus = "HIDDEN";
    private const int UnseededValue = 0;
    private const string NotMemberMessage = "No perteneces a esta quiniela.";
    private const string NoStrategyMessage = "La liga de la quiniela no tiene un formato de eliminatoria configurado.";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IKnockoutStrategyResolver _strategyResolver;

    public LiguillaApplication(IUnitOfWork unitOfWork, IKnockoutStrategyResolver strategyResolver)
    {
        _unitOfWork = unitOfWork;
        _strategyResolver = strategyResolver;
    }

    public async Task<LiguillaOverviewDto> GetOverviewAsync(int quinielaId, int userId, bool isAdmin, CancellationToken ct = default)
    {
        var quiniela = await _unitOfWork.Quinielas.GetAsync(quinielaId);
        if (quiniela == null)
            throw new KeyNotFoundException($"No se encontró la quiniela con Id {quinielaId}.");

        // Obtener la temporada activa de la liga
        if (!quiniela.LeagueId.HasValue)
        {
            return new LiguillaOverviewDto { IsAvailable = false, Status = HiddenStatus };
        }

        var (league, strategy) = await ResolveStrategyAsync(quiniela);
        if (league == null || strategy == null)
        {
            return new LiguillaOverviewDto { IsAvailable = false, Status = HiddenStatus };
        }

        var seasonId = await QuinielaSeasonResolver.ResolveSeasonIdAsync(_unitOfWork, quiniela);
        if (seasonId == null)
        {
            return new LiguillaOverviewDto { IsAvailable = false, Status = HiddenStatus };
        }

        // 1. Verificación de Stealth Gating:
        // Todas las jornadas regulares (WeekNumber <= League.WeeksCount) deben estar SCORED
        var weeks = await _unitOfWork.Weeks.GetBySeasonIdAsync(seasonId.Value);
        var regularWeeks = weeks.Where(w => w.WeekNumber <= league.WeeksCount).ToList();
        bool isRegularSeasonFinished = regularWeeks.Count > 0 && regularWeeks.All(w => string.Equals(w.Status, "SCORED", StringComparison.OrdinalIgnoreCase));

        // Obtener los nodos oficiales de la eliminatoria
        var nodes = await _unitOfWork.Liguilla.GetNodesBySeasonAsync(seasonId.Value, ct);
        var firstRoundNodes = GetFirstRoundNodes(strategy, nodes);
        bool isFieldReady = strategy.IsFieldReady(nodes, isRegularSeasonFinished);
        bool isFieldPopulated = isRegularSeasonFinished ? isFieldReady : strategy.IsFieldReady(nodes, true);

        // Stealth Gating estricto: si la fase regular no ha concluido o el campo no está listo,
        // el módulo permanece estrictamente oculto e inaccesible para usuarios comunes.
        if (!isAdmin && !isFieldReady)
        {
            return new LiguillaOverviewDto
            {
                IsAvailable = false,
                Status = HiddenStatus
            };
        }

        // Determinar status operativo
        DateTime? lockDeadlineUtc = GetLockDeadline(firstRoundNodes);
        bool isLocked = lockDeadlineUtc.HasValue && DateTime.UtcNow >= lockDeadlineUtc.Value;
        string finalRoundCode = strategy.Rounds[^1].Code;
        bool isFinished = nodes.Any(n => string.Equals(n.Round, finalRoundCode, StringComparison.OrdinalIgnoreCase) && n.IsResolved);

        string status;
        if (isFinished)
            status = "FINISHED";
        else if (isLocked)
            status = "LOCKED_IN_PROGRESS";
        else if (isFieldPopulated)
            status = "OPEN_FOR_PICKS";
        else if (!isRegularSeasonFinished)
            status = isAdmin ? "ADMIN_PREVIEW_HIDDEN" : HiddenStatus;
        else
            status = "WAITING_PLAY_IN";

        // Mapear equipos clasificados con su siembra
        var qualifiedTeams = new List<LiguillaTeamDto>();
        foreach (var node in firstRoundNodes)
        {
            if (node.TeamA != null)
            {
                qualifiedTeams.Add(MapTeam(node.TeamA, node.SeedA ?? UnseededValue));
            }
            if (node.TeamB != null)
            {
                qualifiedTeams.Add(MapTeam(node.TeamB, node.SeedB ?? UnseededValue));
            }
        }
        qualifiedTeams = qualifiedTeams.OrderBy(t => t.Seed).ThenBy(t => t.Name, StringComparer.Ordinal).ToList();

        // Mapear nodos
        var nodeDtos = nodes.Select(MapNode).ToList();

        // Obtener el bracket del usuario
        var userBracketEntity = await _unitOfWork.Liguilla.GetBracketAsync(quinielaId, userId, ct);
        LiguillaBracketDetailDto? userBracketDto = null;
        if (userBracketEntity != null)
        {
            if (!userBracketEntity.IsLocked && userBracketEntity.SubmittedAtUtc != default)
            {
                userBracketEntity.IsLocked = true;
                await _unitOfWork.Save(ct);
            }
            var user = await _unitOfWork.Users.GetAsync(userId);
            var member = await _unitOfWork.QuinielaMembers.GetMembershipAsync(quinielaId, userId);
            var picks = await LoadPicksAsync(userBracketEntity, strategy, ct);
            userBracketDto = MapBracketDetail(userBracketEntity, user, member?.Alias, nodes, strategy, picks);
        }

        return new LiguillaOverviewDto
        {
            IsAvailable = true,
            FormatCode = strategy.FormatCode,
            Rounds = strategy.Rounds.Select(MapRound).ToList(),
            Status = status,
            LockDeadlineUtc = lockDeadlineUtc,
            QualifiedTeams = qualifiedTeams,
            Nodes = nodeDtos,
            UserBracket = userBracketDto
        };
    }

    public async Task<LiguillaBracketDetailDto?> GetMyBracketAsync(int quinielaId, int userId, CancellationToken ct = default)
    {
        var bracket = await _unitOfWork.Liguilla.GetBracketAsync(quinielaId, userId, ct);
        if (bracket == null) return null;

        if (!bracket.IsLocked && bracket.SubmittedAtUtc != default)
        {
            bracket.IsLocked = true;
            await _unitOfWork.Save(ct);
        }

        var quiniela = await _unitOfWork.Quinielas.GetAsync(quinielaId);
        if (quiniela == null) return null;

        var (_, strategy) = await ResolveStrategyAsync(quiniela);
        if (strategy == null) return null;

        var seasonId = await QuinielaSeasonResolver.ResolveSeasonIdAsync(_unitOfWork, quiniela);
        var nodes = seasonId.HasValue ? await _unitOfWork.Liguilla.GetNodesBySeasonAsync(seasonId.Value, ct) : new List<LiguillaNode>();

        var user = await _unitOfWork.Users.GetAsync(userId);
        var member = await _unitOfWork.QuinielaMembers.GetMembershipAsync(quinielaId, userId);
        var picks = await LoadPicksAsync(bracket, strategy, ct);

        return MapBracketDetail(bracket, user, member?.Alias, nodes, strategy, picks);
    }

    public Task<LiguillaBracketDetailDto> SubmitBracketAsync(int quinielaId, int userId, SubmitLiguillaBracketDto dto, CancellationToken ct = default)
    {
        return SubmitInternalAsync(quinielaId, userId, strategy => ToPicksByRound(strategy, dto), ct);
    }

    /// <summary>Registra los picks genéricos por ronda (cualquier formato). El DTO legado solo cubre FMF_LIGUILLA.</summary>
    public Task<LiguillaBracketDetailDto> SubmitPicksAsync(
        int quinielaId,
        int userId,
        IReadOnlyDictionary<string, IReadOnlyList<int>> picksByRound,
        CancellationToken ct = default)
    {
        return SubmitInternalAsync(quinielaId, userId, _ => picksByRound, ct);
    }

    private async Task<LiguillaBracketDetailDto> SubmitInternalAsync(
        int quinielaId,
        int userId,
        Func<IKnockoutFormatStrategy, IReadOnlyDictionary<string, IReadOnlyList<int>>> picksFactory,
        CancellationToken ct)
    {
        // El repositorio solo devuelve membresías activas.
        var membership = await _unitOfWork.QuinielaMembers.GetMembershipAsync(quinielaId, userId);
        if (membership == null)
            throw new InvalidOperationException(NotMemberMessage);

        var quiniela = await _unitOfWork.Quinielas.GetAsync(quinielaId);
        if (quiniela == null)
            throw new KeyNotFoundException($"No se encontró la quiniela con Id {quinielaId}.");

        if (!quiniela.LeagueId.HasValue)
            throw new InvalidOperationException("La quiniela no tiene una liga asignada.");

        var (_, strategy) = await ResolveStrategyAsync(quiniela);
        if (strategy == null)
            throw new InvalidOperationException(NoStrategyMessage);

        var seasonId = await QuinielaSeasonResolver.ResolveSeasonIdAsync(_unitOfWork, quiniela);
        if (seasonId == null)
            throw new InvalidOperationException("No hay una temporada vigente configurada.");

        var nodes = await _unitOfWork.Liguilla.GetNodesBySeasonAsync(seasonId.Value, ct);

        // 1. Validar fecha límite de bloqueo (Kickoff del primer partido de la primera ronda)
        DateTime? lockDeadlineUtc = GetLockDeadline(GetFirstRoundNodes(strategy, nodes));
        if (lockDeadlineUtc.HasValue && DateTime.UtcNow >= lockDeadlineUtc.Value)
        {
            throw new InvalidOperationException("El tiempo límite para registrar pronósticos de Liguilla ha expirado.");
        }

        // 2. Validar coherencia lógica del árbol (la regla es de la estrategia)
        var picksByRound = picksFactory(strategy);
        strategy.ValidatePicks(nodes, picksByRound);

        // 3. Crear o actualizar el bracket
        var existing = await _unitOfWork.Liguilla.GetBracketAsync(quinielaId, userId, ct);
        if (existing != null && existing.IsLocked)
        {
            throw new InvalidOperationException("Tu pronóstico ya ha sido bloqueado y no puede modificarse.");
        }

        bool isFmf = strategy.FormatCode == FmfLiguillaStrategy.Code;
        if (existing == null)
        {
            existing = new LiguillaBracket
            {
                QuinielaId = quinielaId,
                UserId = userId,
                IsLocked = true,
                SubmittedAtUtc = DateTime.UtcNow,
                Created = DateTime.UtcNow,
                CreatedBy = userId.ToString(),
                Active = true
            };
            if (isFmf)
                ApplyLegacyColumns(existing, picksByRound);
            await _unitOfWork.Liguilla.AddBracketAsync(existing, ct);
        }
        else
        {
            existing.IsLocked = true;
            existing.SubmittedAtUtc = DateTime.UtcNow;
            if (isFmf)
                ApplyLegacyColumns(existing, picksByRound);
            existing.LastModified = DateTime.UtcNow;
            existing.LastModifiedBy = userId.ToString();
            await _unitOfWork.Liguilla.UpdateBracketAsync(existing, ct);
        }

        await _unitOfWork.Liguilla.ReplacePicksAsync(existing.Id, BuildPickEntities(existing, picksByRound, userId), ct);
        await _unitOfWork.Save(ct);

        var user = await _unitOfWork.Users.GetAsync(userId);
        var member = await _unitOfWork.QuinielaMembers.GetMembershipAsync(quinielaId, userId);

        return MapBracketDetail(existing, user, member?.Alias, nodes, strategy, picksByRound);
    }

    public async Task<LiguillaStandingsDto> GetStandingsAsync(int quinielaId, CancellationToken ct = default)
    {
        var quiniela = await _unitOfWork.Quinielas.GetAsync(quinielaId);
        if (quiniela == null)
            throw new KeyNotFoundException($"No se encontró la quiniela con Id {quinielaId}.");

        var seasonId = await QuinielaSeasonResolver.ResolveSeasonIdAsync(_unitOfWork, quiniela);
        var currentSeason = seasonId.HasValue ? await _unitOfWork.Seasons.GetAsync(seasonId.Value) : null;
        string seasonName = currentSeason?.Name ?? "Liguilla";

        var (_, strategy) = await ResolveStrategyAsync(quiniela);
        if (strategy == null)
        {
            return new LiguillaStandingsDto
            {
                QuinielaId = quinielaId,
                SeasonName = seasonName,
                Standings = new List<LiguillaStandingItemDto>()
            };
        }

        var nodes = seasonId.HasValue ? await _unitOfWork.Liguilla.GetNodesBySeasonAsync(seasonId.Value, ct) : new List<LiguillaNode>();

        var brackets = await _unitOfWork.Liguilla.GetAllBracketsForQuinielaAsync(quinielaId, ct);
        var members = await _unitOfWork.QuinielaMembers.GetMembersAsync(quinielaId);
        var memberMap = members.ToDictionary(m => m.UserId, m => m);

        string finalRoundCode = strategy.Rounds[^1].Code;
        var finalNode = nodes.FirstOrDefault(n => string.Equals(n.Round, finalRoundCode, StringComparison.OrdinalIgnoreCase));
        bool finalResolved = finalNode != null && finalNode.IsResolved && finalNode.WinnerTeamId.HasValue;

        var evaluatedItems = new List<LiguillaStandingItemDto>();
        var scoresByItem = new Dictionary<LiguillaStandingItemDto, KnockoutScore>(ReferenceEqualityComparer.Instance);

        foreach (var bracket in brackets)
        {
            var picks = await LoadPicksAsync(bracket, strategy, ct);
            var score = KnockoutScoringEngine.Evaluate(strategy, picks, nodes);
            var (firstPts, middlePts, lastPts) = SplitPoints(strategy, score);
            int totalPts = score.TotalPoints;

            if (bracket.TotalScore != totalPts || bracket.QfPoints != firstPts || bracket.SfPoints != middlePts || bracket.ChampionPoints != lastPts)
            {
                bracket.QfPoints = firstPts;
                bracket.SfPoints = middlePts;
                bracket.ChampionPoints = lastPts;
                bracket.BonusPoints = score.BonusPoints;
                bracket.TotalScore = totalPts;
                await _unitOfWork.Liguilla.UpdateBracketAsync(bracket, ct);
            }

            memberMap.TryGetValue(bracket.UserId, out var member);
            bool champPickedCorrectly = finalResolved
                && picks.TryGetValue(finalRoundCode, out var championPicks)
                && championPicks.Contains(finalNode!.WinnerTeamId!.Value);

            var item = new LiguillaStandingItemDto
            {
                UserId = bracket.UserId,
                Alias = member?.Alias ?? bracket.User?.Username ?? "Participante",
                AvatarUrl = bracket.User?.AvatarUrl,
                TotalPoints = totalPts,
                QfPoints = firstPts,
                SfPoints = middlePts,
                ChampionPoints = lastPts,
                BonusPoints = score.BonusPoints,
                ChampionPickedCorrectly = champPickedCorrectly,
                SubmittedAtUtc = bracket.SubmittedAtUtc
            };
            evaluatedItems.Add(item);
            scoresByItem[item] = score;
        }

        await _unitOfWork.Save(ct);

        // Desempates en cascada:
        // 1. TotalPoints desc
        // 2. ChampionPickedCorrectly desc
        // 3. Puntos de cada ronda, de la última intermedia hacia la primera (excluye la final)
        // 4. SubmittedAtUtc asc
        // En Liga MX (QF, SF, FINAL) equivale a SfPoints y luego QfPoints.
        var tiebreakRounds = strategy.Rounds.Take(strategy.Rounds.Count - 1).Reverse().Select(r => r.Code).ToList();
        var ordered = evaluatedItems
            .OrderByDescending(x => x.TotalPoints)
            .ThenByDescending(x => x.ChampionPickedCorrectly);
        foreach (var roundCode in tiebreakRounds)
        {
            ordered = ordered.ThenByDescending(x => PointsOfRound(scoresByItem[x], roundCode));
        }

        var sorted = ordered
            .ThenBy(x => x.SubmittedAtUtc)
            .ToList();

        for (int i = 0; i < sorted.Count; i++)
        {
            sorted[i].Rank = i + 1;
        }

        return new LiguillaStandingsDto
        {
            QuinielaId = quinielaId,
            SeasonName = seasonName,
            Standings = sorted
        };
    }

    public async Task<bool> ResolveNodeAsync(int quinielaId, ResolveLiguillaNodeRequestDto dto, CancellationToken ct = default)
    {
        var quiniela = await _unitOfWork.Quinielas.GetAsync(quinielaId);
        if (quiniela == null)
            throw new KeyNotFoundException($"No se encontró la quiniela con Id {quinielaId}.");

        var (_, strategy) = await ResolveStrategyAsync(quiniela);
        if (strategy == null)
            throw new InvalidOperationException(NoStrategyMessage);

        var node = await _unitOfWork.Liguilla.GetNodeByIdAsync(dto.NodeId, ct);
        if (node == null)
            throw new KeyNotFoundException($"No se encontró el nodo con Id {dto.NodeId}.");

        node.WinnerTeamId = dto.WinnerTeamId;
        node.IsResolved = true;
        node.ResolvedAtUtc = DateTime.UtcNow;
        node.LastModified = DateTime.UtcNow;

        await _unitOfWork.Liguilla.UpdateNodeAsync(node, ct);

        var allNodes = await _unitOfWork.Liguilla.GetNodesBySeasonAsync(node.SeasonId, ct);

        // El listado puede traer otra instancia del mismo nodo: asegura que la estrategia vea el ganador recién guardado
        var listed = allNodes.FirstOrDefault(n => n.Id == node.Id);
        if (listed != null && !ReferenceEquals(listed, node))
        {
            listed.WinnerTeamId = node.WinnerTeamId;
            listed.IsResolved = node.IsResolved;
            listed.ResolvedAtUtc = node.ResolvedAtUtc;
        }

        foreach (var advanced in strategy.AdvanceWinners(allNodes))
        {
            await _unitOfWork.Liguilla.UpdateNodeAsync(advanced, ct);
        }

        await _unitOfWork.Save(ct);

        // Recalcular standings de la quiniela
        await GetStandingsAsync(quinielaId, ct);
        return true;
    }

    private async Task<(League? League, IKnockoutFormatStrategy? Strategy)> ResolveStrategyAsync(Quiniela quiniela)
    {
        if (!quiniela.LeagueId.HasValue)
            return (null, null);

        var league = quiniela.League ?? await _unitOfWork.Leagues.GetAsync(quiniela.LeagueId.Value);
        return league == null ? (null, null) : (league, _strategyResolver.For(league));
    }

    private static List<LiguillaNode> GetFirstRoundNodes(IKnockoutFormatStrategy strategy, IReadOnlyList<LiguillaNode> nodes) =>
        nodes.Where(n => string.Equals(n.Round, strategy.FirstPickableRoundCode, StringComparison.OrdinalIgnoreCase)).ToList();

    private static DateTime? GetLockDeadline(IReadOnlyList<LiguillaNode> firstRoundNodes) =>
        firstRoundNodes.Where(n => n.KickoffUtc.HasValue).Select(n => n.KickoffUtc).Min();

    /// <summary>Picks por ronda del bracket: genéricos si existen; si no, y solo en FMF, derivados de las columnas legadas.</summary>
    private async Task<Dictionary<string, IReadOnlyList<int>>> LoadPicksAsync(
        LiguillaBracket bracket,
        IKnockoutFormatStrategy strategy,
        CancellationToken ct)
    {
        var stored = await _unitOfWork.Liguilla.GetPicksByBracketAsync(bracket.Id, ct);
        if (stored is { Count: > 0 })
        {
            return stored
                .GroupBy(p => p.RoundCode)
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyList<int>)g.OrderBy(p => p.Slot).Select(p => p.TeamId).ToList());
        }

        return strategy.FormatCode == FmfLiguillaStrategy.Code
            ? FromLegacyColumns(bracket)
            : new Dictionary<string, IReadOnlyList<int>>();
    }

    private static Dictionary<string, IReadOnlyList<int>> FromLegacyColumns(LiguillaBracket bracket) => new()
    {
        [FmfLiguillaStrategy.QuarterFinalRound] = new List<int>
        {
            bracket.QfWinner1TeamId, bracket.QfWinner2TeamId, bracket.QfWinner3TeamId, bracket.QfWinner4TeamId
        },
        [FmfLiguillaStrategy.SemiFinalRound] = new List<int> { bracket.SfWinner1TeamId, bracket.SfWinner2TeamId },
        [FmfLiguillaStrategy.FinalRound] = new List<int> { bracket.ChampionTeamId }
    };

    private static IReadOnlyDictionary<string, IReadOnlyList<int>> ToPicksByRound(IKnockoutFormatStrategy strategy, SubmitLiguillaBracketDto dto)
    {
        if (strategy.FormatCode != FmfLiguillaStrategy.Code)
            throw new InvalidOperationException($"El formato {strategy.FormatCode} requiere pronósticos por ronda; el DTO legado solo aplica a {FmfLiguillaStrategy.Code}.");

        return new Dictionary<string, IReadOnlyList<int>>
        {
            [FmfLiguillaStrategy.QuarterFinalRound] = new List<int>
            {
                dto.QfWinner1TeamId, dto.QfWinner2TeamId, dto.QfWinner3TeamId, dto.QfWinner4TeamId
            },
            [FmfLiguillaStrategy.SemiFinalRound] = new List<int> { dto.SfWinner1TeamId, dto.SfWinner2TeamId },
            [FmfLiguillaStrategy.FinalRound] = new List<int> { dto.ChampionTeamId }
        };
    }

    private static void ApplyLegacyColumns(LiguillaBracket bracket, IReadOnlyDictionary<string, IReadOnlyList<int>> picksByRound)
    {
        var qf = picksByRound[FmfLiguillaStrategy.QuarterFinalRound];
        var sf = picksByRound[FmfLiguillaStrategy.SemiFinalRound];
        var final = picksByRound[FmfLiguillaStrategy.FinalRound];

        bracket.QfWinner1TeamId = qf[0];
        bracket.QfWinner2TeamId = qf[1];
        bracket.QfWinner3TeamId = qf[2];
        bracket.QfWinner4TeamId = qf[3];
        bracket.SfWinner1TeamId = sf[0];
        bracket.SfWinner2TeamId = sf[1];
        bracket.ChampionTeamId = final[0];
    }

    private static List<LiguillaBracketPick> BuildPickEntities(
        LiguillaBracket bracket,
        IReadOnlyDictionary<string, IReadOnlyList<int>> picksByRound,
        int userId)
    {
        var entities = new List<LiguillaBracketPick>();
        foreach (var (roundCode, teamIds) in picksByRound)
        {
            for (int i = 0; i < teamIds.Count; i++)
            {
                entities.Add(new LiguillaBracketPick
                {
                    BracketId = bracket.Id,
                    Bracket = bracket,
                    RoundCode = roundCode,
                    Slot = i + 1,
                    TeamId = teamIds[i],
                    Created = DateTime.UtcNow,
                    CreatedBy = userId.ToString(),
                    Active = true
                });
            }
        }

        return entities;
    }

    /// <summary>Puntos de la primera ronda, de las intermedias y de la última (campeón): el desglose que expone el DTO.</summary>
    private static (int First, int Middle, int Last) SplitPoints(IKnockoutFormatStrategy strategy, KnockoutScore score)
    {
        string firstCode = strategy.Rounds[0].Code;
        string lastCode = strategy.Rounds[^1].Code;
        int first = score.PointsByRound.TryGetValue(firstCode, out var f) ? f : 0;
        int last = score.PointsByRound.TryGetValue(lastCode, out var l) ? l : 0;
        int middle = score.PointsByRound
            .Where(kv => kv.Key != firstCode && kv.Key != lastCode)
            .Sum(kv => kv.Value);
        return (first, middle, last);
    }

    private static int PointsOfRound(KnockoutScore score, string roundCode) =>
        score.PointsByRound.TryGetValue(roundCode, out var points) ? points : 0;

    private static int PickAt(IReadOnlyDictionary<string, IReadOnlyList<int>> picks, string roundCode, int index) =>
        picks.TryGetValue(roundCode, out var list) && index < list.Count ? list[index] : 0;

    private static LiguillaRoundDto MapRound(KnockoutRound round) => new()
    {
        Code = round.Code,
        Order = round.Order,
        WinnersToPick = round.WinnersToPick,
        PointsPerHit = round.PointsPerHit,
        NodeCount = round.NodeCount
    };

    private static LiguillaTeamDto MapTeam(Team team, int seed) => new()
    {
        TeamId = team.Id,
        Name = team.Name,
        ShortName = team.DisplayName,
        Abbreviation = team.Abbreviation,
        LogoUrl = team.LogoUrl,
        PrimaryColor = team.PrimaryColor,
        Seed = seed
    };

    private static LiguillaNodeDto MapNode(LiguillaNode node) => new()
    {
        Id = node.Id,
        Round = node.Round,
        NodeNumber = node.NodeNumber,
        TeamA = node.TeamA != null ? MapTeam(node.TeamA, node.SeedA ?? UnseededValue) : null,
        TeamB = node.TeamB != null ? MapTeam(node.TeamB, node.SeedB ?? UnseededValue) : null,
        WinnerTeam = node.WinnerTeam != null ? MapTeam(node.WinnerTeam, UnseededValue) : null,
        IsResolved = node.IsResolved,
        ResolvedAtUtc = node.ResolvedAtUtc,
        KickoffUtc = node.KickoffUtc
    };

    private static LiguillaBracketDetailDto MapBracketDetail(
        LiguillaBracket bracket,
        User? user,
        string? alias,
        IReadOnlyList<LiguillaNode> nodes,
        IKnockoutFormatStrategy strategy,
        IReadOnlyDictionary<string, IReadOnlyList<int>> picks)
    {
        var score = KnockoutScoringEngine.Evaluate(strategy, picks, nodes);
        var (firstPts, middlePts, lastPts) = SplitPoints(strategy, score);
        bool isFmf = strategy.FormatCode == FmfLiguillaStrategy.Code;

        var dto = new LiguillaBracketDetailDto
        {
            Id = bracket.Id,
            UserId = bracket.UserId,
            UserName = user?.Username ?? "",
            Alias = alias ?? user?.DisplayName ?? user?.Username ?? "",
            AvatarUrl = user?.AvatarUrl,
            IsLocked = bracket.IsLocked,
            SubmittedAtUtc = bracket.SubmittedAtUtc,
            TotalScore = score.TotalPoints,
            QfPoints = firstPts,
            SfPoints = middlePts,
            ChampionPoints = lastPts,
            BonusPoints = score.BonusPoints,
            ShatteredTeamIds = score.ShatteredTeamIds.ToList(),
            PicksByRound = picks.ToDictionary(kv => kv.Key, kv => kv.Value.ToList())
        };

        // Campos legados del frontend: solo tienen sentido en la Liguilla FMF
        if (isFmf)
        {
            dto.QfWinner1TeamId = PickAt(picks, FmfLiguillaStrategy.QuarterFinalRound, 0);
            dto.QfWinner2TeamId = PickAt(picks, FmfLiguillaStrategy.QuarterFinalRound, 1);
            dto.QfWinner3TeamId = PickAt(picks, FmfLiguillaStrategy.QuarterFinalRound, 2);
            dto.QfWinner4TeamId = PickAt(picks, FmfLiguillaStrategy.QuarterFinalRound, 3);
            dto.SfWinner1TeamId = PickAt(picks, FmfLiguillaStrategy.SemiFinalRound, 0);
            dto.SfWinner2TeamId = PickAt(picks, FmfLiguillaStrategy.SemiFinalRound, 1);
            dto.ChampionTeamId = PickAt(picks, FmfLiguillaStrategy.FinalRound, 0);
        }

        return dto;
    }

    public async Task<bool> SeedSimulationNodesAsync(int quinielaId, CancellationToken ct = default)
    {
        var quiniela = await _unitOfWork.Quinielas.GetAsync(quinielaId);
        if (quiniela == null)
            throw new KeyNotFoundException($"No se encontró la quiniela con Id {quinielaId}.");

        if (!quiniela.LeagueId.HasValue)
            throw new InvalidOperationException("La quiniela no tiene una liga asignada.");

        var (_, strategy) = await ResolveStrategyAsync(quiniela);
        if (strategy == null || strategy.FormatCode != FmfLiguillaStrategy.Code)
            throw new InvalidOperationException("La simulación de siembra solo está disponible para el formato FMF_LIGUILLA.");

        var seasonId = await QuinielaSeasonResolver.ResolveSeasonIdAsync(_unitOfWork, quiniela);
        if (seasonId == null)
            throw new InvalidOperationException("No se encontró una temporada activa para esta liga.");

        // Obtener equipos de la liga ordenados por puntos o por ESPN Standings
        // Orden top 8 oficial de Liga MX Apertura 2026 actual según ESPN:
        // 1: América (AME, 227), 2: Toluca (TOL, 223), 3: Querétaro (QRO, 222), 4: Guadalajara (GDL, 219),
        // 5: León (LEO, 228), 6: Atlas (ATS, 216), 7: Cruz Azul (CAZ, 218), 8: Tijuana (TIJ, 10125)
        var leagueTeams = (await _unitOfWork.Teams.GetByLeagueIdAsync(quiniela.LeagueId.Value)).ToList();
        
        var top8EspnIds = new[] { "227", "223", "222", "219", "228", "216", "218", "10125" };
        var top8Teams = new List<Team>();

        foreach (var espnId in top8EspnIds)
        {
            var match = leagueTeams.FirstOrDefault(t => t.EspnTeamId == espnId);
            if (match != null)
            {
                top8Teams.Add(match);
            }
        }

        // Si por alguna razón faltan equipos específicos, rellenar con los primeros disponibles de la liga
        foreach (var t in leagueTeams)
        {
            if (top8Teams.Count >= 8) break;
            if (!top8Teams.Any(existing => existing.Id == t.Id))
            {
                top8Teams.Add(t);
            }
        }

        if (top8Teams.Count < 8)
            throw new InvalidOperationException($"Se requieren al menos 8 equipos en la liga para sembrar la Liguilla. Encontrados: {top8Teams.Count}.");

        // Equipos con sus siembras:
        // 1: top8Teams[0], 2: top8Teams[1], 3: top8Teams[2], 4: top8Teams[3]
        // 5: top8Teams[4], 6: top8Teams[5], 7: top8Teams[6], 8: top8Teams[7]
        var existingNodes = await _unitOfWork.Liguilla.GetNodesBySeasonAsync(seasonId.Value, ct);
        var kickoffSimulation = DateTime.UtcNow.AddDays(7); // Tiempo suficiente para pronosticar

        // Cuartos de Final (Reglamento FMF):
        // Serie 1: 1° vs 8°
        // Serie 2: 2° vs 7°
        // Serie 3: 3° vs 6°
        // Serie 4: 4° vs 5°
        var matchups = new[]
        {
            (nodeNumber: 1, teamA: top8Teams[0], seedA: 1, teamB: top8Teams[7], seedB: 8),
            (nodeNumber: 2, teamA: top8Teams[1], seedA: 2, teamB: top8Teams[6], seedB: 7),
            (nodeNumber: 3, teamA: top8Teams[2], seedA: 3, teamB: top8Teams[5], seedB: 6),
            (nodeNumber: 4, teamA: top8Teams[3], seedA: 4, teamB: top8Teams[4], seedB: 5),
        };

        foreach (var (nodeNumber, teamA, seedA, teamB, seedB) in matchups)
        {
            var node = existingNodes.FirstOrDefault(n => n.NodeNumber == nodeNumber);
            if (node == null)
            {
                node = new LiguillaNode
                {
                    SeasonId = seasonId.Value,
                    Round = "QF",
                    NodeNumber = nodeNumber,
                    TeamAId = teamA.Id,
                    TeamBId = teamB.Id,
                    SeedA = seedA,
                    SeedB = seedB,
                    KickoffUtc = kickoffSimulation,
                    IsResolved = false,
                    Active = true
                };
                await _unitOfWork.Liguilla.AddNodeAsync(node, ct);
            }
            else
            {
                node.TeamAId = teamA.Id;
                node.TeamBId = teamB.Id;
                node.SeedA = seedA;
                node.SeedB = seedB;
                node.KickoffUtc = kickoffSimulation;
                node.IsResolved = false;
                node.WinnerTeamId = null;
                await _unitOfWork.Liguilla.UpdateNodeAsync(node, ct);
            }
        }

        // Semifinales (Nodos 5 y 6) y Final (Nodo 7) vacías a la espera de resolución de QF
        var laterNodes = new[]
        {
            (nodeNumber: 5, round: "SF"),
            (nodeNumber: 6, round: "SF"),
            (nodeNumber: 7, round: "FINAL")
        };

        foreach (var (nodeNumber, round) in laterNodes)
        {
            var node = existingNodes.FirstOrDefault(n => n.NodeNumber == nodeNumber);
            if (node == null)
            {
                node = new LiguillaNode
                {
                    SeasonId = seasonId.Value,
                    Round = round,
                    NodeNumber = nodeNumber,
                    TeamAId = null,
                    TeamBId = null,
                    SeedA = null,
                    SeedB = null,
                    KickoffUtc = kickoffSimulation.AddDays(nodeNumber == 7 ? 14 : 7),
                    IsResolved = false,
                    Active = true
                };
                await _unitOfWork.Liguilla.AddNodeAsync(node, ct);
            }
            else
            {
                node.TeamAId = null;
                node.TeamBId = null;
                node.SeedA = null;
                node.SeedB = null;
                node.WinnerTeamId = null;
                node.IsResolved = false;
                await _unitOfWork.Liguilla.UpdateNodeAsync(node, ct);
            }
        }

        await _unitOfWork.Save(ct);
        return true;
    }
}
