using Common;
using Domain.Entities;
using Domain.Gamification;
using DTO.Gamification;
using Interface.Persistence;
using Interface.UseCases;
using UseCases.Scoring;

namespace UseCases.Gamification;

/// <summary>SPEC-013: evaluacion, entrega y consulta de trofeos (por temporada).</summary>
public class AchievementApplication : IAchievementApplication
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly AchievementEvaluator _evaluator;

    public AchievementApplication(IUnitOfWork unitOfWork, ScoringEngine? scoringEngine = null)
    {
        _unitOfWork = unitOfWork;
        _evaluator = new AchievementEvaluator(scoringEngine);
    }

    public async Task EvaluateWeekAsync(int quinielaId, int weekId)
    {
        var week = await _unitOfWork.Weeks.GetAsync(weekId);
        if (week == null) return;
        int seasonId = week.SeasonId;

        var seasonPicks = (await _unitOfWork.Picks.GetAllPicksForQuinielaAsync(quinielaId))
            .Where(p => p.Match?.Week?.SeasonId == seasonId)
            .ToList();
        var members = (await _unitOfWork.QuinielaMembers.GetMembersAsync(quinielaId)).ToList();
        var awards = (await _unitOfWork.WeeklyAwards.GetAllAwardsForQuinielaAsync(quinielaId)).ToList();

        // Partidos reales de cada jornada de la temporada con picks (sin los pospuestos), para JORNADA_COMPLETA / SEMANA_INVICTA.
        var realMatchIdsByWeek = new Dictionary<int, HashSet<int>>();
        foreach (var weekKey in seasonPicks.Select(AchievementEvaluator.WeekKey).Distinct())
        {
            var weekMatches = await _unitOfWork.Matches.GetByWeekIdAsync(weekKey);
            realMatchIdsByWeek[weekKey] = weekMatches
                .Where(m => !string.Equals(m.StatusState, "postponed", StringComparison.OrdinalIgnoreCase))
                .Select(m => m.Id)
                .ToHashSet();
        }

        var earned = _evaluator.Evaluate(members, seasonPicks, awards, realMatchIdsByWeek);

        foreach (var member in members)
        {
            earned.TryGetValue(member.Id, out var codes);
            if (codes == null) continue;

            foreach (var code in codes)
            {
                await TryInsertAsync(member.UserId, code, quinielaId, seasonId);
            }

            // COLECCIONISTA_DE_BRONCE: todos los Bronce de la temporada, contando los recien otorgados.
            bool hasAllBronze = true;
            foreach (var bronze in AchievementCatalog.BronzeAchievements)
            {
                if (codes.Contains(bronze.Code)) continue; // ya cumplido en esta pasada (otorgado ahora o antes)
                if (!await _unitOfWork.Achievements.ExistsAsync(member.UserId, bronze.Code, seasonId))
                {
                    hasAllBronze = false;
                    break;
                }
            }

            if (hasAllBronze)
            {
                await TryInsertAsync(member.UserId, AchievementCatalog.ColeccionistaDeBronce, quinielaId, seasonId);
            }
        }
    }

    public async Task<bool> GrantAsync(int userId, string code, int quinielaId, int seasonId)
    {
        var definition = AchievementCatalog.Find(code);
        if (definition == null) return false;
        return await TryInsertAsync(userId, definition.Code, quinielaId, seasonId);
    }

    private async Task<bool> TryInsertAsync(int userId, string code, int quinielaId, int seasonId)
    {
        if (await _unitOfWork.Achievements.ExistsAsync(userId, code, seasonId)) return false;

        try
        {
            await _unitOfWork.Achievements.InsertAsync(new UserAchievement
            {
                UserId = userId,
                Code = code,
                QuinielaId = quinielaId,
                SeasonId = seasonId,
                UnlockedAt = DateTime.UtcNow,
                IsSeen = false,
                Active = true
            });
            return true;
        }
        catch (Exception)
        {
            // Carrera: otra calificacion concurrente inserto el mismo (UserId, Code, SeasonId) y el indice unico rechazo este insert.
            // Si el trofeo ya existe no es un error y la pasada sigue; si no existe, el fallo es otro y se propaga.
            if (await _unitOfWork.Achievements.ExistsAsync(userId, code, seasonId)) return false;
            throw;
        }
    }

    public async Task<Response<IEnumerable<AchievementDto>>> GetUnseenAsync(int userId)
    {
        var unseen = (await _unitOfWork.Achievements.GetUnseenAsync(userId)).ToList();
        return new Response<IEnumerable<AchievementDto>>
        {
            isSuccess = true,
            Message = "Trofeos nuevos obtenidos correctamente.",
            Data = unseen.Select(ToDto).ToList()
        };
    }

    public async Task<Response<AchievementDto>> AcknowledgeAsync(int userId, int achievementId)
    {
        var response = new Response<AchievementDto>();
        var achievement = await _unitOfWork.Achievements.GetByIdAsync(achievementId);
        if (achievement == null || achievement.UserId != userId)
        {
            response.isSuccess = false;
            response.Message = "Trofeo no encontrado.";
            return response;
        }

        if (!achievement.IsSeen)
        {
            achievement.IsSeen = true;
            achievement.SeenAt = DateTime.UtcNow;
            await _unitOfWork.Achievements.UpdateAsync(achievement);
        }

        response.isSuccess = true;
        response.Message = "Trofeo confirmado.";
        response.Data = ToDto(achievement);
        return response;
    }

    public async Task<Response<AchievementShowcaseDto>> GetShowcaseAsync(int userId, int? seasonId)
    {
        var response = new Response<AchievementShowcaseDto>();
        var user = await _unitOfWork.Users.GetAsync(userId);
        if (user == null)
        {
            response.isSuccess = false;
            response.Message = "Usuario no encontrado.";
            return response;
        }

        var all = (await _unitOfWork.Achievements.GetByUserAsync(userId)).ToList();

        int? effectiveSeasonId = seasonId ?? await ResolveLatestSeasonIdAsync(userId);

        var showcase = new AchievementShowcaseDto
        {
            EquippedTitleCode = user.EquippedTitleCode,
            EquippedTitle = string.IsNullOrWhiteSpace(user.EquippedTitleCode)
                ? null
                : AchievementCatalog.Find(user.EquippedTitleCode)?.TitleName
        };

        // Temporadas con trofeos (con su Gamerscore), de la mas reciente a la mas antigua.
        // Mismo criterio que ResolveLatestSeasonIdAsync: Year descendente, desempate por Id descendente.
        var seasonEntries = new List<(int Id, int Year, SeasonScoreDto Dto)>();
        foreach (var group in all.GroupBy(a => a.SeasonId))
        {
            var season = await _unitOfWork.Seasons.GetAsync(group.Key);
            seasonEntries.Add((group.Key, season?.Year ?? int.MinValue, new SeasonScoreDto
            {
                SeasonId = group.Key,
                SeasonName = season?.Name ?? $"Temporada {group.Key}",
                Gamerscore = group.Sum(GamerscoreOf)
            }));
        }
        showcase.AvailableSeasons.AddRange(seasonEntries.OrderByDescending(e => e.Year).ThenByDescending(e => e.Id).Select(e => e.Dto));

        var unlocked = effectiveSeasonId.HasValue
            ? all.Where(a => a.SeasonId == effectiveSeasonId.Value).ToList()
            : new List<UserAchievement>();

        int gamerscore = unlocked.Sum(GamerscoreOf);
        showcase.SeasonId = effectiveSeasonId;
        if (effectiveSeasonId.HasValue)
        {
            var season = await _unitOfWork.Seasons.GetAsync(effectiveSeasonId.Value);
            showcase.SeasonName = season?.Name ?? $"Temporada {effectiveSeasonId.Value}";
        }
        showcase.Gamerscore = gamerscore;
        showcase.Level = AchievementCatalog.LevelFor(gamerscore);
        showcase.NextLevelAt = AchievementCatalog.NextLevelAt(gamerscore);
        showcase.Unlocked = unlocked.OrderBy(a => a.UnlockedAt).Select(ToDto).ToList();

        var unlockedCodes = unlocked.Select(a => a.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        showcase.Locked = AchievementCatalog.All
            .Where(d => !unlockedCodes.Contains(d.Code))
            .Select(d => new LockedAchievementDto
            {
                Code = d.Code,
                Name = d.Name,
                Description = d.Description,
                Tier = d.Tier,
                Gamerscore = d.Gamerscore
            })
            .ToList();

        response.isSuccess = true;
        response.Message = "Vitrina obtenida correctamente.";
        response.Data = showcase;
        return response;
    }

    public async Task<Response<AchievementShowcaseDto>> EquipTitleAsync(int userId, string? code)
    {
        var response = new Response<AchievementShowcaseDto>();
        var user = await _unitOfWork.Users.GetAsync(userId);
        if (user == null)
        {
            response.isSuccess = false;
            response.Message = "Usuario no encontrado.";
            return response;
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            user.EquippedTitleCode = null;
        }
        else
        {
            var definition = AchievementCatalog.Find(code);
            var owned = (await _unitOfWork.Achievements.GetByUserAsync(userId)).ToList();
            bool unlocked = definition?.TitleName != null
                            && owned.Any(a => string.Equals(a.Code, definition.Code, StringComparison.OrdinalIgnoreCase));
            if (!unlocked)
            {
                response.isSuccess = false;
                response.Message = "No has desbloqueado ese título.";
                return response;
            }

            user.EquippedTitleCode = definition!.Code;
        }

        await _unitOfWork.Users.UpdateAsync(user);
        return await GetShowcaseAsync(userId, null);
    }

    /// <summary>Temporada mas reciente de las quinielas con gamificacion activa donde el usuario es miembro.</summary>
    private async Task<int?> ResolveLatestSeasonIdAsync(int userId)
    {
        var quinielas = (await _unitOfWork.Quinielas.GetUserQuinielasAsync(userId))
            .Where(q => q.GamificationEnabled && q.SeasonId.HasValue)
            .Select(q => q.SeasonId!.Value)
            .Distinct()
            .ToList();
        if (quinielas.Count == 0) return null;
        if (quinielas.Count == 1) return quinielas[0];

        int? best = null;
        int bestYear = int.MinValue;
        foreach (var id in quinielas)
        {
            var season = await _unitOfWork.Seasons.GetAsync(id);
            int year = season?.Year ?? int.MinValue;
            if (best == null || year > bestYear || (year == bestYear && id > best))
            {
                best = id;
                bestYear = year;
            }
        }
        return best;
    }

    private static int GamerscoreOf(UserAchievement a) => AchievementCatalog.Find(a.Code)?.Gamerscore ?? 0;

    private static AchievementDto ToDto(UserAchievement a)
    {
        var def = AchievementCatalog.Find(a.Code);
        return new AchievementDto
        {
            Id = a.Id,
            Code = a.Code,
            Name = def?.Name ?? a.Code,
            Description = def?.Description ?? string.Empty,
            Tier = def?.Tier ?? string.Empty,
            Gamerscore = def?.Gamerscore ?? 0,
            TitleName = def?.TitleName,
            SeasonId = a.SeasonId,
            UnlockedAt = a.UnlockedAt,
            IsSeen = a.IsSeen
        };
    }
}
