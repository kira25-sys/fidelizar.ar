using Fidelizar.Domain.Entities;
using Fidelizar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Fidelizar.Infrastructure.Tests.Persistence;

/// <summary>
/// Asserts the EF Core model DATA-MODEL §3, §4 requires — column types, and both plain and
/// partial indexes — by inspecting <see cref="DbContext.Model"/> directly. Building the model
/// does not open a connection, so this runs with no database (ARCHITECTURE §11) and works in CI,
/// which has no Postgres service.
/// </summary>
public class FidelizarDbContextModelTests
{
    private static IModel BuildModel()
    {
        // Never opened: building DbContext.Model only inspects the configuration, it does not
        // connect. A real connection string is only needed for dotnet ef database update,
        // verified separately against the disposable container (see the PR description).
        var options = new DbContextOptionsBuilder<FidelizarDbContext>()
            .UseNpgsql("Host=localhost;Database=CAMBIAR_ESTO;Username=CAMBIAR_ESTO;Password=CAMBIAR_ESTO")
            .Options;

        using var dbContext = new FidelizarDbContext(options);
        return dbContext.Model;
    }

    private static IEntityType GetEntity<TEntity>(IModel model) => model.FindEntityType(typeof(TEntity))!;

    [Theory]
    [InlineData(typeof(Negocio))]
    [InlineData(typeof(Sucursal))]
    [InlineData(typeof(Miembro))]
    [InlineData(typeof(MovimientoCredito))]
    [InlineData(typeof(Corte))]
    [InlineData(typeof(ConfiguracionPrograma))]
    public void Las_seis_entidades_estan_mapeadas(Type tipoEntidad)
    {
        var model = BuildModel();
        Assert.NotNull(model.FindEntityType(tipoEntidad));
    }

    [Theory]
    [InlineData(typeof(Sucursal), "NegocioId")]
    [InlineData(typeof(Miembro), "NegocioId")]
    [InlineData(typeof(MovimientoCredito), "NegocioId")]
    [InlineData(typeof(Corte), "NegocioId")]
    [InlineData(typeof(ConfiguracionPrograma), "NegocioId")]
    public void NegocioId_no_es_nullable_en_cada_tabla(Type tipoEntidad, string propiedad)
    {
        var model = BuildModel();
        var entidad = model.FindEntityType(tipoEntidad)!;

        Assert.False(entidad.FindProperty(propiedad)!.IsNullable);
    }

    [Fact]
    public void Miembro_ClienteExternoId_es_texto_nullable()
    {
        var entidad = GetEntity<Miembro>(BuildModel());
        var propiedad = entidad.FindProperty(nameof(Miembro.ClienteExternoId))!;

        Assert.True(propiedad.IsNullable);
        Assert.Equal(typeof(string), propiedad.ClrType);
    }

    [Fact]
    public void Miembro_tiene_indice_unico_parcial_por_ClienteExternoId()
    {
        var entidad = GetEntity<Miembro>(BuildModel());

        var indice = entidad.GetIndexes().Single(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(["NegocioId", "ClienteExternoId"]));

        Assert.True(indice.IsUnique);
        Assert.Equal("\"ClienteExternoId\" IS NOT NULL", indice.GetFilter());
    }

    [Fact]
    public void MovimientoCredito_Monto_y_SaldoResultante_son_numeric_14_2()
    {
        var entidad = GetEntity<MovimientoCredito>(BuildModel());

        Assert.Equal("numeric(14,2)", entidad.FindProperty(nameof(MovimientoCredito.Monto))!.GetColumnType());
        Assert.Equal("numeric(14,2)", entidad.FindProperty(nameof(MovimientoCredito.SaldoResultante))!.GetColumnType());
    }

