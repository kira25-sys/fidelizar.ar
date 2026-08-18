using Fidelizar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fidelizar.Infrastructure.Persistence.Configurations;

/// <summary>
/// Replaces Octaviano's list of Telegram chat IDs (DATA-MODEL §2, Plan §5). <c>Email</c> is
/// <c>citext</c> so "Ana@x.com" and "ana@x.com" collide at the database level — a duplicate
/// account here is not a cosmetic annoyance the way it would be for a member's name, it is a
/// locked-out cashier or a silently-split audit trail.
/// </summary>
public sealed class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("Usuarios");

        builder.HasKey(u => u.Id);

        // I8: NegocioId present, not nullable, indexed on every table.
        builder.Property(u => u.NegocioId).IsRequired();
        builder.HasIndex(u => u.NegocioId);

        builder.Property(u => u.NombreCompleto).IsRequired();

        builder.Property(u => u.Email)
            .HasColumnType("citext")
            .IsRequired();

        // Unique per NegocioId, not global (DATA-MODEL §2): two different businesses may each
        // have a "duena@gmail.com" without colliding.
        builder.HasIndex(u => new { u.NegocioId, u.Email }).IsUnique();

        builder.Property(u => u.PasswordHash).IsRequired();

        builder.Property(u => u.Rol)
            .HasColumnType("integer")
            .IsRequired();

        // Nullable: required for Cajero/Encargada, null for Dueno/Soporte — enforced in
        // Usuario.Crear (DATA-MODEL §2), not at the schema level, the same way ConfiguracionPrograma
        // leaves ObjetivoMensual nullable and lets Domain decide when it may be null.
        builder.Property(u => u.SucursalId);

        builder.Property(u => u.Activo).IsRequired();

        builder.Property(u => u.CreadoEn)
            .HasColumnType("timestamptz")
            .IsRequired();
    }
}
