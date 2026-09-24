using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Configurations;

public class PickAuditLogConfiguration : IEntityTypeConfiguration<PickAuditLog>
{
    public void Configure(EntityTypeBuilder<PickAuditLog> builder)
    {
        builder.ToTable("PickAuditLogs");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.PickAbbr).HasMaxLength(10).IsRequired();
        builder.Property(l => l.Source).HasMaxLength(20).HasDefaultValue("MANUAL").IsRequired();
        builder.Property(l => l.Action).HasMaxLength(50).IsRequired();

        builder.HasIndex(l => new { l.QuinielaId, l.WeekId });
        builder.HasIndex(l => l.Timestamp);

        builder.HasOne(l => l.Quiniela)
            .WithMany(q => q.AuditLogs)
            .HasForeignKey(l => l.QuinielaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.Week)
            .WithMany(w => w.AuditLogs)
            .HasForeignKey(l => l.WeekId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.Member)
            .WithMany()
            .HasForeignKey(l => l.MemberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.Match)
            .WithMany(m => m.AuditLogs)
            .HasForeignKey(l => l.MatchId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
