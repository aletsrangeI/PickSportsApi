using Common;
using Domain.Entities;
using DTO.Bulletin;
using DTO.Scoring;
using Interface.Persistence;
using Interface.UseCases;
using UseCases.Scoring;

namespace UseCases.Bulletin;

public class BulletinApplication : IBulletinApplication
{
    private const int AnnouncementMaxLength = 2000;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ScoringEngine _scoringEngine;

    public BulletinApplication(IUnitOfWork unitOfWork, ScoringEngine scoringEngine)
    {
        _unitOfWork = unitOfWork;
        _scoringEngine = scoringEngine;
    }

    public async Task<Response<WeeklyBulletinDto>> GetBulletinAsync(int quinielaId, int? weekId)
    {
        var response = new Response<WeeklyBulletinDto>();

        var quiniela = await _unitOfWork.Quinielas.GetAsync(quinielaId);
        if (quiniela == null)
        {
            response.isSuccess = false;
            response.Message = "Quiniela no encontrada.";
            return response;
        }

        Week? week;
        if (weekId.HasValue && weekId.Value > 0)
        {
            week = await _unitOfWork.Weeks.GetAsync(weekId.Value);
        }
        else
        {
            week = await ResolveLatestEditionWeekAsync(quiniela.LeagueId);
        }

        if (week == null)
        {
            response.isSuccess = false;
            response.Message = weekId.HasValue
                ? "Jornada no encontrada."
                : "Aún no hay jornadas publicadas para esta quiniela.";
            return response;
        }

        var members = (await _unitOfWork.QuinielaMembers.GetMembersAsync(quinielaId)).ToList();
        var matches = (await _unitOfWork.Matches.GetByWeekIdAsync(week.Id)).ToList();
        var weekPicks = (await _unitOfWork.Picks.GetAllPicksForWeekAsync(quinielaId, week.Id)).ToList();

        var standings = _scoringEngine.CalculateWeeklyStandings(members, weekPicks, matches);
        var podium = BuildPodium(standings);
        var awards = BuildAwards(quinielaId, week.Id, standings, matches, weekPicks);

        var bulletin = await _unitOfWork.WeeklyBulletins.GetByQuinielaAndWeekAsync(quinielaId, week.Id);
        bool isOfficial = string.Equals(week.Status, "SCORED", StringComparison.OrdinalIgnoreCase);

        // Auto-publicación: en cuanto la jornada está calificada se emite la edición oficial
        if (bulletin == null && isOfficial)
        {
            bulletin = new WeeklyBulletin
            {
                QuinielaId = quinielaId,
                WeekId = week.Id,
                AdminAnnouncement = null,
                PublishedAtUtc = week.ScoredAt ?? DateTime.UtcNow,
                IsPublished = true,
                Active = true
            };
            await _unitOfWork.WeeklyBulletins.InsertAsync(bulletin);
        }

        var nextWeekInfo = await BuildNextWeekInfoAsync(week);

        response.isSuccess = true;
        response.Message = isOfficial
            ? $"Edición oficial de la Jornada {week.WeekNumber}."
            : $"Edición preliminar de la Jornada {week.WeekNumber} (la jornada no ha sido calificada).";
        response.Data = new WeeklyBulletinDto
        {
            QuinielaId = quinielaId,
            WeekId = week.Id,
            WeekNumber = week.WeekNumber,
            WeekName = week.Name,
            WeekStatus = week.Status,
            IsOfficial = isOfficial,
            WeekEndDate = week.EndDate,
            PublishedAtUtc = bulletin?.IsPublished == true ? bulletin.PublishedAtUtc : null,
            AdminAnnouncement = bulletin?.AdminAnnouncement,
            Podium = podium,
            Awards = awards,
            NextWeekInfo = nextWeekInfo
        };

        return response;
    }

