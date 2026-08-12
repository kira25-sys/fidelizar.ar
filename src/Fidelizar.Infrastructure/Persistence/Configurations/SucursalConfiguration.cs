using Fidelizar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fidelizar.Infrastructure.Persistence.Configurations;

public sealed class SucursalConfiguration : IEntityTypeConfiguration<Sucursal>
{
    public void Configure(EntityTypeBuilder<Sucursal> builder)
    {
        builder.ToTable("Sucursales");

        builder.HasKey(s => s.Id);

        // I8: NegocioId present, not nullable, indexed on every table.
        builder.Property(s => s.NegocioId).IsRequired();
        builder.HasIndex(s => s.NegocioId);

        builder.Property(s => s.Nombre).IsRequired();

        builder.Property(s => s.CodigoExterno);

        builder.Property(s => s.Activa).IsRequired();
    }
}
