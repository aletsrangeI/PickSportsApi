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

    public DbSet<User> Users { get; set; }
    public DbSet<Catalogo> Catalogos { get; set; }
    public DbSet<ContenidoCatalogo> ContenidoCatalogos { get; set; }
    public DbSet<EquipoLiga> EquipoLigas { get; set; }
    
    public DbSet<FormField> FormFields { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().ToTable("Users");
        modelBuilder.Entity<Catalogo>().ToTable("Catalogos");
        modelBuilder.Entity<ContenidoCatalogo>().ToTable("ContenidoCatalgos");
        modelBuilder.Entity<EquipoLiga>().ToTable("EquipoLigas");
        modelBuilder.Entity<FormField>().ToTable("FormFields");
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