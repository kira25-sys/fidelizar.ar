using System.Reflection;
using Fidelizar.Domain.Entities;

namespace Fidelizar.Domain.Tests.Entities;

/// <summary>
/// I3 — every correction is a new movement (<c>Ajuste</c>), never an edit: "leaves the original
/// row untouched, byte for byte". The mandatory-<c>Motivo</c> half of I3 is already covered by
/// <c>MovimientoCreditoTests.Canje_y_Ajuste_sin_Motivo_se_rechazan</c> and is not duplicated here.
///
/// This file covers the "untouched" half, at the strongest level available with no database
/// (ARCHITECTURE §11): a reflection check that <see cref="MovimientoCredito"/> exposes no public
/// way to mutate a row after <c>Crear</c> constructs it — so "the original stays untouched" is not
/// a promise a caller has to keep, it is something the type makes impossible to violate.
/// </summary>
public class MovimientoCreditoInmutabilidadTests
{
    private static readonly DateOnly Hoy = new(2026, 8, 12);

    [Fact]
    public void MovimientoCredito_no_tiene_ninguna_propiedad_publica_con_setter_publico()
    {
        var propiedadesConSetterPublico = typeof(MovimientoCredito)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.SetMethod is { IsPublic: true })
            .Select(p => p.Name)
            .ToArray();

        Assert.Empty(propiedadesConSetterPublico);
    }

    /// <summary>
    /// The only non-getter member declared on the type is the internal
    /// <see cref="MovimientoCredito.FijarSaldoResultante"/> (I2), reachable only from
    /// <c>Fidelizar.Infrastructure</c> and this test project via <c>InternalsVisibleTo</c> — never
    /// from public API surface. Every other public instance member is a compiler-generated
    /// property getter (<c>IsSpecialName</c>), which this excludes.
    /// </summary>
    [Fact]
    public void MovimientoCredito_no_declara_ningun_metodo_publico_de_instancia_que_pueda_mutarlo()
    {
        var metodosPublicosDeInstancia = typeof(MovimientoCredito)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .Select(m => m.Name)
            .ToArray();

        Assert.Empty(metodosPublicosDeInstancia);
    }

    /// <summary>
    /// Belt-and-suspenders behavioural check on top of the structural ones above: creating and
    /// appending an <c>Ajuste</c> that corrects an earlier movement must leave every field of the
    /// original exactly as it was — captured by reflection, so this catches any future field this
    /// test's author did not think to name individually.
    /// </summary>
    [Fact]
    public void Registrar_un_Ajuste_no_cambia_ningun_campo_del_movimiento_original()
    {
        var original = MovimientoCredito.Crear(
            negocioId: 1,
            miembroId: 1,
            fechaEfectiva: Hoy,
            registradoEn: DateTime.UtcNow,
            tipo: TipoMovimientoCredito.Acumulacion,
            monto: 500m,
            hoy: Hoy,
            configuracionId: 3,
            referenciaVenta: "VENTA-777");

        var valoresAntes = CapturarValores(original);

        // The correction: a brand new row, never a mutation of "original" above.
        var ajuste = MovimientoCredito.Crear(
            negocioId: 1,
            miembroId: 1,
            fechaEfectiva: Hoy,
            registradoEn: DateTime.UtcNow,
            tipo: TipoMovimientoCredito.Ajuste,
            monto: -500m,
            hoy: Hoy,
            motivo: "Corrección de prueba: venta anulada (I3)");

        var valoresDespues = CapturarValores(original);

        Assert.Equal(valoresAntes, valoresDespues);
        Assert.Equal(TipoMovimientoCredito.Acumulacion, original.Tipo);
        Assert.Equal(TipoMovimientoCredito.Ajuste, ajuste.Tipo);
        Assert.NotSame(original, ajuste);
    }

    private static Dictionary<string, object?> CapturarValores(MovimientoCredito movimiento) =>
        typeof(MovimientoCredito)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(p => p.Name, p => p.GetValue(movimiento));
}
