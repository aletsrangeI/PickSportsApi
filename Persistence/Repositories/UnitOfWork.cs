using Interface.Persistence;
using Persistence.Context;

namespace Persistence.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _dbContext;

    public UnitOfWork(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
        Users = new UserRepository(_dbContext);
        Sports = new SportRepository(_dbContext);
        Leagues = new LeagueRepository(_dbContext);
        Teams = new TeamRepository(_dbContext);
        Seasons = new SeasonRepository(_dbContext);
        Weeks = new WeekRepository(_dbContext);
        Matches = new MatchRepository(_dbContext);
        Quinielas = new QuinielaRepository(_dbContext);
        QuinielaMembers = new QuinielaMemberRepository(_dbContext);
        Picks = new PickRepository(_dbContext);
        WeeklyAwards = new WeeklyAwardRepository(_dbContext);
        PickAuditLogs = new PickAuditLogRepository(_dbContext);
        PushSubscriptions = new PushSubscriptionRepository(_dbContext);
        PushNotificationLogs = new PushNotificationLogRepository(_dbContext);
        EspnHealthLogs = new EspnHealthLogRepository(_dbContext);
        Catalogos = new CatalogoRepository(_dbContext);
        ContenidoCatalogos = new ContenidoCatalogoRepository(_dbContext);
        FormFields = new FormFieldRepository(_dbContext);
    }

    public IUserRepository Users { get; }
    public ISportRepository Sports { get; }
    public ILeagueRepository Leagues { get; }
    public ITeamRepository Teams { get; }
    public ISeasonRepository Seasons { get; }
    public IWeekRepository Weeks { get; }
    public IMatchRepository Matches { get; }
    public IQuinielaRepository Quinielas { get; }
    public IQuinielaMemberRepository QuinielaMembers { get; }
    public IPickRepository Picks { get; }
    public IWeeklyAwardRepository WeeklyAwards { get; }
    public IPickAuditLogRepository PickAuditLogs { get; }
    public IPushSubscriptionRepository PushSubscriptions { get; }
    public IPushNotificationLogRepository PushNotificationLogs { get; }
    public IEspnHealthLogRepository EspnHealthLogs { get; }
    public ICatalogoRepository Catalogos { get; }
    public IContenidoCatalogoRepository ContenidoCatalogos { get; }
    public IFormFieldRepository FormFields { get; }

    private Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? _currentTransaction;

    public async Task<int> Save(CancellationToken cancellationToken = default)
    {
        return await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_dbContext.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory")
        {
            _currentTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        }
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null)
        {
            await _currentTransaction.CommitAsync(cancellationToken);
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null)
        {
            await _currentTransaction.RollbackAsync(cancellationToken);
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }

    public void Dispose()
    {
        _currentTransaction?.Dispose();
        GC.SuppressFinalize(this);
    }
}