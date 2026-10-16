using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Configurations;

public class EventQuestionConfiguration : IEntityTypeConfiguration<EventQuestion>
{
    public void Configure(EntityTypeBuilder<EventQuestion> builder)
    {
        builder.ToTable("EventQuestions");
        builder.HasKey(eq => eq.Id);
        builder.Property(eq => eq.QuestionText).HasMaxLength(500).IsRequired();
        builder.Property(eq => eq.Description).HasMaxLength(1000);
        builder.Property(eq => eq.Points).HasDefaultValue(1);
        builder.Property(eq => eq.QuestionType).HasMaxLength(50).HasDefaultValue("SingleChoice").IsRequired();
        builder.Property(eq => eq.QuestionOrder).HasDefaultValue(1);

        builder.HasOne(eq => eq.EventRound)
            .WithMany(er => er.Questions)
            .HasForeignKey(eq => eq.EventRoundId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
