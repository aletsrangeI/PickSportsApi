using Common;
using Common.Security;
using Domain.Entities;
using DTO.Migration;
using Interface.Persistence;
using Interface.UseCases;
using Microsoft.Extensions.Logging;
using UseCases.Scoring;

namespace UseCases.Migration;

public class QuinielaMigrationService : IQuinielaMigrationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IXlsxParserService _parserService;
    private readonly ScoringEngine _scoringEngine;
    private readonly PasswordHasher _passwordHasher;
    private readonly ILogger<QuinielaMigrationService> _logger;

    public QuinielaMigrationService(
        IUnitOfWork unitOfWork,
        IXlsxParserService parserService,
        ScoringEngine scoringEngine,
        PasswordHasher passwordHasher,
        ILogger<QuinielaMigrationService> logger)
    {
        _unitOfWork = unitOfWork;
        _parserService = parserService;
        _scoringEngine = scoringEngine;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<Response<MigrationPreviewDto>> PreviewMigrationAsync(Stream fileStream, CancellationToken cancellationToken = default)
    {
        var response = new Response<MigrationPreviewDto>();

        try
        {
            var data = await _parserService.ParseAsync(fileStream, cancellationToken);
            if (!data.Matches.Any() || !data.Picks.Any())
            {
                response.isSuccess = false;
                response.Message = "El archivo XLSX no contiene partidos o pronósticos válidos.";
                return response;
            }

            var preview = new MigrationPreviewDto
            {
                SeasonYear = data.Config.SeasonYear > 0 ? data.Config.SeasonYear : 2026,
                SeasonType = data.Config.SeasonType > 0 ? data.Config.SeasonType : 1,
                LeagueCode = "mex.1",
                SuggestedQuinielaName = "Liga MX Clausura 2026",
                TotalParticipants = data.Standings.Count > 0 ? data.Standings.Count : data.Picks.Select(p => p.PlayerAlias).Distinct().Count(),
                TotalMatches = data.Matches.Count,
                TotalWeeks = data.Weeks.Count > 0 ? data.Weeks.Count : data.Matches.Select(m => m.WeekNumber).DefaultIfEmpty(0).Max(),
                TotalPicks = data.Picks.Count,
                CurrentWeekNumber = data.Config.CurrentWeek > 0 ? data.Config.CurrentWeek : 10
            };

            // Participants
            var standingsList = data.Standings.Any()
                ? data.Standings
                : data.Picks.GroupBy(p => p.PlayerAlias).Select(g => new XlsxStanding { PlayerAlias = g.Key, TotalPicks = g.Count() }).ToList();

            foreach (var st in standingsList)
            {
                var cleanName = ClosedXmlParserService.RemoveDiacritics(st.PlayerAlias).ToLowerInvariant().Replace(" ", "");
                var email = $"{cleanName}@quiniela.local";

                var existing = await _unitOfWork.Users.GetByEmailAsync(email);
                if (existing == null)
                {
                    existing = await _unitOfWork.Users.GetByUsernameAsync(cleanName);
                }

                preview.Participants.Add(new MigrationPlayerPreviewDto
                {
                    Alias = st.PlayerAlias,
                    ExpectedHits = st.Hits,
                    TotalPicks = st.TotalPicks,
                    Percentage = Math.Round(st.Pct * 100m, 1),
                    IsExistingUser = existing != null,
                    ExistingUserId = existing?.Id,
                    ExistingDisplayName = existing?.DisplayName
                });
            }

            // Weeks summary
            var matchesByWeek = data.Matches.GroupBy(m => m.WeekNumber).ToDictionary(g => g.Key, g => g.ToList());
            var gameIdsByWeek = data.Matches.GroupBy(m => m.WeekNumber)
                .ToDictionary(g => g.Key, g => g.Select(m => m.EspnGameId).ToHashSet());

            var maxWeek = Math.Max(10, matchesByWeek.Keys.DefaultIfEmpty(0).Max());
            for (int w = 1; w <= maxWeek; w++)
            {
                int matchCount = matchesByWeek.TryGetValue(w, out var wMatches) ? wMatches.Count : 0;
                var weekGameIds = gameIdsByWeek.TryGetValue(w, out var gIds) ? gIds : new HashSet<string>();
                int pickCount = data.Picks.Count(p => weekGameIds.Contains(p.EspnGameId));

                var cj = data.ControlJornadas.FirstOrDefault(c => c.WeekNumber == w);
                string status = cj?.Status ?? (w < 10 ? "SCORED" : (w == 10 ? "PUBLISHED" : "DRAFT"));

                preview.WeeksSummary.Add(new MigrationWeekSummaryDto
                {
                    WeekNumber = w,
                    Status = status,
                    MatchesCount = matchCount,
                    PicksCount = pickCount
                });
            }

            // Sample matches
            preview.SampleMatches = data.Matches
                .Where(m => m.WeekNumber == 1 || m.WeekNumber == preview.CurrentWeekNumber)
                .Take(10)
                .Select(m => new MigrationMatchSampleDto
                {
                    GameId = m.EspnGameId,
                    WeekNumber = m.WeekNumber,
                    HomeTeam = $"{m.HomeName} ({m.HomeAbbr})",
                    AwayTeam = $"{m.AwayName} ({m.AwayAbbr})",
                    StatusState = m.StatusState,
                    HomeScore = m.StatusState == "post" ? m.HomeScore : null,
                    AwayScore = m.StatusState == "post" ? m.AwayScore : null,
                    WinnerAbbr = m.WinnerAbbr
                })
                .ToList();

            response.isSuccess = true;
            response.Message = "Previsualización generada exitosamente.";
            response.Data = preview;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar previsualización de migración");
            response.isSuccess = false;
            response.Message = $"Error al procesar el archivo: {ex.Message}";
        }

        return response;
    }

    public async Task<Response<MigrationResultDto>> ExecuteMigrationAsync(
        Stream fileStream,
        MigrationExecuteRequestDto request,
        int adminUserId,
        CancellationToken cancellationToken = default)
    {
        var response = new Response<MigrationResultDto>();

        try
        {
            var data = await _parserService.ParseAsync(fileStream, cancellationToken);
            if (!data.Matches.Any() || !data.Picks.Any() || !data.Standings.Any())
            {
                response.isSuccess = false;
                response.Message = "El archivo XLSX no contiene la estructura requerida (Matches, Picks, Standings).";
                return response;
            }

            var adminUser = await _unitOfWork.Users.GetAsync(adminUserId);
            if (adminUser == null)
            {
                response.isSuccess = false;
                response.Message = "El usuario administrador no existe.";
                return response;
            }

            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            // 1. League
            var allLeagues = (await _unitOfWork.Leagues.GetAllAsync()).ToList();
            var league = allLeagues.FirstOrDefault(l => string.Equals(l.Code, "mex.1", StringComparison.OrdinalIgnoreCase))
                         ?? allLeagues.FirstOrDefault(l => string.Equals(l.Name, "Liga MX", StringComparison.OrdinalIgnoreCase));

            if (league == null)
            {
                var sports = (await _unitOfWork.Sports.GetAllAsync()).ToList();
                var soccer = sports.FirstOrDefault(s => string.Equals(s.Code, "SOCCER", StringComparison.OrdinalIgnoreCase));
                if (soccer == null)
                {
                    soccer = new Sport { Code = "SOCCER", Name = "Fútbol", HasDraw = true, Icon = "futbol", Active = true, Created = DateTime.UtcNow };
                    await _unitOfWork.Sports.InsertAsync(soccer);
                    await _unitOfWork.Save(cancellationToken);
                }

                league = new League
                {
                    SportId = soccer.Id,
                    Code = "mex.1",
                    Name = "Liga MX",
                    Country = "México",
                    EspnApiPath = "sports/soccer/mex.1",
                    LogoUrl = "https://a.espncdn.com/i/leaguelogos/soccer/500/mex.1.png",
                    WeeksCount = 17,
                    WeekDurationDays = 7,
                    SportKey = "soccer",
                    Active = true,
                    Created = DateTime.UtcNow
                };
                await _unitOfWork.Leagues.InsertAsync(league);
                await _unitOfWork.Save(cancellationToken);
            }

            // 2. Season
            var seasons = (await _unitOfWork.Seasons.GetAllAsync()).Where(s => s.LeagueId == league.Id).ToList();
            var seasonYear = data.Config.SeasonYear > 0 ? data.Config.SeasonYear : 2026;
            var seasonType = data.Config.SeasonType > 0 ? data.Config.SeasonType : 1;

            var season = seasons.FirstOrDefault(s => s.Year == seasonYear && s.SeasonType == seasonType)
                         ?? seasons.FirstOrDefault(s => s.Year == seasonYear && s.Name.Contains("Clausura"));

            if (season == null)
            {
                season = new Season
                {
                    LeagueId = league.Id,
                    Year = seasonYear,
                    SeasonType = seasonType,
                    Name = $"Clausura {seasonYear}",
                    IsCurrent = true,
                    Active = true,
                    Created = DateTime.UtcNow
                };
                await _unitOfWork.Seasons.InsertAsync(season);
                await _unitOfWork.Save(cancellationToken);
            }

            // 3. Weeks (1 to 17)
            var existingWeeks = (await _unitOfWork.Weeks.GetBySeasonIdAsync(season.Id)).ToList();
            var weeksByNumber = new Dictionary<int, Week>();

            for (int w = 1; w <= 17; w++)
            {
                var existingWeek = existingWeeks.FirstOrDefault(ew => ew.WeekNumber == w);
                var xlsxWeek = data.Weeks.FirstOrDefault(xw => xw.WeekNumber == w);
                var cj = data.ControlJornadas.FirstOrDefault(c => c.WeekNumber == w);

                string status = w < 10 ? "SCORED" : (w == 10 ? "PUBLISHED" : "DRAFT");
                DateTime startDate = xlsxWeek?.StartDate ?? DateTime.UtcNow.AddDays((w - 10) * 7);
                DateTime endDate = xlsxWeek?.EndDate ?? startDate.AddDays(3);

                if (existingWeek == null)
                {
                    var newWeek = new Week
                    {
                        SeasonId = season.Id,
                        WeekNumber = w,
                        Name = $"Jornada {w}",
                        StartDate = startDate,
                        EndDate = endDate,
                        Status = status,
                        PublishedAt = cj?.PublishedAt ?? (w <= 10 ? DateTime.UtcNow : null),
                        LockedAt = cj?.LockedAt ?? (w < 10 ? DateTime.UtcNow : null),
                        ScoredAt = cj?.ScoredAt ?? (w < 10 ? DateTime.UtcNow : null),
                        Active = true,
                        Created = DateTime.UtcNow
                    };
                    await _unitOfWork.Weeks.InsertAsync(newWeek);
                    await _unitOfWork.Save(cancellationToken);
                    weeksByNumber[w] = newWeek;
                }
                else
                {
                    existingWeek.Status = status;
                    if (xlsxWeek?.StartDate.HasValue == true) existingWeek.StartDate = xlsxWeek.StartDate.Value;
                    if (xlsxWeek?.EndDate.HasValue == true) existingWeek.EndDate = xlsxWeek.EndDate.Value;
                    if (cj?.PublishedAt.HasValue == true) existingWeek.PublishedAt = cj.PublishedAt;
                    if (cj?.LockedAt.HasValue == true) existingWeek.LockedAt = cj.LockedAt;
                    if (cj?.ScoredAt.HasValue == true) existingWeek.ScoredAt = cj.ScoredAt;

                    await _unitOfWork.Weeks.UpdateAsync(existingWeek);
                    weeksByNumber[w] = existingWeek;
                }
            }
            await _unitOfWork.Save(cancellationToken);

            // 4. Teams
            var existingTeams = (await _unitOfWork.Teams.GetByLeagueIdAsync(league.Id)).ToList();
            var teamsByEspnId = existingTeams.Where(t => !string.IsNullOrEmpty(t.EspnTeamId)).ToDictionary(t => t.EspnTeamId, t => t);
            var teamsByAbbr = existingTeams.Where(t => !string.IsNullOrEmpty(t.Abbreviation)).ToDictionary(t => t.Abbreviation.ToUpperInvariant(), t => t);

            async Task<Team> EnsureTeamAsync(string espnId, string abbr, string name)
            {
                var cleanAbbr = abbr.Trim().ToUpperInvariant();
                var cleanEspnId = espnId.Trim();

                if (!string.IsNullOrEmpty(cleanEspnId) && teamsByEspnId.TryGetValue(cleanEspnId, out var teamByEspn))
                {
                    return teamByEspn;
                }
                if (!string.IsNullOrEmpty(cleanAbbr) && teamsByAbbr.TryGetValue(cleanAbbr, out var teamByAbbr))
                {
                    return teamByAbbr;
                }

                var newTeam = new Team
                {
                    LeagueId = league.Id,
                    EspnTeamId = string.IsNullOrEmpty(cleanEspnId) ? cleanAbbr : cleanEspnId,
                    Name = string.IsNullOrEmpty(name) ? cleanAbbr : name.Trim(),
                    DisplayName = string.IsNullOrEmpty(name) ? cleanAbbr : name.Trim(),
                    Abbreviation = cleanAbbr,
                    Active = true,
                    Created = DateTime.UtcNow
                };

                await _unitOfWork.Teams.InsertAsync(newTeam);
                await _unitOfWork.Save(cancellationToken);

                if (!string.IsNullOrEmpty(newTeam.EspnTeamId)) teamsByEspnId[newTeam.EspnTeamId] = newTeam;
                if (!string.IsNullOrEmpty(newTeam.Abbreviation)) teamsByAbbr[newTeam.Abbreviation] = newTeam;
                return newTeam;
            }

            // 5. Matches
            var matchesByGameId = new Dictionary<string, Domain.Entities.Match>();
            foreach (var xm in data.Matches)
            {
                var homeTeam = await EnsureTeamAsync(xm.HomeTeamEspnId, xm.HomeAbbr, xm.HomeName);
                var awayTeam = await EnsureTeamAsync(xm.AwayTeamEspnId, xm.AwayAbbr, xm.AwayName);

                if (!weeksByNumber.TryGetValue(xm.WeekNumber, out var weekEntity))
                {
                    weekEntity = weeksByNumber[1];
                }

                var existingMatch = await _unitOfWork.Matches.GetByEspnGameIdAsync(xm.EspnGameId);
                if (existingMatch == null)
                {
                    var newMatch = new Domain.Entities.Match
                    {
                        WeekId = weekEntity.Id,
                        EspnGameId = xm.EspnGameId,
                        DateUtc = xm.DateUtc,
                        HomeTeamId = homeTeam.Id,
                        AwayTeamId = awayTeam.Id,
                        StatusState = xm.StatusState,
                        StatusDesc = xm.StatusDesc,
                        HomeScore = xm.HomeScore,
                        AwayScore = xm.AwayScore,
                        WinnerAbbr = xm.WinnerAbbr,
                        Venue = xm.Venue,
                        City = xm.City,
                        LastSyncUtc = DateTime.UtcNow,
                        Active = true,
                        Created = DateTime.UtcNow
                    };
                    await _unitOfWork.Matches.InsertAsync(newMatch);
                    matchesByGameId[xm.EspnGameId] = newMatch;
                }
                else
                {
                    existingMatch.WeekId = weekEntity.Id;
                    existingMatch.DateUtc = xm.DateUtc;
                    existingMatch.HomeTeamId = homeTeam.Id;
                    existingMatch.AwayTeamId = awayTeam.Id;
                    existingMatch.StatusState = xm.StatusState;
                    existingMatch.StatusDesc = xm.StatusDesc;
                    existingMatch.HomeScore = xm.HomeScore;
                    existingMatch.AwayScore = xm.AwayScore;
                    existingMatch.WinnerAbbr = xm.WinnerAbbr;
                    existingMatch.Venue = xm.Venue;
                    existingMatch.City = xm.City;
                    existingMatch.LastSyncUtc = DateTime.UtcNow;

                    await _unitOfWork.Matches.UpdateAsync(existingMatch);
                    matchesByGameId[xm.EspnGameId] = existingMatch;
                }
            }
            await _unitOfWork.Save(cancellationToken);

            // Re-fetch match IDs if needed
            foreach (var kvp in matchesByGameId.ToList())
            {
                if (kvp.Value.Id == 0)
                {
                    var refreshed = await _unitOfWork.Matches.GetByEspnGameIdAsync(kvp.Key);
                    if (refreshed != null) matchesByGameId[kvp.Key] = refreshed;
                }
            }

            // 6. Create Quiniela
            var inviteCode = $"LMX26-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
            var quinielaName = string.IsNullOrWhiteSpace(request.QuinielaName)
                ? "Liga MX Clausura 2026"
                : request.QuinielaName.Trim();

            var quiniela = new Domain.Entities.Quiniela
            {
                Name = quinielaName,
                Description = "Quiniela migrada de Google Sheets Clausura 2026",
                LeagueId = league.Id,
                OwnerId = adminUserId,
                InviteCode = inviteCode,
                EntryFee = request.EntryFee,
                FirstPlacePct = request.FirstPlacePct,
                SecondPlacePct = request.SecondPlacePct,
                ThirdPlacePct = request.ThirdPlacePct,
                IsActive = true,
                Active = true,
                Created = DateTime.UtcNow
            };
            await _unitOfWork.Quinielas.InsertAsync(quiniela);
            await _unitOfWork.Save(cancellationToken);

            // 7. Users and QuinielaMembers
            var participantAliases = data.Standings.Select(s => s.PlayerAlias).Distinct().ToList();
            var membersByAlias = new Dictionary<string, QuinielaMember>(StringComparer.OrdinalIgnoreCase);
            var createdUsernames = new List<string>();

            var selectedAdminAlias = (request.AdminPlayerAlias ?? "Alex").Trim();

            foreach (var alias in participantAliases)
            {
                bool isAdminParticipant = string.Equals(alias, selectedAdminAlias, StringComparison.OrdinalIgnoreCase);
                User userEntity;

                if (isAdminParticipant)
                {
                    userEntity = adminUser;
                }
                else
                {
                    var cleanName = ClosedXmlParserService.RemoveDiacritics(alias).ToLowerInvariant().Replace(" ", "");
                    var email = $"{cleanName}@quiniela.local";

                    var existingUser = await _unitOfWork.Users.GetByEmailAsync(email);
                    if (existingUser == null)
                    {
                        existingUser = await _unitOfWork.Users.GetByUsernameAsync(cleanName);
                    }

                    if (existingUser == null)
                    {
                        var tempPassword = Guid.NewGuid().ToString("N")[..10] + "Aa1!";
                        existingUser = new User
                        {
                            Username = cleanName,
                            Email = email,
                            DisplayName = alias,
                            Role = "USER",
                            PasswordHash = _passwordHasher.Hash(tempPassword),
                            Active = true,
                            Created = DateTime.UtcNow
                        };
                        await _unitOfWork.Users.InsertAsync(existingUser);
                        await _unitOfWork.Save(cancellationToken);
                        createdUsernames.Add(existingUser.Username);
                    }

                    userEntity = existingUser;
                }

                var member = new QuinielaMember
                {
                    QuinielaId = quiniela.Id,
                    UserId = userEntity.Id,
                    Alias = alias,
                    Role = isAdminParticipant ? "OWNER" : "MEMBER",
                    PaidFee = false,
                    JoinedAt = DateTime.UtcNow,
                    Active = true,
                    Created = DateTime.UtcNow
                };

                await _unitOfWork.QuinielaMembers.InsertAsync(member);
                await _unitOfWork.Save(cancellationToken);
                membersByAlias[alias] = member;
            }

            // 8. Insert Picks
            var pickEntities = new List<Pick>();
            foreach (var xp in data.Picks)
            {
                if (!matchesByGameId.TryGetValue(xp.EspnGameId, out var matchEntity))
                {
                    continue;
                }
                if (!membersByAlias.TryGetValue(xp.PlayerAlias, out var memberEntity))
                {
                    continue;
                }

                pickEntities.Add(new Pick
                {
                    QuinielaId = quiniela.Id,
                    MemberId = memberEntity.Id,
                    MatchId = matchEntity.Id,
                    PickAbbr = xp.PickAbbr,
                    IsAutoFilled = false,
                    Active = true,
                    Created = DateTime.UtcNow
                });
            }

            await _unitOfWork.Picks.InsertRangeAsync(pickEntities);
            await _unitOfWork.Save(cancellationToken);

            // 9. Score weeks 1 to 9 and mathematical verification
            var quinielaMembersList = membersByAlias.Values.ToList();

            for (int w = 1; w <= 9; w++)
            {
                var weekEntity = weeksByNumber[w];
                var weekMatches = matchesByGameId.Values.Where(m => m.WeekId == weekEntity.Id).ToList();
                var weekPicks = pickEntities.Where(p => weekMatches.Any(m => m.Id == p.MatchId)).ToList();

                _scoringEngine.EvaluatePicksAndMatches(weekMatches, weekPicks, quinielaMembersList.Count, isFootball: false);

                var weeklyStandings = _scoringEngine.CalculateWeeklyStandings(quinielaMembersList, weekPicks, weekMatches);
                var awards = _scoringEngine.CalculateAwards(quiniela.Id, weekEntity.Id, weeklyStandings, weekMatches, weekPicks);

                foreach (var aw in awards)
                {
                    await _unitOfWork.WeeklyAwards.InsertAsync(aw);
                }
            }
            await _unitOfWork.Save(cancellationToken);

            // Mathematical verification against Standings sheet
            var verifications = new List<MigrationPlayerVerificationDto>();
            bool mathValid = true;

            foreach (var st in data.Standings)
            {
                if (!membersByAlias.TryGetValue(st.PlayerAlias, out var member))
                {
                    continue;
                }

                int calculatedHits = pickEntities.Count(p => p.MemberId == member.Id && p.IsHit == true);
                var verif = new MigrationPlayerVerificationDto
                {
                    Alias = st.PlayerAlias,
                    ExpectedHits = st.Hits,
                    CalculatedHits = calculatedHits
                };
                verifications.Add(verif);

                if (calculatedHits != st.Hits)
                {
                    mathValid = false;
                    _logger.LogError("Discrepancia en {Alias}: Calculado={Calc}, Esperado={Exp}", st.PlayerAlias, calculatedHits, st.Hits);
                }
            }

            if (!mathValid)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                response.isSuccess = false;
                response.Message = "Error de validación matemática: Los aciertos calculados por el motor no coinciden con la hoja Standings.";
                response.Data = new MigrationResultDto
                {
                    ValidationPassed = false,
                    Message = response.Message,
                    PlayerVerification = verifications
                };
                return response;
            }

            // Update member aggregated stats
            foreach (var member in quinielaMembersList)
            {
                var mHistorical = pickEntities.Where(p => p.MemberId == member.Id && p.IsHit.HasValue).ToList();
                member.TotalHits = mHistorical.Count(p => p.IsHit == true);
                member.TotalUpsets = mHistorical.Count(p => p.IsUpsetHit);
                member.TotalHumillaciones = mHistorical.Count(p => p.IsHumillacion);

                var (cur, best) = _scoringEngine.CalculateMemberStreaks(mHistorical);
                member.CurrentStreak = cur;
                member.BestStreak = best;
                member.LastModified = DateTime.UtcNow;

                await _unitOfWork.QuinielaMembers.UpdateAsync(member);
            }
            await _unitOfWork.Save(cancellationToken);

            // Commit transaction
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            response.isSuccess = true;
            response.Message = "Migración completada exitosamente con verificación matemática 100% aprobada.";
            response.Data = new MigrationResultDto
            {
                QuinielaId = quiniela.Id,
                QuinielaName = quiniela.Name,
                InviteCode = quiniela.InviteCode,
                TotalMembersMigrated = quinielaMembersList.Count,
                TotalMatchesMigrated = data.Matches.Count,
                TotalPicksMigrated = pickEntities.Count,
                CurrentWeekNumber = 10,
                ValidationPassed = true,
                Message = response.Message,
                PlayerVerification = verifications,
                CreatedUsernames = createdUsernames
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error durante la ejecución de la migración");
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            response.isSuccess = false;
            response.Message = $"Fallo crítico en migración: {ex.Message}";
        }

        return response;
    }
}