    public async Task<Response<WeeklyBulletinDto>> UpdateAnnouncementAsync(int quinielaId, int weekId, int userId, UpdateAnnouncementDto dto)
    {
        var response = new Response<WeeklyBulletinDto>();

        var quiniela = await _unitOfWork.Quinielas.GetAsync(quinielaId);
        if (quiniela == null)
        {
            response.isSuccess = false;
            response.Message = "Quiniela no encontrada.";
            return response;
        }

        var membership = await _unitOfWork.QuinielaMembers.GetMembershipAsync(quinielaId, userId);
        if (membership == null || (membership.Role != "ADMIN" && membership.Role != "OWNER"))
        {
            response.isSuccess = false;
            response.Message = "Solo los administradores u owners pueden publicar anuncios en el boletín.";
            return response;
        }

        var week = await _unitOfWork.Weeks.GetAsync(weekId);
        if (week == null)
        {
            response.isSuccess = false;
            response.Message = "Jornada no encontrada.";
            return response;
        }

        var announcement = dto?.Announcement?.Trim();
        if (announcement != null && announcement.Length > AnnouncementMaxLength)
        {
            response.isSuccess = false;
            response.Message = $"El anuncio excede el máximo de {AnnouncementMaxLength} caracteres.";
            return response;
        }

        if (string.IsNullOrWhiteSpace(announcement))
        {
            announcement = null;
        }

        var bulletin = await _unitOfWork.WeeklyBulletins.GetByQuinielaAndWeekAsync(quinielaId, weekId);
        if (bulletin == null)
        {
            bulletin = new WeeklyBulletin
            {
                QuinielaId = quinielaId,
                WeekId = weekId,
                AdminAnnouncement = announcement,
                PublishedAtUtc = DateTime.UtcNow,
                IsPublished = true,
                Active = true
            };
            await _unitOfWork.WeeklyBulletins.InsertAsync(bulletin);
        }
        else
        {
            bulletin.AdminAnnouncement = announcement;
            bulletin.IsPublished = true;
            if (bulletin.PublishedAtUtc == default)
            {
                bulletin.PublishedAtUtc = DateTime.UtcNow;
            }
            bulletin.LastModified = DateTime.UtcNow;
            await _unitOfWork.WeeklyBulletins.UpdateAsync(bulletin);
        }

        var bulletinResponse = await GetBulletinAsync(quinielaId, weekId);
        bulletinResponse.Message = bulletinResponse.isSuccess
            ? "Anuncio del administrador actualizado correctamente."
            : bulletinResponse.Message;

        return bulletinResponse;
    }

