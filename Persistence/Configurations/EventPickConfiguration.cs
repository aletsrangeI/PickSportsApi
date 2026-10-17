using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Configurations;

public class EventPickConfiguration : IEntityTypeConfiguration<EventPick>
{
    public void Configure(EntityTypeBuilder<EventPick> builder)
    {
        builder.ToTable("EventPicks");
        builder.HasKey(ep => ep.Id);

        // Regla de Oro: Índice único compuesto para que un miembro solo tenga un pick por pregunta
        builder.HasIndex(ep => new { ep.EventQuestionId, ep.QuinielaMemberId }).IsUnique();

        builder.Property(ep => ep.PointsEarned).HasDefaultValue(0);

        builder.HasOne(ep => ep.EventQuestion)
            .WithMany(eq => eq.Picks)
            .HasForeignKey(ep => ep.EventQuestionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ep => ep.EventOption)
            .WithMany(eo => eo.Picks)
            .HasForeignKey(ep => ep.EventOptionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ep => ep.QuinielaMember)
            .WithMany(m => m.EventPicks)
            .HasForeignKey(ep => ep.QuinielaMemberId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
