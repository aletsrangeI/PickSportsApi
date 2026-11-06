namespace Interface.UseCases;

public interface IGamificationApplication
{
    /// <summary>
    /// SPEC-013: se invoca al calificar una jornada. No hace nada si la quiniela no tiene GamificationEnabled
    /// o si la jornada es de Liguilla. No hace Save.
    /// </summary>
    Task ResolveWeekAsync(int quinielaId, int weekId);
}
