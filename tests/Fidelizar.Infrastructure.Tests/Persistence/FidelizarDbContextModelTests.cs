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
    [InlineData(typeof(Consentimiento), "RegistradoPorUsuarioId")]
    [InlineData(typeof(RegistroAuditoria), "UsuarioId")]
    public void Columnas_de_UsuarioId_tienen_FK_a_Usuario(Type tipoEntidad, string propiedad)
    {
        // The Usuario sistema row (Id=0, Rol=Sistema) migration made the FK safe: it is what
        // F0-09's UsuarioPlaceholderMigracion=0 already points at in Cortes and
        // ConfiguracionesPrograma for the 293 migrated members.
        var model = BuildModel();
        var entidad = model.FindEntityType(tipoEntidad)!;

        Assert.NotNull(entidad.FindProperty(propiedad));
        Assert.Contains(entidad.GetForeignKeys(), fk => fk.Properties.Any(p => p.Name == propiedad));
    }

    [Fact]
    public void Usuario_y_RegistroAuditoria_estan_mapeados()
    {
        var model = BuildModel();

        Assert.NotNull(model.FindEntityType(typeof(Usuario)));
        Assert.NotNull(model.FindEntityType(typeof(RegistroAuditoria)));
    }

    [Theory]
    [InlineData(typeof(Usuario), "NegocioId")]
    [InlineData(typeof(RegistroAuditoria), "NegocioId")]
    public void Usuario_y_RegistroAuditoria_tienen_NegocioId_no_nullable(Type tipoEntidad, string propiedad)
    {
        var entidad = GetEntity(BuildModel(), tipoEntidad);
        Assert.False(entidad.FindProperty(propiedad)!.IsNullable);
    }

    [Fact]
    public void Usuario_Email_es_citext()
    {
        var entidad = GetEntity<Usuario>(BuildModel());
        Assert.Equal("citext", entidad.FindProperty(nameof(Usuario.Email))!.GetColumnType());
    }

    [Fact]
    public void Usuario_tiene_indice_unico_por_NegocioId_y_Email()
    {
        var entidad = GetEntity<Usuario>(BuildModel());

        var indice = entidad.GetIndexes().Single(i =>
            i.Properties.Select(p => p.Name).SequenceEqual([nameof(Usuario.NegocioId), nameof(Usuario.Email)]));

        Assert.True(indice.IsUnique);
    }

    [Fact]
    public void Usuario_SucursalId_es_nullable_a_nivel_de_esquema()
    {
        // DATA-MODEL §2: la obligatoriedad de SucursalId depende del Rol (Cajero/Encargada sí,
        // Dueno/Soporte no) — Usuario.Crear la exige en Domain, no el esquema (mismo patrón que
        // ConfiguracionPrograma.ObjetivoMensual).
        var entidad = GetEntity<Usuario>(BuildModel());
        Assert.True(entidad.FindProperty(nameof(Usuario.SucursalId))!.IsNullable);
    }

    [Fact]
    public void RegistroAuditoria_UsuarioId_no_es_nullable()
    {
        var entidad = GetEntity<RegistroAuditoria>(BuildModel());
        Assert.False(entidad.FindProperty(nameof(RegistroAuditoria.UsuarioId))!.IsNullable);
    }

    [Fact]
    public void RegistroAuditoria_Detalle_es_jsonb_y_opcional()
    {
        var entidad = GetEntity<RegistroAuditoria>(BuildModel());
        var propiedad = entidad.FindProperty(nameof(RegistroAuditoria.Detalle))!;

        Assert.Equal("jsonb", propiedad.GetColumnType());
        Assert.True(propiedad.IsNullable);
    }

    [Fact]
    public void La_migracion_AddUsuarioYAuditoria_habilita_la_extension_citext()
    {
        // model.GetPostgresExtensions() reads an annotation the RuntimeModelConvention prunes
        // from DbContext.Model — it only affects migration SQL generation, never
        // SaveChanges/queries, so BuildModel()'s runtime model does not carry it (unlike every
        // other assertion in this file, which reads things the runtime model does keep). The
        // migration file itself is what Postgres actually applies, so it is the authoritative
        // check that the extension is created before Usuario.Email's citext column needs it.
        var migrationPath = Path.Combine(
            FindSolutionRoot(
                "src", "Fidelizar.Infrastructure", "Persistence", "Migrations",
                "20260813210033_AddUsuarioYAuditoria.cs"));

        var contenido = File.ReadAllText(migrationPath);

        Assert.Contains("PostgresExtension:citext", contenido, StringComparison.Ordinal);
    }

    [Fact]
    public void La_migracion_AddForeignKeysAUsuario_inserta_el_usuario_sistema_de_forma_idempotente()
    {
        // Raw SQL, so the runtime model (BuildModel()) carries none of it — the migration file
        // itself is what Postgres applies, same reasoning as the citext check above.
        var migrationPath = Path.Combine(
            FindSolutionRoot(
                "src", "Fidelizar.Infrastructure", "Persistence", "Migrations",
                "20260818150610_AddForeignKeysAUsuario.cs"));

        var contenido = File.ReadAllText(migrationPath);

        Assert.Contains("ON CONFLICT (\"Id\") DO NOTHING", contenido, StringComparison.Ordinal);
        Assert.Contains("(0, negocio_id,", contenido, StringComparison.Ordinal);
    }

    private static string FindSolutionRoot(params string[] relativePathFromRoot)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Fidelizar.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException(
                "Could not locate Fidelizar.sln by walking up from the test output directory.");
        }

        return Path.Combine([directory.FullName, .. relativePathFromRoot]);
    }

    private static IEntityType GetEntity(IModel model, Type tipoEntidad) => model.FindEntityType(tipoEntidad)!;
}
