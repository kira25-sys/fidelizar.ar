using Fidelizar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fidelizar.Infrastructure.Persistence.Configurations;

public sealed class MiembroConfiguration : IEntityTypeConfiguration<Miembro>
{
    public void Configure(EntityTypeBuilder<Miembro> builder)
    {
        builder.ToTable("Miembros");

        builder.HasKey(m => m.Id);

        // I8: NegocioId present, not nullable, indexed on every table.
        builder.Property(m => m.NegocioId).IsRequired();
        builder.HasIndex(m => m.NegocioId);

        // Nullable: a member registered at the counter starts unlinked (DATA-MODEL §3). text,
        // not int — other POS systems use non-numeric ids.
        builder.Property(m => m.ClienteExternoId);

        // Partial unique index: only rows with a linked ClienteExternoId are constrained, so
        // multiple unlinked counter registrations can coexist.
        builder.HasIndex(m => new { m.NegocioId, m.ClienteExternoId })
            .IsUnique()
            .HasFilter("\"ClienteExternoId\" IS NOT NULL");

        builder.Property(m => m.NumeroSocio);

        builder.Property(m => m.Nombre).IsRequired();

        builder.Property(m => m.NombreNormalizado).IsRequired();
        builder.HasIndex(m => m.NombreNormalizado);

        builder.Property(m => m.Telefono);

        builder.Property(m => m.Dni);

        builder.Property(m => m.FechaNacimiento).HasColumnType("date");

        builder.Property(m => m.SucursalId);

        builder.Property(m => m.FechaAlta)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(m => m.Activo).IsRequired();

        builder.Property(m => m.ActualizadoEn)
            .HasColumnType("timestamptz")
            .IsRequired();
    }
}
