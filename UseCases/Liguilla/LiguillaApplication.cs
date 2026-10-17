using Domain.Entities;
using DTO.Liguilla;
using Interface.Persistence;
using Interface.UseCases;

namespace UseCases.Liguilla;

public class LiguillaApplication : ILiguillaApplication
{
    private readonly IUnitOfWork _unitOfWork;

    public LiguillaApplication(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<LiguillaOverviewDto> GetOverviewAsync(int quinielaId, int userId, bool isAdmin, CancellationToken ct = default)
    {
        var quiniela = await _unitOfWork.Quinielas.GetAsync(quinielaId);
        if (quiniela == null)
            throw new KeyNotFoundException($"No se encontró la quiniela con Id {quinielaId}.");

        // Obtener la temporada activa de la liga
        if (!quiniela.LeagueId.HasValue)
        {
            return new LiguillaOverviewDto { IsAvailable = false, Status = "HIDDEN" };
        }

        var currentSeason = await _unitOfWork.Seasons.GetCurrentSeasonAsync(quiniela.LeagueId.Value);
        if (currentSeason == null)
        {
            return new LiguillaOverviewDto { IsAvailable = false, Status = "HIDDEN" };
        }

        // 1. Verificación de Stealth Gating:
        // Todas las jornadas regulares (<= 17) deben estar SCORED
        var weeks = await _unitOfWork.Weeks.GetBySeasonIdAsync(currentSeason.Id);
        var regularWeeks = weeks.Where(w => w.WeekNumber <= 17).ToList();
        bool isRegularSeasonFinished = regularWeeks.Count > 0 && regularWeeks.All(w => string.Equals(w.Status, "SCORED", StringComparison.OrdinalIgnoreCase));

        // Obtener los nodos oficiales de Liguilla
        var nodes = await _unitOfWork.Liguilla.GetNodesBySeasonAsync(currentSeason.Id, ct);
        var qfNodes = nodes.Where(n => n.NodeNumber >= 1 && n.NodeNumber <= 4).ToList();
        bool isPlayInFinished = qfNodes.Count == 4 && qfNodes.All(n => n.TeamAId.HasValue && n.TeamBId.HasValue);

        // Stealth Gating estricto: Si el torneo regular no ha concluido (J17 SCORED) o el Play-In no está listo,
        // el módulo permanece estrictamente oculto e inaccesible para usuarios comunes.
        if (!isAdmin && (!isRegularSeasonFinished || !isPlayInFinished))
        {
            return new LiguillaOverviewDto
            {
                IsAvailable = false,
                Status = "HIDDEN"
            };
        }

        // Determinar status operativo
        DateTime? lockDeadlineUtc = qfNodes.FirstOrDefault()?.KickoffUtc;
        bool isLocked = lockDeadlineUtc.HasValue && DateTime.UtcNow >= lockDeadlineUtc.Value;
        bool isFinished = nodes.Any(n => n.NodeNumber == 7 && n.IsResolved);

        string status;
        if (isFinished)
            status = "FINISHED";
        else if (isLocked)
            status = "LOCKED_IN_PROGRESS";
        else if (isPlayInFinished)
            status = "OPEN_FOR_PICKS";
        else if (!isRegularSeasonFinished)
            status = isAdmin ? "ADMIN_PREVIEW_HIDDEN" : "HIDDEN";
        else
            status = "WAITING_PLAY_IN";

        // Mapear equipos clasificados con su siembra (1 al 8)
        var qualifiedTeams = new List<LiguillaTeamDto>();
        foreach (var node in qfNodes)
        {
            if (node.TeamA != null && node.SeedA.HasValue)
            {
                qualifiedTeams.Add(MapTeam(node.TeamA, node.SeedA.Value));
            }
            if (node.TeamB != null && node.SeedB.HasValue)
            {
                qualifiedTeams.Add(MapTeam(node.TeamB, node.SeedB.Value));
            }
        }
        qualifiedTeams = qualifiedTeams.OrderBy(t => t.Seed).ToList();

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
            userBracketDto = MapBracketDetail(userBracketEntity, user, member?.Alias, nodes);
        }

        return new LiguillaOverviewDto
        {
            IsAvailable = true,
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
        var currentSeason = (quiniela != null && quiniela.LeagueId.HasValue) ? await _unitOfWork.Seasons.GetCurrentSeasonAsync(quiniela.LeagueId.Value) : null;
        var nodes = currentSeason != null ? await _unitOfWork.Liguilla.GetNodesBySeasonAsync(currentSeason.Id, ct) : new List<LiguillaNode>();

        var user = await _unitOfWork.Users.GetAsync(userId);
        var member = await _unitOfWork.QuinielaMembers.GetMembershipAsync(quinielaId, userId);

        return MapBracketDetail(bracket, user, member?.Alias, nodes);
    }

    public async Task<LiguillaBracketDetailDto> SubmitBracketAsync(int quinielaId, int userId, SubmitLiguillaBracketDto dto, CancellationToken ct = default)
    {
        var quiniela = await _unitOfWork.Quinielas.GetAsync(quinielaId);
        if (quiniela == null)
            throw new KeyNotFoundException($"No se encontró la quiniela con Id {quinielaId}.");

        if (!quiniela.LeagueId.HasValue)
            throw new InvalidOperationException("La quiniela no tiene una liga asignada.");

        var currentSeason = await _unitOfWork.Seasons.GetCurrentSeasonAsync(quiniela.LeagueId.Value);
        if (currentSeason == null)
            throw new InvalidOperationException("No hay una temporada vigente configurada.");

        var nodes = await _unitOfWork.Liguilla.GetNodesBySeasonAsync(currentSeason.Id, ct);
        var qfNodes = nodes.Where(n => n.NodeNumber >= 1 && n.NodeNumber <= 4).ToList();

        // 1. Validar fecha límite de bloqueo (Kickoff del primer partido de cuartos)
        DateTime? lockDeadlineUtc = qfNodes.FirstOrDefault()?.KickoffUtc;
        if (lockDeadlineUtc.HasValue && DateTime.UtcNow >= lockDeadlineUtc.Value)
        {
            throw new InvalidOperationException("El tiempo límite para registrar pronósticos de Liguilla ha expirado.");
        }

        // 2. Validar coherencia lógica del árbol
        var qfWinners = new HashSet<int>
        {
            dto.QfWinner1TeamId,
            dto.QfWinner2TeamId,
            dto.QfWinner3TeamId,
            dto.QfWinner4TeamId
        };

        if (qfWinners.Count != 4)
            throw new ArgumentException("Debes elegir 4 ganadores distintos en Cuartos de Final.");

        if (!qfWinners.Contains(dto.SfWinner1TeamId) || !qfWinners.Contains(dto.SfWinner2TeamId))
            throw new ArgumentException("Los finalistas de Semifinales deben haber sido seleccionados como ganadores en Cuartos de Final.");

        if (dto.SfWinner1TeamId == dto.SfWinner2TeamId)
            throw new ArgumentException("Los dos finalistas deben ser equipos distintos.");

        if (dto.ChampionTeamId != dto.SfWinner1TeamId && dto.ChampionTeamId != dto.SfWinner2TeamId)
            throw new ArgumentException("El Campeón debe ser uno de los dos equipos finalistas seleccionados.");

        // 3. Crear o actualizar el bracket
        var existing = await _unitOfWork.Liguilla.GetBracketAsync(quinielaId, userId, ct);
        if (existing != null && existing.IsLocked)
        {
            throw new InvalidOperationException("Tu pronóstico ya ha sido bloqueado y no puede modificarse.");
        }

        if (existing == null)
        {
            existing = new LiguillaBracket
            {
                QuinielaId = quinielaId,
                UserId = userId,
                IsLocked = true,
                SubmittedAtUtc = DateTime.UtcNow,
                QfWinner1TeamId = dto.QfWinner1TeamId,
                QfWinner2TeamId = dto.QfWinner2TeamId,
                QfWinner3TeamId = dto.QfWinner3TeamId,
                QfWinner4TeamId = dto.QfWinner4TeamId,
                SfWinner1TeamId = dto.SfWinner1TeamId,
                SfWinner2TeamId = dto.SfWinner2TeamId,
                ChampionTeamId = dto.ChampionTeamId,
                Created = DateTime.UtcNow,
                CreatedBy = userId.ToString(),
                Active = true
            };
            await _unitOfWork.Liguilla.AddBracketAsync(existing, ct);
        }
        else
        {
            existing.IsLocked = true;
            existing.SubmittedAtUtc = DateTime.UtcNow;
            existing.QfWinner1TeamId = dto.QfWinner1TeamId;
            existing.QfWinner2TeamId = dto.QfWinner2TeamId;
            existing.QfWinner3TeamId = dto.QfWinner3TeamId;
            existing.QfWinner4TeamId = dto.QfWinner4TeamId;
            existing.SfWinner1TeamId = dto.SfWinner1TeamId;
            existing.SfWinner2TeamId = dto.SfWinner2TeamId;
            existing.ChampionTeamId = dto.ChampionTeamId;
            existing.LastModified = DateTime.UtcNow;
            existing.LastModifiedBy = userId.ToString();
            await _unitOfWork.Liguilla.UpdateBracketAsync(existing, ct);
        }

        await _unitOfWork.Save(ct);

        var user = await _unitOfWork.Users.GetAsync(userId);
        var member = await _unitOfWork.QuinielaMembers.GetMembershipAsync(quinielaId, userId);

        return MapBracketDetail(existing, user, member?.Alias, nodes);
    }

    public async Task<LiguillaStandingsDto> GetStandingsAsync(int quinielaId, CancellationToken ct = default)
    {
        var quiniela = await _unitOfWork.Quinielas.GetAsync(quinielaId);
        if (quiniela == null)
            throw new KeyNotFoundException($"No se encontró la quiniela con Id {quinielaId}.");

        var currentSeason = quiniela.LeagueId.HasValue ? await _unitOfWork.Seasons.GetCurrentSeasonAsync(quiniela.LeagueId.Value) : null;
        var nodes = currentSeason != null ? await _unitOfWork.Liguilla.GetNodesBySeasonAsync(currentSeason.Id, ct) : new List<LiguillaNode>();

        var brackets = await _unitOfWork.Liguilla.GetAllBracketsForQuinielaAsync(quinielaId, ct);
        var members = await _unitOfWork.QuinielaMembers.GetMembersAsync(quinielaId);
        var memberMap = members.ToDictionary(m => m.UserId, m => m);

        var finalNode = nodes.FirstOrDefault(n => n.NodeNumber == 7);
        bool finalResolved = finalNode != null && finalNode.IsResolved && finalNode.WinnerTeamId.HasValue;

        var evaluatedItems = new List<LiguillaStandingItemDto>();

        foreach (var bracket in brackets)
        {
            var (qfPts, sfPts, champPts, bonusPts, totalPts, _) = FmfLiguillaEngine.EvaluateBracket(bracket, nodes);

            if (bracket.TotalScore != totalPts || bracket.QfPoints != qfPts || bracket.SfPoints != sfPts || bracket.ChampionPoints != champPts)
            {
                bracket.QfPoints = qfPts;
                bracket.SfPoints = sfPts;
                bracket.ChampionPoints = champPts;
                bracket.BonusPoints = bonusPts;
                bracket.TotalScore = totalPts;
                await _unitOfWork.Liguilla.UpdateBracketAsync(bracket, ct);
            }

            memberMap.TryGetValue(bracket.UserId, out var member);
            bool champPickedCorrectly = finalResolved && bracket.ChampionTeamId == finalNode!.WinnerTeamId!.Value;

            evaluatedItems.Add(new LiguillaStandingItemDto
            {
                UserId = bracket.UserId,
                Alias = member?.Alias ?? bracket.User?.Username ?? "Participante",
                AvatarUrl = bracket.User?.AvatarUrl,
                TotalPoints = totalPts,
                QfPoints = qfPts,
                SfPoints = sfPts,
                ChampionPoints = champPts,
                BonusPoints = bonusPts,
                ChampionPickedCorrectly = champPickedCorrectly,
                SubmittedAtUtc = bracket.SubmittedAtUtc
            });
        }

        await _unitOfWork.Save(ct);

        // Desempates en cascada de "El Pilón":
        // 1. TotalPoints desc
        // 2. ChampionPickedCorrectly desc
        // 3. SfPoints desc
        // 4. QfPoints desc
        // 5. SubmittedAtUtc asc
        var sorted = evaluatedItems
            .OrderByDescending(x => x.TotalPoints)
            .ThenByDescending(x => x.ChampionPickedCorrectly)
            .ThenByDescending(x => x.SfPoints)
            .ThenByDescending(x => x.QfPoints)
            .ThenBy(x => x.SubmittedAtUtc)
            .ToList();

        for (int i = 0; i < sorted.Count; i++)
        {
            sorted[i].Rank = i + 1;
        }

        return new LiguillaStandingsDto
        {
            QuinielaId = quinielaId,
            SeasonName = currentSeason?.Name ?? "Liguilla",
            Standings = sorted
        };
    }

    public async Task<bool> ResolveNodeAsync(int quinielaId, ResolveLiguillaNodeRequestDto dto, CancellationToken ct = default)
    {
        var node = await _unitOfWork.Liguilla.GetNodeByIdAsync(dto.NodeId, ct);
        if (node == null)
            throw new KeyNotFoundException($"No se encontró el nodo con Id {dto.NodeId}.");

        node.WinnerTeamId = dto.WinnerTeamId;
        node.IsResolved = true;
        node.ResolvedAtUtc = DateTime.UtcNow;
        node.LastModified = DateTime.UtcNow;

        await _unitOfWork.Liguilla.UpdateNodeAsync(node, ct);

        // Si se están resolviendo Cuartos de Final (1 al 4) y los 4 ya quedaron listos:
        var allNodes = await _unitOfWork.Liguilla.GetNodesBySeasonAsync(node.SeasonId, ct);
        var qfNodes = allNodes.Where(n => n.NodeNumber >= 1 && n.NodeNumber <= 4).ToList();

        if (qfNodes.All(n => n.IsResolved && n.WinnerTeamId.HasValue))
        {
            // Aplicar Art. 10 de la FMF para sembrar Semifinales (Nodos 5 y 6)
            var qfWinnerSeeds = new List<TeamSeedInfo>();
            foreach (var qf in qfNodes)
            {
                int winnerId = qf.WinnerTeamId!.Value;
                int seed = (winnerId == qf.TeamAId && qf.SeedA.HasValue) ? qf.SeedA.Value
                         : (winnerId == qf.TeamBId && qf.SeedB.HasValue) ? qf.SeedB.Value
                         : 8;
                qfWinnerSeeds.Add(new TeamSeedInfo(winnerId, seed));
            }

            var (semi1, semi2) = FmfLiguillaEngine.CalculateFmfReSeed(qfWinnerSeeds);

            var sfNode1 = allNodes.FirstOrDefault(n => n.NodeNumber == 5);
            if (sfNode1 != null)
            {
                sfNode1.TeamAId = semi1.TeamA.TeamId;
                sfNode1.SeedA = semi1.TeamA.Seed;
                sfNode1.TeamBId = semi1.TeamB.TeamId;
                sfNode1.SeedB = semi1.TeamB.Seed;
                await _unitOfWork.Liguilla.UpdateNodeAsync(sfNode1, ct);
            }

            var sfNode2 = allNodes.FirstOrDefault(n => n.NodeNumber == 6);
            if (sfNode2 != null)
            {
                sfNode2.TeamAId = semi2.TeamA.TeamId;
                sfNode2.SeedA = semi2.TeamA.Seed;
                sfNode2.TeamBId = semi2.TeamB.TeamId;
                sfNode2.SeedB = semi2.TeamB.Seed;
                await _unitOfWork.Liguilla.UpdateNodeAsync(sfNode2, ct);
            }
        }

        // Si Semifinales ya están listas, sembrar la Gran Final (Nodo 7)
        var sfNodes = allNodes.Where(n => n.NodeNumber == 5 || n.NodeNumber == 6).ToList();
        if (sfNodes.Count == 2 && sfNodes.All(n => n.IsResolved && n.WinnerTeamId.HasValue))
        {
            var finalNode = allNodes.FirstOrDefault(n => n.NodeNumber == 7);
            if (finalNode != null)
            {
                finalNode.TeamAId = sfNodes[0].WinnerTeamId;
                finalNode.TeamBId = sfNodes[1].WinnerTeamId;
                await _unitOfWork.Liguilla.UpdateNodeAsync(finalNode, ct);
            }
        }

        await _unitOfWork.Save(ct);

        // Recalcular standings de la quiniela
        await GetStandingsAsync(quinielaId, ct);
        return true;
    }

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
        TeamA = node.TeamA != null && node.SeedA.HasValue ? MapTeam(node.TeamA, node.SeedA.Value) : null,
        TeamB = node.TeamB != null && node.SeedB.HasValue ? MapTeam(node.TeamB, node.SeedB.Value) : null,
        WinnerTeam = node.WinnerTeam != null ? MapTeam(node.WinnerTeam, 0) : null,
        IsResolved = node.IsResolved,
        ResolvedAtUtc = node.ResolvedAtUtc,
        KickoffUtc = node.KickoffUtc
    };

