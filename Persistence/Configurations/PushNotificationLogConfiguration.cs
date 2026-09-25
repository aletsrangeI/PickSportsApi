using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Configurations;

public class PushNotificationLogConfiguration : IEntityTypeConfiguration<PushNotificationLog>
{
    public void Configure(EntityTypeBuilder<PushNotificationLog> builder)
    {
        builder.ToTable("PushNotificationLogs");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.NotificationType)
            .IsRequired()
            .HasMaxLength(60);

        builder.Property(l => l.DateLocal)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(l => l.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(l => l.Message)
            .IsRequired()
            .HasMaxLength(500);

        // Índice para búsqueda rápida de deduplicación
        builder.HasIndex(l => new { l.NotificationType, l.WeekId, l.QuinielaId, l.UserId, l.DateLocal });

        builder.HasOne(l => l.Week)
            .WithMany()
            .HasForeignKey(l => l.WeekId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.Quiniela)
            .WithMany()
            .HasForeignKey(l => l.QuinielaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.User)
            .WithMany()
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
