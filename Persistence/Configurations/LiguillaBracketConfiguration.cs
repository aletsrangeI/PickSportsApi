using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Configurations;

public class LiguillaBracketConfiguration : IEntityTypeConfiguration<LiguillaBracket>
{
    public void Configure(EntityTypeBuilder<LiguillaBracket> builder)
    {
        builder.ToTable("LiguillaBrackets");
        builder.HasKey(b => b.Id);

        builder.HasIndex(b => new { b.QuinielaId, b.UserId }).IsUnique();

        builder.HasOne(b => b.Quiniela)
            .WithMany()
            .HasForeignKey(b => b.QuinielaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(b => b.User)
            .WithMany()
            .HasForeignKey(b => b.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
