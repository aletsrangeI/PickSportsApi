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

    /// <summary>
    /// Hay algun bracket enviado en una quiniela de esa temporada. Una quiniela sin SeasonId fijo resuelve la temporada vigente
    /// de su liga (ver QuinielaSeasonResolver), asi que tambien cuenta cuando seasonId es la vigente de esa liga.
    /// </summary>
    public async Task<bool> AnyBracketsForSeasonAsync(int seasonId, CancellationToken ct = default)
    {
        return await _context.LiguillaBrackets
            .AsNoTracking()
            .AnyAsync(b => b.Active
                && (b.Quiniela.SeasonId == seasonId
                    || (b.Quiniela.SeasonId == null
                        && _context.Seasons.Any(s => s.Id == seasonId && s.IsCurrent && s.LeagueId == b.Quiniela.LeagueId))), ct);
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

    public async Task<List<LiguillaBracketPick>> GetPicksByBracketAsync(int bracketId, CancellationToken ct = default)
    {
        return await _context.LiguillaBracketPicks
            .AsNoTracking()
            .Where(p => p.BracketId == bracketId)
            .OrderBy(p => p.RoundCode)
            .ThenBy(p => p.Slot)
            .ToListAsync(ct);
    }

    public async Task ReplacePicksAsync(int bracketId, IReadOnlyList<LiguillaBracketPick> picks, CancellationToken ct = default)
    {
        var existing = await _context.LiguillaBracketPicks
            .Where(p => p.BracketId == bracketId)
            .ToListAsync(ct);
        _context.LiguillaBracketPicks.RemoveRange(existing);

        foreach (var pick in picks)
        {
            pick.BracketId = bracketId;
        }

        await _context.LiguillaBracketPicks.AddRangeAsync(picks, ct);
    }

    public async Task AddNodeAsync(LiguillaNode node, CancellationToken ct = default)
    {
        AttachExistingTeams(node);
        await _context.LiguillaNodes.AddAsync(node, ct);
    }

    public Task UpdateNodeAsync(LiguillaNode node, CancellationToken ct = default)
    {
        AttachExistingTeams(node);
        _context.LiguillaNodes.Update(node);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Los equipos que llegan como navegacion de un nodo ya existen en la base y suelen venir desconectados (los repositorios leen con AsNoTracking).
    /// Sin esto, Add/Update recorre el grafo y EF los marca como nuevos (PK_Teams duplicada) o modificados. Se reutiliza la instancia ya rastreada
    /// si hay una con el mismo Id, y si no se adjunta como sin cambios.
    /// </summary>
    private void AttachExistingTeams(LiguillaNode node)
    {
        node.TeamA = ResolveExistingTeam(node.TeamA);
        node.TeamB = ResolveExistingTeam(node.TeamB);
        node.WinnerTeam = ResolveExistingTeam(node.WinnerTeam);
    }

    private Team? ResolveExistingTeam(Team? team)
    {
        if (team == null) return null;

        var tracked = _context.Teams.Local.FirstOrDefault(t => t.Id == team.Id);
        if (tracked != null) return tracked;

        _context.Teams.Attach(team);
        return team;
    }
}