    private async Task<Week?> ResolveLatestEditionWeekAsync(int leagueId)
    {
        var season = await _unitOfWork.Seasons.GetCurrentSeasonAsync(leagueId);
        if (season == null) return null;

        var weeks = (await _unitOfWork.Weeks.GetBySeasonIdAsync(season.Id)).ToList();
        if (weeks.Count == 0) return null;

        return weeks
                   .Where(w => string.Equals(w.Status, "SCORED", StringComparison.OrdinalIgnoreCase))
                   .OrderByDescending(w => w.WeekNumber)
                   .FirstOrDefault()
               ?? weeks.FirstOrDefault(w =>
                   string.Equals(w.Status, "PUBLISHED", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(w.Status, "LOCKED", StringComparison.OrdinalIgnoreCase))
               ?? weeks.OrderByDescending(w => w.WeekNumber).FirstOrDefault();
    }

    private static List<PodiumMemberDto> BuildPodium(List<MemberStandingDto> standings)
    {
        return standings
            .Take(3)
            .Select(s => new PodiumMemberDto
            {
                Position = s.Rank,
                MemberId = s.MemberId,
                UserId = s.UserId,
                Alias = s.Alias,
                DisplayName = s.DisplayName,
                AvatarUrl = s.AvatarUrl,
                Hits = s.Hits,
                TotalPicks = s.TotalPicks,
                AccuracyPct = s.AccuracyPct,
                UpsetHits = s.UpsetHits,
                Humillaciones = s.Humillaciones
            })
            .ToList();
    }

    private BulletinAwardsDto BuildAwards(
        int quinielaId,
        int weekId,
        List<MemberStandingDto> standings,
        List<Match> matches,
        List<Pick> weekPicks)
    {
        var awards = new BulletinAwardsDto();

        var computedAwards = _scoringEngine.CalculateAwards(quinielaId, weekId, standings, matches, weekPicks);
        var standingByMember = standings.ToDictionary(s => s.MemberId);

        List<BulletinWinnerDto> MapWinners(string awardType)
        {
            return computedAwards
                .Where(a => a.AwardType == awardType)
                .Select(a =>
                {
                    standingByMember.TryGetValue(a.MemberId, out var standing);
                    return new BulletinWinnerDto
                    {
                        MemberId = a.MemberId,
                        Alias = standing?.Alias ?? "Anónimo",
                        DisplayName = standing?.DisplayName,
                        AvatarUrl = standing?.AvatarUrl,
                        Value = a.AwardValue1,
                        SecondaryValue = a.AwardValue2,
                        Notes = a.Notes
                    };
                })
                .ToList();
        }

        awards.Mvp = MapWinners("MVP");
        awards.SurpriseKing = MapWinners("REY_SORPRESAS");
        awards.Humillado = MapWinners("HUMILLADO");
        awards.Somnifero = MapWinners("SOMNIFERO");
        awards.EmpateFallido = MapWinners("EMPATE_FALLIDO");
        awards.RompeQuinielas = BuildRompeQuinielas(matches, weekPicks);

        return awards;
    }

    private static BulletinMatchAwardDto? BuildRompeQuinielas(List<Match> matches, List<Pick> weekPicks)
    {
        var finishedMatches = matches
            .Where(m => string.Equals(m.StatusState, "post", StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(m.WinnerAbbr))
            .ToList();

        BulletinMatchAwardDto? hardest = null;

        foreach (var match in finishedMatches)
        {
            var matchPicks = weekPicks.Where(p => p.MatchId == match.Id).ToList();
            if (matchPicks.Count == 0) continue;

            var winner = match.WinnerAbbr!.Trim().ToUpperInvariant();
            int correct = matchPicks.Count(p => string.Equals(p.PickAbbr?.Trim(), winner, StringComparison.OrdinalIgnoreCase));
            decimal accuracy = Math.Round((decimal)correct / matchPicks.Count * 100m, 1);

            if (hardest == null || accuracy < hardest.AccuracyPct)
            {
                hardest = new BulletinMatchAwardDto
                {
                    MatchLabel = $"{match.HomeTeam?.Abbreviation ?? "LOC"} vs {match.AwayTeam?.Abbreviation ?? "VIS"}",
                    WinnerAbbr = winner,
                    AccuracyPct = accuracy,
                    CorrectPicks = correct,
                    TotalPicks = matchPicks.Count,
                    Notes = $"Ganador: {winner}. Solo {correct} de {matchPicks.Count} participantes acertaron ({accuracy}%)"
                };
            }
        }

        return hardest;
    }

    private async Task<NextWeekInfoDto?> BuildNextWeekInfoAsync(Week currentWeek)
    {
        var seasonWeeks = (await _unitOfWork.Weeks.GetBySeasonIdAsync(currentWeek.SeasonId)).ToList();
        var nextWeek = seasonWeeks.FirstOrDefault(w => w.WeekNumber == currentWeek.WeekNumber + 1);
        if (nextWeek == null) return null;

        var firstGameUtc = nextWeek.FirstGameUtc;
        if (firstGameUtc == null)
        {
            var nextMatches = (await _unitOfWork.Matches.GetByWeekIdAsync(nextWeek.Id)).ToList();
            firstGameUtc = nextMatches
                .Where(m => m.DateUtc != default)
                .OrderBy(m => m.DateUtc)
                .Select(m => (DateTime?)m.DateUtc)
                .FirstOrDefault();
        }

        return new NextWeekInfoDto
        {
            WeekId = nextWeek.Id,
            WeekNumber = nextWeek.WeekNumber,
            WeekName = nextWeek.Name,
            Status = nextWeek.Status,
            FirstGameUtc = firstGameUtc
        };
    }
}
