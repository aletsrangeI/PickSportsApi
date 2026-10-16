using Domain.Entities;
using Interface.Persistence;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories;

public class CustomEventRepository : ICustomEventRepository
{
    private readonly ApplicationDbContext _context;

    public CustomEventRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<EventGroup> GetOrCreateEventGroupForQuinielaAsync(int quinielaId, string defaultTitle = "Eventos")
    {
        var group = await _context.EventGroups
            .FirstOrDefaultAsync(eg => eg.QuinielaId == quinielaId && eg.Active);

        if (group != null)
        {
            return group;
        }

        group = new EventGroup
        {
            QuinielaId = quinielaId,
            Title = defaultTitle,
            IsActive = true,
            Active = true,
            Created = DateTime.UtcNow,
            CreatedBy = "System"
        };

        await _context.EventGroups.AddAsync(group);
        await _context.SaveChangesAsync();
        return group;
    }

    public async Task<EventGroup?> GetEventGroupByQuinielaIdAsync(int quinielaId)
    {
        return await _context.EventGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(eg => eg.QuinielaId == quinielaId && eg.Active);
    }

    public async Task<IEnumerable<EventRound>> GetRoundsByQuinielaIdAsync(int quinielaId)
    {
        return await _context.EventRounds
            .AsNoTracking()
            .Include(r => r.EventGroup)
            .Include(r => r.Questions)
                .ThenInclude(q => q.Options)
            .Include(r => r.Questions)
                .ThenInclude(q => q.Picks)
            .Where(r => r.EventGroup.QuinielaId == quinielaId && r.Active)
            .OrderBy(r => r.RoundOrder)
            .ToListAsync();
    }

    public async Task<EventRound?> GetRoundByIdWithDetailsAsync(int roundId)
    {
        var round = await _context.EventRounds
            .AsNoTracking()
            .Include(r => r.EventGroup)
            .Include(r => r.Questions)
                .ThenInclude(q => q.Options)
            .Include(r => r.Questions)
                .ThenInclude(q => q.Picks)
                    .ThenInclude(p => p.QuinielaMember)
                        .ThenInclude(m => m.User)
            .Include(r => r.Questions)
                .ThenInclude(q => q.Picks)
                    .ThenInclude(p => p.EventOption)
            .FirstOrDefaultAsync(r => r.Id == roundId && r.Active);

        if (round != null)
        {
            round.Questions = round.Questions
                .OrderBy(q => q.QuestionOrder)
                .Select(q =>
                {
                    q.Options = q.Options.OrderBy(o => o.OptionOrder).ToList();
                    return q;
                })
                .ToList();
        }

        return round;
    }

    public async Task<EventRound?> GetRoundByIdAsync(int roundId)
    {
        return await _context.EventRounds
            .Include(r => r.EventGroup)
            .FirstOrDefaultAsync(r => r.Id == roundId && r.Active);
    }

    public async Task<bool> InsertRoundAsync(EventRound round)
    {
        await _context.EventRounds.AddAsync(round);
        return await _context.SaveChangesAsync() > 0;
    }

    public async Task<bool> UpdateRoundAsync(EventRound round)
    {
        _context.EventRounds.Update(round);
        return await _context.SaveChangesAsync() > 0;
    }

    public async Task<bool> InsertQuestionAsync(EventQuestion question)
    {
        await _context.EventQuestions.AddAsync(question);
        return await _context.SaveChangesAsync() > 0;
    }

    public async Task<bool> InsertOptionsAsync(IEnumerable<EventOption> options)
    {
        await _context.EventOptions.AddRangeAsync(options);
        return await _context.SaveChangesAsync() > 0;
    }

    public async Task<bool> UpsertPicksAsync(IEnumerable<EventPick> picks)
    {
        var pickList = picks.ToList();
        if (!pickList.Any()) return true;

        var questionIds = pickList.Select(p => p.EventQuestionId).Distinct().ToList();
        var memberIds = pickList.Select(p => p.QuinielaMemberId).Distinct().ToList();

        var existingPicks = await _context.EventPicks
            .Where(p => questionIds.Contains(p.EventQuestionId) && memberIds.Contains(p.QuinielaMemberId))
            .ToListAsync();

        foreach (var pick in pickList)
        {
            var existing = existingPicks.FirstOrDefault(p =>
                p.EventQuestionId == pick.EventQuestionId && p.QuinielaMemberId == pick.QuinielaMemberId);

            if (existing != null)
            {
                existing.EventOptionId = pick.EventOptionId;
                existing.SubmittedAtUtc = pick.SubmittedAtUtc == default ? DateTime.UtcNow : pick.SubmittedAtUtc;
                existing.IsHit = pick.IsHit;
                existing.PointsEarned = pick.PointsEarned;
                existing.Active = pick.Active;
                _context.EventPicks.Update(existing);
            }
            else
            {
                await _context.EventPicks.AddAsync(pick);
            }
        }

        return await _context.SaveChangesAsync() > 0;
    }

    public async Task<IEnumerable<EventPick>> GetAllEventPicksForQuinielaAsync(int quinielaId)
    {
        return await _context.EventPicks
            .AsNoTracking()
            .Include(p => p.EventQuestion)
                .ThenInclude(q => q.EventRound)
            .Include(p => p.EventOption)
            .Include(p => p.QuinielaMember)
                .ThenInclude(m => m.User)
            .Where(p => p.QuinielaMember.QuinielaId == quinielaId && p.Active)
            .ToListAsync();
    }

    public async Task<IEnumerable<EventPick>> GetPicksByRoundIdAsync(int roundId)
    {
        return await _context.EventPicks
            .AsNoTracking()
            .Include(p => p.EventQuestion)
            .Include(p => p.EventOption)
            .Include(p => p.QuinielaMember)
                .ThenInclude(m => m.User)
            .Where(p => p.EventQuestion.EventRoundId == roundId && p.Active)
            .ToListAsync();
    }

    public async Task<int> GetMaxRoundOrderByQuinielaIdAsync(int quinielaId)
    {
        var maxOrder = await _context.EventRounds
            .Where(r => r.EventGroup.QuinielaId == quinielaId && r.Active)
            .MaxAsync(r => (int?)r.RoundOrder);
        return maxOrder ?? 0;
    }

    public async Task<int> GetMaxQuestionOrderByRoundIdAsync(int roundId)
    {
        var maxOrder = await _context.EventQuestions
            .Where(q => q.EventRoundId == roundId && q.Active)
            .MaxAsync(q => (int?)q.QuestionOrder);
        return maxOrder ?? 0;
    }
}
