using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Configurations;

public class SeasonConfiguration : IEntityTypeConfiguration<Season>
{
    public void Configure(EntityTypeBuilder<Season> builder)
    {
        builder.ToTable("Seasons");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Name).HasMaxLength(100).IsRequired();
        builder.Property(s => s.StartDate).IsRequired(false);
        builder.Property(s => s.EndDate).IsRequired(false);

        builder.HasIndex(s => new { s.LeagueId, s.Year, s.SeasonType }).IsUnique();

        builder.HasOne(s => s.League)
            .WithMany(l => l.Seasons)
            .HasForeignKey(s => s.LeagueId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
