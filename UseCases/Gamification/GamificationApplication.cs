using Common;
using Interface.Persistence;
using Interface.UseCases;

namespace UseCases.Gamification;

/// <summary>SPEC-013: punto de entrada de la gamificacion al calificar una jornada.</summary>
public class GamificationApplication : IGamificationApplication
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAchievementApplication _achievementApplication;
    private readonly IAppLogger<GamificationApplication>? _logger;

    public GamificationApplication(
        IUnitOfWork unitOfWork,
        IAchievementApplication achievementApplication,
        IAppLogger<GamificationApplication>? logger = null)
    {
        _unitOfWork = unitOfWork;
        _achievementApplication = achievementApplication;
        _logger = logger;
    }

    public async Task ResolveWeekAsync(int quinielaId, int weekId)
    {
        // La gamificacion es accesoria: un fallo aqui nunca debe impedir que se califique la jornada.
        try
        {
            var quiniela = await _unitOfWork.Quinielas.GetAsync(quinielaId);
            if (quiniela == null || !quiniela.GamificationEnabled) return;

            var week = await _unitOfWork.Weeks.GetAsync(weekId);
            if (week == null) return;

            // La Liguilla no tiene trofeos ni duelos.
            var league = quiniela.LeagueId.HasValue ? await _unitOfWork.Leagues.GetAsync(quiniela.LeagueId.Value) : null;
            if (league?.IsPlayoffWeek(week.WeekNumber) == true) return;

            // Recalificar una jornada de una temporada anterior no otorga trofeos (misma idea que SPEC-020).
            if (quiniela.SeasonId.HasValue && week.SeasonId != quiniela.SeasonId.Value) return;

            await _achievementApplication.EvaluateWeekAsync(quinielaId, weekId);

            // SPEC-013 Etapa 2: aqui se resolveran los duelos de la jornada (IDuelApplication.ResolveWeekAsync).
        }
        catch (Exception ex)
        {
            _logger?.LogError("[Gamification] Error al resolver la jornada {0} de la quiniela {1}: {2}", weekId, quinielaId, ex.Message);
        }
    }
}
