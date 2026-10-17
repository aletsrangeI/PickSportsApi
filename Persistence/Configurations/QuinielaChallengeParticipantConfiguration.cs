using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Configurations;

public class QuinielaChallengeParticipantConfiguration : IEntityTypeConfiguration<QuinielaChallengeParticipant>
{
    public void Configure(EntityTypeBuilder<QuinielaChallengeParticipant> builder)
    {
        builder.ToTable("QuinielaChallengeParticipants");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Status).HasMaxLength(30).HasDefaultValue("PENDING").IsRequired();
        builder.Property(p => p.PredictedWinnerAbbr).HasMaxLength(20);
        builder.Property(p => p.Outcome).HasMaxLength(30).HasDefaultValue("NONE").IsRequired();
        builder.Property(p => p.IsDebtSettled).HasDefaultValue(false).IsRequired();

        // Restricción única requerida: un miembro solo puede figurar una vez por reto
        builder.HasIndex(p => new { p.ChallengeId, p.MemberId }).IsUnique();

        builder.HasOne(p => p.Challenge)
            .WithMany(c => c.Participants)
            .HasForeignKey(p => p.ChallengeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Member)
            .WithMany()
            .HasForeignKey(p => p.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.EliminatedAtMatch)
            .WithMany()
            .HasForeignKey(p => p.EliminatedAtMatchId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
