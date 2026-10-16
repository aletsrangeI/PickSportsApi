using Domain.Entities;
using Interface.Persistence;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories;

public class LiguillaRepository : ILiguillaRepository
{
    private readonly ApplicationDbContext _context;

    public LiguillaRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<LiguillaNode>> GetNodesBySeasonAsync(int seasonId, CancellationToken ct = default)
    {
        return await _context.LiguillaNodes
            .Include(n => n.TeamA)
            .Include(n => n.TeamB)
            .Include(n => n.WinnerTeam)
            .Where(n => n.SeasonId == seasonId && n.Active)
            .OrderBy(n => n.NodeNumber)
            .ToListAsync(ct);
    }

    public async Task<LiguillaNode?> GetNodeByIdAsync(int nodeId, CancellationToken ct = default)
    {
        return await _context.LiguillaNodes
            .Include(n => n.TeamA)
            .Include(n => n.TeamB)
            .Include(n => n.WinnerTeam)
            .FirstOrDefaultAsync(n => n.Id == nodeId && n.Active, ct);
    }

    public async Task<LiguillaBracket?> GetBracketAsync(int quinielaId, int userId, CancellationToken ct = default)
    {
        return await _context.LiguillaBrackets
            .Include(b => b.User)
            .FirstOrDefaultAsync(b => b.QuinielaId == quinielaId && b.UserId == userId && b.Active, ct);
    }

    public async Task<List<LiguillaBracket>> GetAllBracketsForQuinielaAsync(int quinielaId, CancellationToken ct = default)
    {
        return await _context.LiguillaBrackets
            .Include(b => b.User)
            .Where(b => b.QuinielaId == quinielaId && b.Active)
            .OrderByDescending(b => b.TotalScore)
            .ThenBy(b => b.SubmittedAtUtc)
            .ToListAsync(ct);
    }

    public async Task AddBracketAsync(LiguillaBracket bracket, CancellationToken ct = default)
    {
        await _context.LiguillaBrackets.AddAsync(bracket, ct);
    }

    public Task UpdateBracketAsync(LiguillaBracket bracket, CancellationToken ct = default)
    {
        _context.LiguillaBrackets.Update(bracket);
        return Task.CompletedTask;
    }

    public async Task AddNodeAsync(LiguillaNode node, CancellationToken ct = default)
    {
        await _context.LiguillaNodes.AddAsync(node, ct);
    }

    public Task UpdateNodeAsync(LiguillaNode node, CancellationToken ct = default)
    {
        _context.LiguillaNodes.Update(node);
        return Task.CompletedTask;
    }
}
