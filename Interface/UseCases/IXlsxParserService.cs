using DTO.Migration;

namespace Interface.UseCases;

public interface IXlsxParserService
{
    Task<XlsxMigrationData> ParseAsync(Stream stream, CancellationToken cancellationToken = default);
}
