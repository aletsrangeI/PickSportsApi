using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Configurations;

public class MatchConfiguration : IEntityTypeConfiguration<Match>
{
    public void Configure(EntityTypeBuilder<Match> builder)
    {
        builder.ToTable("Matches");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.EspnGameId).HasMaxLength(50).IsRequired();
        builder.Property(m => m.StatusState).HasMaxLength(20).HasDefaultValue("pre").IsRequired();
        builder.Property(m => m.StatusDesc).HasMaxLength(100);
        builder.Property(m => m.WinnerAbbr).HasMaxLength(10);
        builder.Property(m => m.Venue).HasMaxLength(150);
        builder.Property(m => m.City).HasMaxLength(100);
        builder.Property(m => m.PostponedToDate).IsRequired(false);

        builder.HasIndex(m => m.EspnGameId).IsUnique();
        builder.HasIndex(m => m.WeekId);
        builder.HasIndex(m => m.DateUtc);

        builder.HasOne(m => m.Week)
            .WithMany(w => w.Matches)
            .HasForeignKey(m => m.WeekId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.HomeTeam)
            .WithMany(t => t.HomeMatches)
            .HasForeignKey(m => m.HomeTeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.AwayTeam)
            .WithMany(t => t.AwayMatches)
            .HasForeignKey(m => m.AwayTeamId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
