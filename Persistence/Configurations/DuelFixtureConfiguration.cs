using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Configurations;

public class DuelFixtureConfiguration : IEntityTypeConfiguration<DuelFixture>
{
    public void Configure(EntityTypeBuilder<DuelFixture> builder)
    {
        builder.ToTable("DuelFixtures");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Result).HasMaxLength(10);

        builder.HasIndex(f => new { f.QuinielaId, f.WeekNumber, f.HomeMemberId }).IsUnique();
        builder.HasIndex(f => new { f.QuinielaId, f.WeekNumber });

        builder.HasOne(f => f.Quiniela).WithMany().HasForeignKey(f => f.QuinielaId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(f => f.HomeMember).WithMany().HasForeignKey(f => f.HomeMemberId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(f => f.AwayMember).WithMany().HasForeignKey(f => f.AwayMemberId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
    }
}
