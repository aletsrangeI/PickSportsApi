using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Configurations;

public class LeagueConfiguration : IEntityTypeConfiguration<League>
{
    public void Configure(EntityTypeBuilder<League> builder)
    {
        builder.ToTable("Leagues");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Code).HasMaxLength(50).IsRequired();
        builder.Property(l => l.Name).HasMaxLength(100).IsRequired();
        builder.Property(l => l.Country).HasMaxLength(50);
        builder.Property(l => l.LogoUrl).HasMaxLength(500);
        builder.Property(l => l.EspnApiPath).HasMaxLength(200).IsRequired();
        builder.Property(l => l.WeeksCount).HasDefaultValue(17);
        builder.Property(l => l.WeekDurationDays).HasDefaultValue(7);
        builder.Property(l => l.SportKey).HasMaxLength(20).HasDefaultValue("soccer");

        builder.HasIndex(l => l.Code).IsUnique();

        builder.HasOne(l => l.Sport)
            .WithMany(s => s.Leagues)
            .HasForeignKey(l => l.SportId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
