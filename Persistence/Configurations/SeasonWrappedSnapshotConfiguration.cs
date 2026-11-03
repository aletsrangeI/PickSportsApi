using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Configurations;

public class SeasonWrappedSnapshotConfiguration : IEntityTypeConfiguration<SeasonWrappedSnapshot>
{
    public void Configure(EntityTypeBuilder<SeasonWrappedSnapshot> builder)
    {
        builder.ToTable("SeasonWrappedSnapshots");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.WrappedPayloadJson).IsRequired().HasColumnType("text");
        builder.Property(s => s.GeneratedAtUtc).IsRequired();

        builder.HasIndex(s => new { s.QuinielaId, s.SeasonId, s.UserId }).IsUnique();

        builder.HasOne(s => s.Quiniela).WithMany().HasForeignKey(s => s.QuinielaId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(s => s.Season).WithMany().HasForeignKey(s => s.SeasonId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(s => s.User).WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
