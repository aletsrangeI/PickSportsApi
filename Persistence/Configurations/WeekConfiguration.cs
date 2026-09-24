using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Configurations;

public class WeekConfiguration : IEntityTypeConfiguration<Week>
{
    public void Configure(EntityTypeBuilder<Week> builder)
    {
        builder.ToTable("Weeks");
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Name).HasMaxLength(50).IsRequired();
        builder.Property(w => w.Status).HasMaxLength(20).HasDefaultValue("DRAFT").IsRequired();

        builder.HasIndex(w => new { w.SeasonId, w.WeekNumber }).IsUnique();

        builder.HasOne(w => w.Season)
            .WithMany(s => s.Weeks)
            .HasForeignKey(w => w.SeasonId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