    [Fact]
    public void MovimientoCredito_FechaEfectiva_es_date_y_RegistradoEn_es_timestamptz()
    {
        var entidad = GetEntity<MovimientoCredito>(BuildModel());

        Assert.Equal("date", entidad.FindProperty(nameof(MovimientoCredito.FechaEfectiva))!.GetColumnType());
        Assert.Equal("timestamptz", entidad.FindProperty(nameof(MovimientoCredito.RegistradoEn))!.GetColumnType());
    }

    [Fact]
    public void MovimientoCredito_Periodo_es_char_7()
    {
        var entidad = GetEntity<MovimientoCredito>(BuildModel());
        var propiedad = entidad.FindProperty(nameof(MovimientoCredito.Periodo))!;

        Assert.Equal("char(7)", propiedad.GetColumnType());
        Assert.Equal(7, propiedad.GetMaxLength());
    }

    [Fact]
    public void MovimientoCredito_tiene_indices_por_MiembroId_y_por_Periodo()
    {
        var entidad = GetEntity<MovimientoCredito>(BuildModel());
        var indices = entidad.GetIndexes().ToList();

        Assert.Contains(indices, i => i.Properties.Select(p => p.Name).SequenceEqual(["NegocioId", "MiembroId"]));
        Assert.Contains(indices, i => i.Properties.Select(p => p.Name).SequenceEqual(["NegocioId", "Periodo"]));
    }

    [Fact]
    public void MovimientoCredito_tiene_indice_unico_parcial_de_acumulacion_por_venta()
    {
        var entidad = GetEntity<MovimientoCredito>(BuildModel());

        var indice = entidad.GetIndexes().Single(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(["NegocioId", "MiembroId", "Tipo", "ReferenciaVenta"]));

        Assert.True(indice.IsUnique);
        Assert.Equal("\"Tipo\" = 1", indice.GetFilter());
    }

    [Fact]
    public void Corte_tiene_indice_unico_por_NegocioId()
    {
        var entidad = GetEntity<Corte>(BuildModel());

        var indice = entidad.GetIndexes().Single(i => i.Properties.Select(p => p.Name).SequenceEqual(["NegocioId"]));

        Assert.True(indice.IsUnique);
    }

    [Fact]
    public void ConfiguracionPrograma_tiene_indice_unico_parcial_por_vigencia()
    {
        var entidad = GetEntity<ConfiguracionPrograma>(BuildModel());

        var indice = entidad.GetIndexes().Single(i => i.GetFilter() is not null);

        Assert.True(indice.IsUnique);
        Assert.Equal(["NegocioId"], indice.Properties.Select(p => p.Name));
        Assert.Equal("\"VigenteHasta\" IS NULL", indice.GetFilter());
    }

    [Fact]
    public void ConfiguracionPrograma_PorcentajeAcumulacion_es_numeric_5_4()
    {
        var entidad = GetEntity<ConfiguracionPrograma>(BuildModel());

        Assert.Equal(
            "numeric(5,4)",
            entidad.FindProperty(nameof(ConfiguracionPrograma.PorcentajeAcumulacion))!.GetColumnType());
    }

    [Theory]
    [InlineData(typeof(MovimientoCredito), "UsuarioId")]
    [InlineData(typeof(Corte), "DeclaradoPorUsuarioId")]
    [InlineData(typeof(ConfiguracionPrograma), "CreadoPorUsuarioId")]
    public void Columnas_de_UsuarioId_son_escalares_sin_relacion_de_clave_foranea(Type tipoEntidad, string propiedad)
    {
        // Usuario es F1-03 y todavía no existe: estas columnas deben quedar como int/int? sin
        // navegación ni FK, hasta que F1-03 agregue la restricción en una migración posterior.
        var model = BuildModel();
        var entidad = model.FindEntityType(tipoEntidad)!;

        Assert.NotNull(entidad.FindProperty(propiedad));
        Assert.DoesNotContain(entidad.GetForeignKeys(), fk => fk.Properties.Any(p => p.Name == propiedad));
    }
}
