using Domain.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Configurations;

public class CatalogosConfiguration
{
    public void Configure(EntityTypeBuilder<Catalogo> builder)
    {
        builder.Property(t => t.Nombre).IsRequired();
        builder.Property(t => t.Descripcion).IsRequired();
    }
}