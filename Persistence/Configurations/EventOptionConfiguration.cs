using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Configurations;

public class EventOptionConfiguration : IEntityTypeConfiguration<EventOption>
{
    public void Configure(EntityTypeBuilder<EventOption> builder)
    {
        builder.ToTable("EventOptions");
        builder.HasKey(eo => eo.Id);
        builder.Property(eo => eo.OptionText).HasMaxLength(200).IsRequired();
        builder.Property(eo => eo.ImageUrl).HasMaxLength(500);
        builder.Property(eo => eo.OptionOrder).HasDefaultValue(1);

        builder.HasOne(eo => eo.EventQuestion)
            .WithMany(eq => eq.Options)
            .HasForeignKey(eo => eo.EventQuestionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
