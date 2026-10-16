using Domain.Entities;

namespace Interface.Persistence;

public interface ILiguillaRepository
{
    Task<List<LiguillaNode>> GetNodesBySeasonAsync(int seasonId, CancellationToken ct = default);
    Task<LiguillaNode?> GetNodeByIdAsync(int nodeId, CancellationToken ct = default);
    Task<LiguillaBracket?> GetBracketAsync(int quinielaId, int userId, CancellationToken ct = default);
    Task<List<LiguillaBracket>> GetAllBracketsForQuinielaAsync(int quinielaId, CancellationToken ct = default);
    Task AddBracketAsync(LiguillaBracket bracket, CancellationToken ct = default);
    Task UpdateBracketAsync(LiguillaBracket bracket, CancellationToken ct = default);
    Task AddNodeAsync(LiguillaNode node, CancellationToken ct = default);
    Task UpdateNodeAsync(LiguillaNode node, CancellationToken ct = default);
}
