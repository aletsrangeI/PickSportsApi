using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Configurations;

public class UserAvatarFileConfiguration : IEntityTypeConfiguration<UserAvatarFile>
{
    public void Configure(EntityTypeBuilder<UserAvatarFile> builder)
    {
        builder.ToTable("UserAvatarFiles");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.FileName).HasMaxLength(255).IsRequired();
        builder.Property(f => f.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(f => f.Data).IsRequired();

        builder.HasIndex(f => f.FileName).IsUnique();
        builder.HasIndex(f => f.UserId);
    }
}
