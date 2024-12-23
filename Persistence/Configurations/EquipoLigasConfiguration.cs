using Domain.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Configurations;

public class EquipoLigasConfiguration
{
    public void Configure(EntityTypeBuilder<EquipoLiga> builder)
    {
        builder.Property(t => t.EquipoId).IsRequired();
        builder.Property(t => t.LigaId).IsRequired();
    }
}