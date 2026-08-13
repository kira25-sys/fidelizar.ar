using Fidelizar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fidelizar.Infrastructure.Persistence.Configurations;

public sealed class ConfiguracionProgramaConfiguration : IEntityTypeConfiguration<ConfiguracionPrograma>
{
    public void Configure(EntityTypeBuilder<ConfiguracionPrograma> builder)
    {
        builder.ToTable("ConfiguracionesPrograma");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.NegocioId).IsRequired();

        // I8: plain index over every row, so it also covers the historical (closed) rows that
        // the partial index below deliberately excludes. A second HasIndex over the very same
        // property needs its name passed *in the HasIndex call itself* — calling the
        // property-expression-only overload twice returns the SAME builder both times and the
        // second call's configuration silently overwrites the first's instead of adding a
        // second index.
        builder.HasIndex(c => c.NegocioId, "IX_ConfiguracionesPrograma_NegocioId");

        // Partial unique index: exactly one current configuration per business, enforced by the
        // database, not by discipline (DATA-MODEL §1).
        builder.HasIndex(c => c.NegocioId, "IX_ConfiguracionesPrograma_NegocioId_Vigente")
            .IsUnique()
            .HasFilter("\"VigenteHasta\" IS NULL");

        builder.Property(c => c.PorcentajeAcumulacion)
            .HasColumnType("numeric(5,4)")
            .IsRequired();

        builder.Property(c => c.ObjetivoMensual).HasColumnType("numeric(14,2)");

        builder.Property(c => c.GraciaHabilitada)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(c => c.MesesDeGracia);

        builder.Property(c => c.UmbralMesMalo).HasColumnType("numeric(14,2)");

        builder.Property(c => c.TopeMesesCongelados);

        builder.Property(c => c.VigenteDesde)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(c => c.VigenteHasta).HasColumnType("date");

        // Scalar column, no navigation, no FK constraint: Usuario is F1-03 and does not exist
        // yet in this wave. F1-03 introduces Usuario and adds the constraint in a later migration.
        builder.Property(c => c.CreadoPorUsuarioId).IsRequired();
    }
}
