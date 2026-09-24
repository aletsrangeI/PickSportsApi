using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Configurations;

public class QuinielaMemberConfiguration : IEntityTypeConfiguration<QuinielaMember>
{
    public void Configure(EntityTypeBuilder<QuinielaMember> builder)
    {
        builder.ToTable("QuinielaMembers");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Alias).HasMaxLength(50).IsRequired();
        builder.Property(m => m.Role).HasMaxLength(20).HasDefaultValue("MEMBER").IsRequired();

        builder.HasIndex(m => new { m.QuinielaId, m.UserId }).IsUnique();

        builder.HasOne(m => m.Quiniela)
            .WithMany(q => q.Members)
            .HasForeignKey(m => m.QuinielaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.User)
            .WithMany(u => u.Memberships)
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
