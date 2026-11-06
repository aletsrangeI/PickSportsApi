using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Configurations;

public class UserAchievementConfiguration : IEntityTypeConfiguration<UserAchievement>
{
    public void Configure(EntityTypeBuilder<UserAchievement> builder)
    {
        builder.ToTable("UserAchievements");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Code).HasMaxLength(40).IsRequired();
        builder.Property(a => a.UnlockedAt).IsRequired();

        builder.HasIndex(a => new { a.UserId, a.Code, a.SeasonId }).IsUnique();
        builder.HasIndex(a => new { a.UserId, a.IsSeen });

        builder.HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(a => a.Quiniela).WithMany().HasForeignKey(a => a.QuinielaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.Season).WithMany().HasForeignKey(a => a.SeasonId).OnDelete(DeleteBehavior.Restrict);
    }
}
