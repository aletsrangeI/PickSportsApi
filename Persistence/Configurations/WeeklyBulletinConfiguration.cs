using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Configurations;

public class WeeklyBulletinConfiguration : IEntityTypeConfiguration<WeeklyBulletin>
{
    public void Configure(EntityTypeBuilder<WeeklyBulletin> builder)
    {
        builder.ToTable("WeeklyBulletins");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.AdminAnnouncement).HasMaxLength(2000);
        builder.Property(b => b.PublishedAtUtc).IsRequired();
        builder.Property(b => b.IsPublished).IsRequired();

        builder.HasIndex(b => new { b.QuinielaId, b.WeekId }).IsUnique();

        builder.HasOne(b => b.Quiniela)
            .WithMany(q => q.WeeklyBulletins)
            .HasForeignKey(b => b.QuinielaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(b => b.Week)
            .WithMany(w => w.WeeklyBulletins)
            .HasForeignKey(b => b.WeekId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