    private static LiguillaBracketDetailDto MapBracketDetail(
        LiguillaBracket bracket,
        User? user,
        string? alias,
        IReadOnlyList<LiguillaNode> nodes)
    {
        var (qfPts, sfPts, champPts, bonusPts, totalPts, shattered) = FmfLiguillaEngine.EvaluateBracket(bracket, nodes);

        return new LiguillaBracketDetailDto
        {
            Id = bracket.Id,
            UserId = bracket.UserId,
            UserName = user?.Username ?? "",
            Alias = alias ?? user?.DisplayName ?? user?.Username ?? "",
            AvatarUrl = user?.AvatarUrl,
            IsLocked = bracket.IsLocked,
            SubmittedAtUtc = bracket.SubmittedAtUtc,
            TotalScore = totalPts,
            QfWinner1TeamId = bracket.QfWinner1TeamId,
            QfWinner2TeamId = bracket.QfWinner2TeamId,
            QfWinner3TeamId = bracket.QfWinner3TeamId,
            QfWinner4TeamId = bracket.QfWinner4TeamId,
            SfWinner1TeamId = bracket.SfWinner1TeamId,
            SfWinner2TeamId = bracket.SfWinner2TeamId,
            ChampionTeamId = bracket.ChampionTeamId,
            QfPoints = qfPts,
            SfPoints = sfPts,
            ChampionPoints = champPts,
            BonusPoints = bonusPts,
            ShatteredTeamIds = shattered
        };
    }

    public async Task<bool> SeedSimulationNodesAsync(int quinielaId, CancellationToken ct = default)
    {
        var quiniela = await _unitOfWork.Quinielas.GetAsync(quinielaId);
        if (quiniela == null)
            throw new KeyNotFoundException($"No se encontró la quiniela con Id {quinielaId}.");

        if (!quiniela.LeagueId.HasValue)
            throw new InvalidOperationException("La quiniela no tiene una liga asignada.");

        var currentSeason = await _unitOfWork.Seasons.GetCurrentSeasonAsync(quiniela.LeagueId.Value);
        if (currentSeason == null)
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
        var existingNodes = await _unitOfWork.Liguilla.GetNodesBySeasonAsync(currentSeason.Id, ct);
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
                    SeasonId = currentSeason.Id,
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
                    SeasonId = currentSeason.Id,
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
