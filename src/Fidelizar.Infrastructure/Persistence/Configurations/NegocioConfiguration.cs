using Fidelizar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fidelizar.Infrastructure.Persistence.Configurations;

public sealed class NegocioConfiguration : IEntityTypeConfiguration<Negocio>
{
    public void Configure(EntityTypeBuilder<Negocio> builder)
    {
        builder.ToTable("Negocios");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.Nombre).IsRequired();

        builder.Property(n => n.Cuit);

        builder.Property(n => n.Domicilio);

        builder.Property(n => n.Activo).IsRequired();

        builder.Property(n => n.CreadoEn)
            .HasColumnType("timestamptz")
            .IsRequired();
    }
}
