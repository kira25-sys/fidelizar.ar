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

        builder.Property(m => m.UsuarioId);

        // FK to Usuario, no navigation property either side. Restrict: a movement's actor is
        // never removable while the ledger still points at it (I1).
        builder.HasOne<Usuario>().WithMany().HasForeignKey(m => m.UsuarioId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(m => m.Motivo);

        // Historical evidence only (I2) — never the source of a balance answer.
        builder.Property(m => m.SaldoResultante)
            .HasColumnType("numeric(14,2)")
            .IsRequired();

        builder.Property(m => m.ConfiguracionId);

        // README decision #6 (2026-08-19). Set on every client-POSTed movement — Canje (S4) and
        // the Ajuste an anulación writes (S8, 2026-08-21). The max length is generous for a
        // client-generated key (a GUID is 36 chars) without inviting an unbounded value onto an
        // indexed column.
        builder.Property(m => m.ClaveIdempotencia)
            .HasMaxLength(100);

        builder.HasIndex(m => new { m.NegocioId, m.MiembroId });

        builder.HasIndex(m => new { m.NegocioId, m.Periodo });

        // The same sale can never be credited twice — guaranteed by the index, not by
        // discipline. TipoMovimientoCredito.Acumulacion = 1 (DATA-MODEL §4).
        builder.HasIndex(m => new { m.NegocioId, m.MiembroId, m.Tipo, m.ReferenciaVenta })
            .IsUnique()
            .HasFilter($"\"Tipo\" = {(int)TipoMovimientoCredito.Acumulacion}")
            .HasDatabaseName("IX_MovimientosCredito_Acumulacion_Unica");

        // README decision #6 (2026-08-19): the guarantee against a double-written movement lives
        // here, in the database — a check-then-insert in code cannot close the race between two
        // simultaneous retries with the same key, this index can. It covers the whole ledger, not
        // just Canje, which is why S8's Ajuste needed no migration of its own.
        builder.HasIndex(m => new { m.NegocioId, m.ClaveIdempotencia })
            .IsUnique()
            .HasFilter("\"ClaveIdempotencia\" IS NOT NULL")
            .HasDatabaseName("IX_MovimientosCredito_NegocioId_ClaveIdempotencia");
    }
}
