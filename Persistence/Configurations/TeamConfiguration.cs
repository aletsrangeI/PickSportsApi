using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Configurations;

public class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.ToTable("Teams");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.EspnTeamId).HasMaxLength(50).IsRequired();
        builder.Property(t => t.Name).HasMaxLength(100).IsRequired();
        builder.Property(t => t.Abbreviation).HasMaxLength(10).IsRequired();
        builder.Property(t => t.DisplayName).HasMaxLength(100).IsRequired();
        builder.Property(t => t.LogoUrl).HasMaxLength(500);
        builder.Property(t => t.PrimaryColor).HasMaxLength(10);

        builder.HasIndex(t => new { t.LeagueId, t.EspnTeamId }).IsUnique();
        builder.HasIndex(t => new { t.LeagueId, t.Abbreviation });

        builder.HasOne(t => t.League)
            .WithMany(l => l.Teams)
            .HasForeignKey(t => t.LeagueId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
