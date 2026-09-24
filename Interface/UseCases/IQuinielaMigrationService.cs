using Common;
using DTO.Migration;

namespace Interface.UseCases;

public interface IQuinielaMigrationService
{
    Task<Response<MigrationPreviewDto>> PreviewMigrationAsync(Stream fileStream, CancellationToken cancellationToken = default);
    Task<Response<MigrationResultDto>> ExecuteMigrationAsync(Stream fileStream, MigrationExecuteRequestDto request, int adminUserId, CancellationToken cancellationToken = default);
}
