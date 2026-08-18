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

        builder.Property(c => c.DeclaradoPorUsuarioId).IsRequired();

        // FK to Usuario, no navigation property either side. Restrict: whoever declared a
        // cutoff is never removable while it is still on record.
        builder.HasOne<Usuario>().WithMany().HasForeignKey(c => c.DeclaradoPorUsuarioId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(c => c.DeclaradoEn)
            .HasColumnType("timestamptz")
            .IsRequired();
    }
}
