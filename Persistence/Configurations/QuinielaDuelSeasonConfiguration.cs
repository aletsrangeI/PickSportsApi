using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Configurations;

public class QuinielaDuelSeasonConfiguration : IEntityTypeConfiguration<QuinielaDuelSeason>
{
    public void Configure(EntityTypeBuilder<QuinielaDuelSeason> builder)
    {
        builder.ToTable("QuinielaDuelSeasons");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.CupName).HasMaxLength(60);
        builder.Property(s => s.PunishmentText).HasMaxLength(200);

        builder.HasIndex(s => s.QuinielaId).IsUnique();

        builder.HasOne(s => s.Quiniela).WithMany().HasForeignKey(s => s.QuinielaId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(s => s.ChampionMember).WithMany().HasForeignKey(s => s.ChampionMemberId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
    }
}
