using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Configurations;

public class SystemConfigConfiguration : IEntityTypeConfiguration<SystemConfig>
{
    public void Configure(EntityTypeBuilder<SystemConfig> builder)
    {
        builder.ToTable("SystemConfigs");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Key).HasMaxLength(150).IsRequired();
        builder.Property(c => c.Value).HasMaxLength(1000).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(500);
        builder.Property(c => c.IsPublic).HasDefaultValue(true);

        builder.HasIndex(c => c.Key).IsUnique();
    }
}
