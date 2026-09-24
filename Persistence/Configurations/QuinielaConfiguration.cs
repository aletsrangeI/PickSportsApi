using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Configurations;

public class QuinielaConfiguration : IEntityTypeConfiguration<Quiniela>
{
    public void Configure(EntityTypeBuilder<Quiniela> builder)
    {
        builder.ToTable("Quinielas");
        builder.HasKey(q => q.Id);
        builder.Property(q => q.Name).HasMaxLength(100).IsRequired();
        builder.Property(q => q.Description).HasMaxLength(500);
        builder.Property(q => q.InviteCode).HasMaxLength(12).IsRequired();
        builder.Property(q => q.EntryFee).HasPrecision(10, 2).HasDefaultValue(0.00m);
        builder.Property(q => q.FirstPlacePct).HasPrecision(5, 2).HasDefaultValue(70.00m);
        builder.Property(q => q.SecondPlacePct).HasPrecision(5, 2).HasDefaultValue(20.00m);
        builder.Property(q => q.ThirdPlacePct).HasPrecision(5, 2).HasDefaultValue(10.00m);

        builder.HasIndex(q => q.InviteCode).IsUnique();

        builder.HasOne(q => q.League)
            .WithMany(l => l.Quinielas)
            .HasForeignKey(q => q.LeagueId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(q => q.Owner)
            .WithMany(u => u.QuinielasOwned)
            .HasForeignKey(q => q.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
