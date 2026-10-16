using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Configurations;

public class EventRoundConfiguration : IEntityTypeConfiguration<EventRound>
{
    public void Configure(EntityTypeBuilder<EventRound> builder)
    {
        builder.ToTable("EventRounds");
        builder.HasKey(er => er.Id);
        builder.Property(er => er.Title).HasMaxLength(200).IsRequired();
        builder.Property(er => er.Status).HasMaxLength(30).HasDefaultValue("Open").IsRequired();
        builder.Property(er => er.RoundOrder).HasDefaultValue(1);

        builder.HasOne(er => er.EventGroup)
            .WithMany(eg => eg.Rounds)
            .HasForeignKey(er => er.EventGroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
