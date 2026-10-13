using DTO.Liguilla;

namespace Interface.UseCases;

public interface ILiguillaApplication
{
    Task<LiguillaOverviewDto> GetOverviewAsync(int quinielaId, int userId, bool isAdmin, CancellationToken ct = default);
    Task<LiguillaBracketDetailDto?> GetMyBracketAsync(int quinielaId, int userId, CancellationToken ct = default);
    Task<LiguillaBracketDetailDto> SubmitBracketAsync(int quinielaId, int userId, SubmitLiguillaBracketDto dto, CancellationToken ct = default);
    Task<LiguillaStandingsDto> GetStandingsAsync(int quinielaId, CancellationToken ct = default);
    Task<bool> ResolveNodeAsync(int quinielaId, ResolveLiguillaNodeRequestDto dto, CancellationToken ct = default);
    Task<bool> SeedSimulationNodesAsync(int quinielaId, CancellationToken ct = default);
}
