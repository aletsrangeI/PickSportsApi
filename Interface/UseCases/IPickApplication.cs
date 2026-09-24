using Common;
using DTO.Pick;

namespace Interface.UseCases;

public interface IPickApplication
{
    Task<Response<PickDto>> SubmitPickAsync(int quinielaId, int userId, SubmitPickRequestDto request);
    Task<Response<QuinielaPicksResponseDto>> GetPicksAsync(int quinielaId, int weekId, int userId);
    Task<Response<LockAndAutofillResultDto>> LockAndAutofillAsync(int quinielaId, int weekId, int userId);
}
