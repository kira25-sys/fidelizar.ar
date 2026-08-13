using Fidelizar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fidelizar.Infrastructure.Persistence.Configurations;

/// <summary>
/// The ledger's mapping. I1 (append-only) is enforced above this layer — by
/// <c>IMovimientoRepository</c> exposing no Update/Delete and by <c>MovimientoRepository</c>
/// never calling either — this class only shapes the columns and indexes DATA-MODEL §4 requires.
/// </summary>
public sealed class MovimientoCreditoConfiguration : IEntityTypeConfiguration<MovimientoCredito>
{
    public void Configure(EntityTypeBuilder<MovimientoCredito> builder)
    {
        builder.ToTable("MovimientosCredito");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).UseIdentityAlwaysColumn();

        builder.Property(m => m.NegocioId).IsRequired();

        builder.Property(m => m.MiembroId).IsRequired();

        // A business day, not an instant — date, never timestamp.
        builder.Property(m => m.FechaEfectiva)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(m => m.RegistradoEn)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(m => m.Periodo)
            .HasColumnType("char(7)")
            .HasMaxLength(7)
            .IsFixedLength()
            .IsRequired();

        // Persisted as int by EF Core's default enum conversion. Never reorder or reuse a
        // number in TipoMovimientoCredito (DATA-MODEL §4) — this column is why.
        builder.Property(m => m.Tipo)
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(m => m.Monto)
            .HasColumnType("numeric(14,2)")
            .IsRequired();

        builder.Property(m => m.ReferenciaVenta);

        // Scalar column, no navigation, no FK constraint: Usuario is F1-03 and does not exist
        // yet in this wave. F1-03 introduces Usuario and adds the constraint in a later migration.
        builder.Property(m => m.UsuarioId);

        builder.Property(m => m.Motivo);

        // Historical evidence only (I2) — never the source of a balance answer.
        builder.Property(m => m.SaldoResultante)
            .HasColumnType("numeric(14,2)")
            .IsRequired();

        builder.Property(m => m.ConfiguracionId);

        builder.HasIndex(m => new { m.NegocioId, m.MiembroId });

        builder.HasIndex(m => new { m.NegocioId, m.Periodo });

        // The same sale can never be credited twice — guaranteed by the index, not by
        // discipline. TipoMovimientoCredito.Acumulacion = 1 (DATA-MODEL §4).
        builder.HasIndex(m => new { m.NegocioId, m.MiembroId, m.Tipo, m.ReferenciaVenta })
            .IsUnique()
            .HasFilter($"\"Tipo\" = {(int)TipoMovimientoCredito.Acumulacion}")
            .HasDatabaseName("IX_MovimientosCredito_Acumulacion_Unica");
    }
}
