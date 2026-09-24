using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Configurations;

public class EspnHealthLogConfiguration : IEntityTypeConfiguration<EspnHealthLog>
{
    public void Configure(EntityTypeBuilder<EspnHealthLog> builder)
    {
        builder.ToTable("EspnHealthLogs");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.EndpointTested).HasMaxLength(250).IsRequired();

        builder.HasIndex(h => h.CheckedAt);
        builder.HasIndex(h => h.IsSuccess);
    }
}
