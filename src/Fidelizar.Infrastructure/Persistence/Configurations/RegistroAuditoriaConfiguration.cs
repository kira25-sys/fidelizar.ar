using Fidelizar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fidelizar.Infrastructure.Persistence.Configurations;

/// <summary>
/// Append-only, like <see cref="MovimientoCreditoConfiguration"/> (DATA-MODEL §2) — even though
/// F1-03 does not yet build any code path that writes here; see
/// <see cref="Fidelizar.Domain.Repositories.IRegistroAuditoriaRepository"/>.
/// </summary>
public sealed class RegistroAuditoriaConfiguration : IEntityTypeConfiguration<RegistroAuditoria>
{
    public void Configure(EntityTypeBuilder<RegistroAuditoria> builder)
    {
        builder.ToTable("RegistrosAuditoria");

        builder.HasKey(r => r.Id);

        // I8: NegocioId present, not nullable, indexed on every table.
        builder.Property(r => r.NegocioId).IsRequired();
        builder.HasIndex(r => r.NegocioId);

        builder.Property(r => r.UsuarioId).IsRequired();
        builder.HasIndex(r => new { r.NegocioId, r.UsuarioId });

        // FK to Usuario, no navigation property either side. No placeholder data exists here
        // (this table has no writer yet), so the constraint is added directly, unlike the four
        // columns F0-09 already wrote UsuarioPlaceholderMigracion into.
        builder.HasOne<Usuario>().WithMany().HasForeignKey(r => r.UsuarioId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(r => r.Accion).IsRequired();

        builder.Property(r => r.EntidadTipo);

        builder.Property(r => r.EntidadId);

        builder.Property(r => r.Detalle).HasColumnType("jsonb");

        builder.Property(r => r.OcurridoEn)
            .HasColumnType("timestamptz")
            .IsRequired();
    }
}
