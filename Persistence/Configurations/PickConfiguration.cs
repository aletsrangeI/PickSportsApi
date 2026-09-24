using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Configurations;

public class PickConfiguration : IEntityTypeConfiguration<Pick>
{
    public void Configure(EntityTypeBuilder<Pick> builder)
    {
        builder.ToTable("Picks");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.PickAbbr).HasMaxLength(10).IsRequired();

        // REGLA DE ORO DE QUINIELA.TXT: Índice Único Compuesto para prevenir carreras y duplicados
        builder.HasIndex(p => new { p.QuinielaId, p.MemberId, p.MatchId }).IsUnique();

        builder.HasOne(p => p.Quiniela)
            .WithMany(q => q.Picks)
            .HasForeignKey(p => p.QuinielaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Member)
            .WithMany(m => m.Picks)
            .HasForeignKey(p => p.MemberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Match)
            .WithMany(m => m.Picks)
            .HasForeignKey(p => p.MatchId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
