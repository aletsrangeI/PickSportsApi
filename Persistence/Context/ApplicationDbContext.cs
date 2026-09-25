using System.Reflection;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Persistence.Interceptors;

namespace Persistence.Context;

public class ApplicationDbContext : DbContext
{
    public readonly AuditableEntitySaveChangesInterceptor _auditableEntitySaveChangesInterceptor;

    static ApplicationDbContext()
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
    }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options,
        AuditableEntitySaveChangesInterceptor auditableEntitySaveChangesInterceptor) : base(options)
    {
        _auditableEntitySaveChangesInterceptor = auditableEntitySaveChangesInterceptor;
    }

    // Core Domain DbSets
    public DbSet<Sport> Sports { get; set; }
    public DbSet<League> Leagues { get; set; }
    public DbSet<Team> Teams { get; set; }
    public DbSet<Season> Seasons { get; set; }
    public DbSet<Week> Weeks { get; set; }
    public DbSet<Match> Matches { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Quiniela> Quinielas { get; set; }
    public DbSet<QuinielaMember> QuinielaMembers { get; set; }
    public DbSet<Pick> Picks { get; set; }
    public DbSet<WeeklyAward> WeeklyAwards { get; set; }
    public DbSet<PickAuditLog> PickAuditLogs { get; set; }
    public DbSet<PushSubscription> PushSubscriptions { get; set; }
    public DbSet<PushNotificationLog> PushNotificationLogs { get; set; }
    public DbSet<EspnHealthLog> EspnHealthLogs { get; set; }

    // Dynamic Form Catalog DbSets
    public DbSet<Catalogo> Catalogos { get; set; }
    public DbSet<ContenidoCatalogo> ContenidoCatalogos { get; set; }
    public DbSet<FormField> FormFields { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(modelBuilder);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(_auditableEntitySaveChangesInterceptor);
        optionsBuilder.EnableSensitiveDataLogging();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await base.SaveChangesAsync(cancellationToken);
    }
}