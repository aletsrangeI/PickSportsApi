namespace Interface.Persistence;

public interface IUnitOfWork : IDisposable
{
    IUserRepository Users { get; }
    ISportRepository Sports { get; }
    ILeagueRepository Leagues { get; }
    ITeamRepository Teams { get; }
    ISeasonRepository Seasons { get; }
    IWeekRepository Weeks { get; }
    IMatchRepository Matches { get; }
    IQuinielaRepository Quinielas { get; }
    IQuinielaMemberRepository QuinielaMembers { get; }
    IPickRepository Picks { get; }
    IWeeklyAwardRepository WeeklyAwards { get; }
    IPickAuditLogRepository PickAuditLogs { get; }
    IPushSubscriptionRepository PushSubscriptions { get; }
    IEspnHealthLogRepository EspnHealthLogs { get; }

    // Dynamic Form Repositories
    ICatalogoRepository Catalogos { get; }
    IContenidoCatalogoRepository ContenidoCatalogos { get; }
    IFormFieldRepository FormFields { get; }

    Task<int> Save(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}