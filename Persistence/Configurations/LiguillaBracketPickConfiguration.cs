using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Configurations;

public class LiguillaBracketPickConfiguration : IEntityTypeConfiguration<LiguillaBracketPick>
{
    public void Configure(EntityTypeBuilder<LiguillaBracketPick> builder)
    {
        builder.ToTable("LiguillaBracketPicks");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.RoundCode).HasMaxLength(20).IsRequired();

        builder.HasIndex(p => new { p.BracketId, p.RoundCode, p.Slot }).IsUnique();
        builder.HasIndex(p => new { p.BracketId, p.RoundCode, p.TeamId }).IsUnique();

        builder.HasOne(p => p.Bracket)
            .WithMany()
            .HasForeignKey(p => p.BracketId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
