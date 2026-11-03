using Common;
using DTO.Wrapped;

namespace Interface.UseCases;

public interface IWrappedApplication
{
    /// <summary>Wrapped del usuario autenticado. Lee snapshot; si no existe lo calcula y lo persiste.</summary>
    Task<Response<SeasonWrappedDto>> GetWrappedAsync(int quinielaId, int seasonId, int userId);

    /// <summary>Pre-calcula (o recalcula) los snapshots de todos los miembros. Solo OWNER/ADMIN de la quiniela o ADMIN global.</summary>
    Task<Response<WrappedGenerationResultDto>> GenerateAllAsync(int quinielaId, int seasonId, int requestingUserId);

    /// <summary>Ultima temporada finalizada de la liga de la quiniela donde el usuario es miembro y tiene picks.</summary>
    Task<Response<WrappedAvailabilityDto>> GetAvailabilityAsync(int quinielaId, int userId);

    /// <summary>Marca/desmarca la temporada como finalizada. Solo usuario con Role == "ADMIN".</summary>
    Task<Response<bool>> SetSeasonFinishedAsync(int seasonId, bool isFinished, int requestingUserId);
}
