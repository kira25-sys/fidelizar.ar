using Fidelizar.Domain.Entities;
using Fidelizar.SeederDesarrollo.Configuracion;
using Fidelizar.SeederDesarrollo.Datos;
using Fidelizar.SeederDesarrollo.Destino;

namespace Fidelizar.SeederDesarrollo.Tests;

/// <summary>
/// The decision about whether the target database may be written to at all. There is a Postgres
/// with the real balances of 293 members on this machine; these are the tests that stand between
/// a mistyped environment variable and an append-only ledger with no undo (I1).
/// </summary>
public class GuardasTests
{
    private static readonly ConteoBase ConteoConDatos = new(
        Negocios: 1, Sucursales: 5, Usuarios: 4, Miembros: 293, Movimientos: 8_142);

    private static Negocio NegocioAjeno() => new()
    {
        Nombre = "Otro negocio cualquiera",
        Cuit = "30-11111111-1",
        Activo = true,
    };

    private static Negocio NegocioSembradoPorEstaHerramienta() => new()
    {
        Nombre = DatosInventados.NegocioNombre,
        Cuit = DatosInventados.NegocioCuit,
        Domicilio = DatosInventados.NegocioDomicilio,
        Activo = true,
    };

    [Fact]
    public void Una_base_vacia_se_siembra()
    {
        var decision = Guardas.DecidirSobreBase(ConteoBase.Vacia, negocioExistente: null, permitirBaseNoVacia: false);

        Assert.True(decision.Continuar);
        Assert.Contains("vacía", decision.Mensaje);
    }

    [Fact]
    public void Una_base_con_datos_ajenos_se_planta_sin_la_bandera()
    {
        var decision = Guardas.DecidirSobreBase(ConteoConDatos, NegocioAjeno(), permitirBaseNoVacia: false);

        Assert.False(decision.Continuar);
        Assert.Contains("No se escribió nada.", decision.Mensaje);
        // The counts are printed so an operator who lands here sees immediately that this is not
        // an empty development database.
        Assert.Contains("293", decision.Mensaje);
        Assert.Contains("8142", decision.Mensaje);
    }

    [Fact]
    public void Una_base_con_datos_ajenos_sigue_solo_con_la_bandera_explicita()
    {
        var decision = Guardas.DecidirSobreBase(ConteoConDatos, NegocioAjeno(), permitirBaseNoVacia: true);

        Assert.True(decision.Continuar);
        Assert.Contains("--permitir-base-no-vacia", decision.Mensaje);
        Assert.Contains("No se borra ni se modifica", decision.Mensaje);
    }

    [Fact]
    public void Una_base_ya_sembrada_por_esta_herramienta_sigue_sin_bandera()
    {
        // The second run of the seeder: not empty, but recognisably its own work — the CUIT of
        // all zeros is the marker. It completes what is missing and duplicates nothing.
        var conteo = new ConteoBase(Negocios: 1, Sucursales: 2, Usuarios: 3, Miembros: 6, Movimientos: 10);

        var decision = Guardas.DecidirSobreBase(
            conteo, NegocioSembradoPorEstaHerramienta(), permitirBaseNoVacia: false);

        Assert.True(decision.Continuar);
        Assert.Contains("idempotente", decision.Mensaje);
    }

    [Fact]
    public void Dos_negocios_nunca_cuentan_como_base_propia_aunque_el_primero_tenga_el_CUIT_del_seeder()
    {
        var conteo = new ConteoBase(Negocios: 2, Sucursales: 2, Usuarios: 3, Miembros: 6, Movimientos: 10);

        var decision = Guardas.DecidirSobreBase(
            conteo, NegocioSembradoPorEstaHerramienta(), permitirBaseNoVacia: false);

        Assert.False(decision.Continuar);
    }

    [Theory]
    [InlineData("fidelizar_gate")]
    [InlineData("Fidelizar_Gate")]
    [InlineData("gate")]
    [InlineData("respaldo_gate_2026")]
    [InlineData("fidelizar_prod")]
    [InlineData("produccion")]
    [InlineData("octaviano_copia")]
    public void Los_nombres_de_base_prohibidos_se_rechazan(string nombre)
    {
        Assert.NotNull(Guardas.NombreProhibido(nombre));
    }

    [Theory]
    [InlineData("fidelizar_dev")]
    [InlineData("fidelizar_local")]
    [InlineData("pruebas")]
    public void Un_nombre_de_base_de_desarrollo_no_se_rechaza(string nombre)
    {
        Assert.Null(Guardas.NombreProhibido(nombre));
    }
}
