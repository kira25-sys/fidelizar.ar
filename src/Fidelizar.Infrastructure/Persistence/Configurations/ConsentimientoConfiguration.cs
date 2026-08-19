using Fidelizar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fidelizar.Infrastructure.Persistence.Configurations;

/// <summary>
/// Append-only, like <see cref="MovimientoCreditoConfiguration"/> — I1's discipline extended to
/// consent (DATA-MODEL §3). F1-08 added the repository that actually enforces "no Update, no
/// Delete" for this table (<c>Fidelizar.Domain.Repositories.IConsentimientoRepository</c>,
/// implemented by <c>Fidelizar.Infrastructure.Repositories.ConsentimientoRepository</c>); this
/// class still only shapes the table.
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

        builder.Property(c => c.RegistradoPorUsuarioId);

        // FK to Usuario, no navigation property either side. Restrict: whoever recorded a
        // consent is never removable while it is still on record. Null for self-service and for
        // every row the phase-0 migration wrote.
        builder.HasOne<Usuario>().WithMany().HasForeignKey(c => c.RegistradoPorUsuarioId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(c => c.OcurridoEn)
            .HasColumnType("timestamptz")
            .IsRequired();
    }
}
