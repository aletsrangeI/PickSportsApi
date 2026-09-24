using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Configurations;

public class SportConfiguration : IEntityTypeConfiguration<Sport>
{
    public void Configure(EntityTypeBuilder<Sport> builder)
    {
        builder.ToTable("Sports");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Code).HasMaxLength(20).IsRequired();
        builder.Property(s => s.Name).HasMaxLength(50).IsRequired();
        builder.Property(s => s.Icon).HasMaxLength(50);
        builder.HasIndex(s => s.Code).IsUnique();
    }
}
