using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Configurations;

public class WeeklyAwardConfiguration : IEntityTypeConfiguration<WeeklyAward>
{
    public void Configure(EntityTypeBuilder<WeeklyAward> builder)
    {
        builder.ToTable("WeeklyAwards");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.AwardType).HasMaxLength(30).IsRequired();
        builder.Property(a => a.AwardValue1).HasMaxLength(100).IsRequired();
        builder.Property(a => a.AwardValue2).HasMaxLength(100);
        builder.Property(a => a.Notes).HasMaxLength(500);

        builder.HasIndex(a => new { a.QuinielaId, a.WeekId, a.AwardType });

        builder.HasOne(a => a.Quiniela)
            .WithMany(q => q.WeeklyAwards)
            .HasForeignKey(a => a.QuinielaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Week)
            .WithMany(w => w.WeeklyAwards)
            .HasForeignKey(a => a.WeekId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Member)
            .WithMany(m => m.WeeklyAwards)
            .HasForeignKey(a => a.MemberId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
