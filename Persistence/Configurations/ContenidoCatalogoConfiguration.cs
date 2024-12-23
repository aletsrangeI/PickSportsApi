using Domain.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Configurations;

public class ContenidoCatalogoConfiguration
{
    public void Configure(EntityTypeBuilder<ContenidoCatalogo> builder)
    {
        builder.Property(t => t.Nombre).IsRequired();
        builder.Property(t => t.Descripcion).IsRequired();
    }
}