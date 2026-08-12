using Fidelizar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fidelizar.Infrastructure.Persistence.Configurations;

public sealed class CorteConfiguration : IEntityTypeConfiguration<Corte>
{
    public void Configure(EntityTypeBuilder<Corte> builder)
    {
        builder.ToTable("Cortes");

        builder.HasKey(c => c.Id);

        // Unique — the schema, not discipline, guarantees one cutoff per business (DATA-MODEL §4).
        builder.Property(c => c.NegocioId).IsRequired();
        builder.HasIndex(c => c.NegocioId).IsUnique();

        builder.Property(c => c.Fecha)
            .HasColumnType("date")
            .IsRequired();

        // Scalar column, no navigation, no FK constraint: Usuario is F1-03 and does not exist
        // yet in this wave. F1-03 introduces Usuario and adds the constraint in a later migration.
        builder.Property(c => c.DeclaradoPorUsuarioId).IsRequired();

        builder.Property(c => c.DeclaradoEn)
            .HasColumnType("timestamptz")
            .IsRequired();
    }
}
