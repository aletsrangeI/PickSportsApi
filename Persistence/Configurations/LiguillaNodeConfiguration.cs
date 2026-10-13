using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Configurations;

public class LiguillaNodeConfiguration : IEntityTypeConfiguration<LiguillaNode>
{
    public void Configure(EntityTypeBuilder<LiguillaNode> builder)
    {
        builder.ToTable("LiguillaNodes");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Round).HasMaxLength(20).IsRequired();

        builder.HasIndex(n => new { n.SeasonId, n.NodeNumber }).IsUnique();

        builder.HasOne(n => n.Season)
            .WithMany()
            .HasForeignKey(n => n.SeasonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(n => n.TeamA)
            .WithMany()
            .HasForeignKey(n => n.TeamAId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(n => n.TeamB)
            .WithMany()
            .HasForeignKey(n => n.TeamBId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(n => n.WinnerTeam)
            .WithMany()
            .HasForeignKey(n => n.WinnerTeamId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
