using Domain.Entities;
using Interface.Persistence;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories;

public class ChallengeRepository : GenericRepository<QuinielaChallenge>, IChallengeRepository
{
    public ChallengeRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<QuinielaChallenge?> GetByIdWithParticipantsAsync(int challengeId, CancellationToken ct = default)
    {
        return await _dbSet
            .Include(c => c.CreatorMember)
                .ThenInclude(m => m.User)
            .Include(c => c.Week)
            .Include(c => c.TargetMatch)
            .Include(c => c.Participants)
                .ThenInclude(p => p.Member)
                    .ThenInclude(m => m.User)
            .Include(c => c.Participants)
                .ThenInclude(p => p.EliminatedAtMatch)
            .FirstOrDefaultAsync(c => c.Id == challengeId, ct);
    }

    public async Task<List<QuinielaChallenge>> GetChallengesByWeekAsync(int quinielaId, int weekId, CancellationToken ct = default)
    {
        return await _dbSet.AsNoTracking()
            .Include(c => c.CreatorMember)
                .ThenInclude(m => m.User)
            .Include(c => c.Week)
            .Include(c => c.TargetMatch)
            .Include(c => c.Participants)
                .ThenInclude(p => p.Member)
                    .ThenInclude(m => m.User)
            .Include(c => c.Participants)
                .ThenInclude(p => p.EliminatedAtMatch)
            .Where(c => c.QuinielaId == quinielaId && c.WeekId == weekId)
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<List<QuinielaChallenge>> GetPendingInvitationsByMemberAsync(int quinielaId, int memberId, CancellationToken ct = default)
    {
        return await _dbSet.AsNoTracking()
            .Include(c => c.CreatorMember)
                .ThenInclude(m => m.User)
            .Include(c => c.Week)
            .Include(c => c.TargetMatch)
            .Include(c => c.Participants)
                .ThenInclude(p => p.Member)
                    .ThenInclude(m => m.User)
            .Where(c => c.QuinielaId == quinielaId
                        && (c.Status == "PENDING_ACCEPTANCE" || c.Status == "ACTIVE")
                        && c.Participants.Any(p => p.MemberId == memberId && p.Status == "PENDING"))
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<List<QuinielaChallenge>> GetActiveOrPendingChallengesByWeekAsync(int quinielaId, int weekId, CancellationToken ct = default)
    {
        return await _dbSet
            .Include(c => c.CreatorMember)
                .ThenInclude(m => m.User)
            .Include(c => c.Week)
            .Include(c => c.TargetMatch)
            .Include(c => c.Participants)
                .ThenInclude(p => p.Member)
                    .ThenInclude(m => m.User)
            .Include(c => c.Participants)
                .ThenInclude(p => p.EliminatedAtMatch)
            .Where(c => c.QuinielaId == quinielaId 
                        && c.WeekId == weekId 
                        && (c.Status == "ACTIVE" || c.Status == "PENDING_ACCEPTANCE"))
            .ToListAsync(ct);
    }

    public async Task<QuinielaChallengeParticipant?> GetParticipantAsync(int challengeId, int memberId, CancellationToken ct = default)
    {
        return await _context.Set<QuinielaChallengeParticipant>()
            .Include(p => p.Member)
                .ThenInclude(m => m.User)
            .Include(p => p.EliminatedAtMatch)
            .FirstOrDefaultAsync(p => p.ChallengeId == challengeId && p.MemberId == memberId, ct);
    }

    public async Task AddParticipantAsync(QuinielaChallengeParticipant participant, CancellationToken ct = default)
    {
        await _context.Set<QuinielaChallengeParticipant>().AddAsync(participant, ct);
    }
}
