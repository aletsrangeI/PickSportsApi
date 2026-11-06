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
    private readonly IDuelApplication? _duelApplication;

    public GamificationApplication(
        IUnitOfWork unitOfWork,
        IAchievementApplication achievementApplication,
        IAppLogger<GamificationApplication>? logger = null,
        IDuelApplication? duelApplication = null)
    {
        _unitOfWork = unitOfWork;
        _achievementApplication = achievementApplication;
        _logger = logger;
        _duelApplication = duelApplication;
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

            // Cada paso se aisla del otro: un fallo en trofeos no debe saltarse los duelos ni al reves.
            await RunIsolatedAsync("trofeos", quinielaId, weekId, () => _achievementApplication.EvaluateWeekAsync(quinielaId, weekId));

            if (_duelApplication != null)
            {
                await RunIsolatedAsync("duelos", quinielaId, weekId, () => _duelApplication.ResolveWeekAsync(quinielaId, weekId));
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError("[Gamification] Error al resolver la jornada {0} de la quiniela {1}: {2}", weekId, quinielaId, ex.Message);
        }
    }

    private async Task RunIsolatedAsync(string step, int quinielaId, int weekId, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            _logger?.LogError("[Gamification] Error en {0} de la jornada {1} de la quiniela {2}: {3}", step, weekId, quinielaId, ex.Message);
        }
    }
}
