using Common;
using DTO.Bulletin;

namespace Interface.UseCases;

public interface IBulletinNarrativeService
{
    Task<BulletinNarrativeResultDto> GenerateChronicleAsync(BulletinNarrativeContextDto context, CancellationToken ct = default);
}
