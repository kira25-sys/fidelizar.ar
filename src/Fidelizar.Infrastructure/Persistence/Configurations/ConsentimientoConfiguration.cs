using Fidelizar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fidelizar.Infrastructure.Persistence.Configurations;

/// <summary>
/// Append-only, like <see cref="MovimientoCreditoConfiguration"/> — I1's discipline extended to
/// consent by convention (DATA-MODEL §3), even though F0-09 does not yet build the repository
/// that would enforce "no Update, no Delete" the way <c>IMovimientoRepository</c> does. That
/// repository is F1-08's job; this migration only shapes the table.
/// </summary>
public sealed class ConsentimientoConfiguration : IEntityTypeConfiguration<Consentimiento>
{
    public void Configure(EntityTypeBuilder<Consentimiento> builder)
    {
        builder.ToTable("Consentimientos");

        builder.HasKey(c => c.Id);

        // I8: NegocioId present, not nullable, indexed on every table.
        builder.Property(c => c.NegocioId).IsRequired();
        builder.HasIndex(c => c.NegocioId);

        builder.Property(c => c.MiembroId).IsRequired();

        // The current consent for a (MiembroId, Tipo) pair is the newest row — this index is
        // what makes that lookup cheap, not what enforces uniqueness (there is none: append-only).
        builder.HasIndex(c => new { c.MiembroId, c.Tipo });

        builder.Property(c => c.Tipo)
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(c => c.Otorgado).IsRequired();

        builder.Property(c => c.VersionTexto).IsRequired();

        builder.Property(c => c.Canal)
            .HasColumnType("integer")
            .IsRequired();

        // Scalar column, no navigation, no FK constraint: Usuario is F1-03 and does not exist
        // yet in this wave. F1-03 introduces Usuario and adds the constraint in a later migration.
        builder.Property(c => c.RegistradoPorUsuarioId);

        builder.Property(c => c.OcurridoEn)
            .HasColumnType("timestamptz")
            .IsRequired();
    }
}
