using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Configurations;

public class QuinielaChallengeConfiguration : IEntityTypeConfiguration<QuinielaChallenge>
{
    public void Configure(EntityTypeBuilder<QuinielaChallenge> builder)
    {
        builder.ToTable("QuinielaChallenges");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.ChallengeType).HasMaxLength(30).IsRequired();
        builder.Property(c => c.PayoutMode).HasMaxLength(30).HasDefaultValue("WINNER_TAKES_ALL").IsRequired();
        builder.Property(c => c.StakesDescription).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Status).HasMaxLength(30).HasDefaultValue("PENDING_ACCEPTANCE").IsRequired();
        builder.Property(c => c.ResolutionSummary).HasMaxLength(1000);
        builder.Property(c => c.DoubleLifeEventsJson).HasMaxLength(4000);

        builder.HasOne(c => c.Quiniela)
            .WithMany()
            .HasForeignKey(c => c.QuinielaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Week)
            .WithMany()
            .HasForeignKey(c => c.WeekId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.CreatorMember)
            .WithMany()
            .HasForeignKey(c => c.CreatorMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.TargetMatch)
            .WithMany()
            .HasForeignKey(c => c.TargetMatchId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(c => c.Participants)
            .WithOne(p => p.Challenge)
            .HasForeignKey(p => p.ChallengeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
