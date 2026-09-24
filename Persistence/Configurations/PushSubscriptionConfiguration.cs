using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Configurations;

public class PushSubscriptionConfiguration : IEntityTypeConfiguration<PushSubscription>
{
    public void Configure(EntityTypeBuilder<PushSubscription> builder)
    {
        builder.ToTable("PushSubscriptions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Endpoint).IsRequired();
        builder.Property(s => s.P256dhKey).IsRequired();
        builder.Property(s => s.AuthKey).IsRequired();
        builder.Property(s => s.DeviceDescription).HasMaxLength(200);

        builder.HasIndex(s => s.Endpoint).IsUnique();
        builder.HasIndex(s => s.UserId);

        builder.HasOne(s => s.User)
            .WithMany(u => u.PushSubscriptions)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
