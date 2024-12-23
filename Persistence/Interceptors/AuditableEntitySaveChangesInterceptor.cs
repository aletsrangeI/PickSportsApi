using Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Persistence.Interceptors;

public class AuditableEntitySaveChangesInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        UpdateEntities(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        UpdateEntities(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public void UpdateEntities(DbContext? context)
    {
        if (context is null) return;

        foreach (var entry in context.ChangeTracker.Entries<BaseAuditableEntity>())
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedBy = "system";
                    entry.Entity.Created = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);
                    entry.Entity.LastModifiedBy = "system";
                    entry.Entity.LastModified = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);
                    break;
                case EntityState.Modified:
                    entry.Entity.LastModifiedBy = "system";
                    entry.Entity.LastModified = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);
                    entry.Entity.CreatedBy = "system";
                    break;
            }
    }
}