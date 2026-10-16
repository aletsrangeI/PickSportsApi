using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Configurations;

public class EventGroupConfiguration : IEntityTypeConfiguration<EventGroup>
{
    public void Configure(EntityTypeBuilder<EventGroup> builder)
    {
        builder.ToTable("EventGroups");
        builder.HasKey(eg => eg.Id);
        builder.Property(eg => eg.Title).HasMaxLength(200).IsRequired();
        builder.Property(eg => eg.Description).HasMaxLength(1000);
        builder.Property(eg => eg.IsActive).HasDefaultValue(true);

        builder.HasOne(eg => eg.Quiniela)
            .WithMany(q => q.EventGroups)
            .HasForeignKey(eg => eg.QuinielaId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
