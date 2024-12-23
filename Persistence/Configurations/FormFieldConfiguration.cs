using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Configurations;

public class FormFieldConfiguration : IEntityTypeConfiguration<FormField>
{
    public void Configure(EntityTypeBuilder<FormField> builder)
    {
        builder.Property(t => t.Name).HasMaxLength(100).IsRequired();
        builder.Property(t => t.Type).HasMaxLength(100).IsRequired();
        builder.Property(t => t.Placeholder).HasMaxLength(100);
    }
}