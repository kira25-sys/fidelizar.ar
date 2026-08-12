using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Exceptions;

namespace Fidelizar.Domain.Tests.Entities;

/// <summary>
/// DATA-MODEL §4's easy-to-get-wrong points, tested with no database (ARCHITECTURE §3, §11).
/// </summary>
public class MovimientoCreditoTests
{
    private static readonly DateOnly Hoy = new(2026, 8, 12);

    [Fact]
    public void Acumulacion_sin_ConfiguracionId_se_rechaza()
    {
        var ex = Assert.Throws<ValidationException>(() => MovimientoCredito.Crear(
            negocioId: 1,
            miembroId: 1,
            fechaEfectiva: Hoy,
            registradoEn: DateTime.UtcNow,
            tipo: TipoMovimientoCredito.Acumulacion,
            monto: 100m,
            hoy: Hoy,
            configuracionId: null));

        Assert.Equal("CONFIGURACION_REQUERIDA", ex.ErrorCode);
    }

    [Fact]
    public void Acumulacion_con_ConfiguracionId_se_acepta()
    {
        var movimiento = MovimientoCredito.Crear(
            negocioId: 1,
            miembroId: 1,
            fechaEfectiva: Hoy,
            registradoEn: DateTime.UtcNow,
            tipo: TipoMovimientoCredito.Acumulacion,
            monto: 100m,
            hoy: Hoy,
            configuracionId: 7);

        Assert.Equal(7, movimiento.ConfiguracionId);
    }

    [Theory]
    [InlineData(TipoMovimientoCredito.Canje)]
    [InlineData(TipoMovimientoCredito.Ajuste)]
    public void Canje_y_Ajuste_sin_Motivo_se_rechazan(TipoMovimientoCredito tipo)
    {
        var ex = Assert.Throws<ValidationException>(() => MovimientoCredito.Crear(
            negocioId: 1,
            miembroId: 1,
            fechaEfectiva: Hoy,
            registradoEn: DateTime.UtcNow,
            tipo: tipo,
            monto: -50m,
            hoy: Hoy,
            motivo: null));

        Assert.Equal("MOTIVO_REQUERIDO", ex.ErrorCode);
    }

    [Fact]
    public void Movimiento_retroactivo_sin_Motivo_se_rechaza()
    {
        var ayer = Hoy.AddDays(-1);

        var ex = Assert.Throws<ValidationException>(() => MovimientoCredito.Crear(
            negocioId: 1,
            miembroId: 1,
            fechaEfectiva: ayer,
            registradoEn: DateTime.UtcNow,
            tipo: TipoMovimientoCredito.SaldoInicial,
            monto: 100m,
            hoy: Hoy,
            motivo: null));

        Assert.Equal("MOTIVO_REQUERIDO", ex.ErrorCode);
    }

    [Fact]
    public void Movimiento_de_hoy_no_requiere_Motivo_salvo_que_sea_Canje_o_Ajuste()
    {
        var movimiento = MovimientoCredito.Crear(
            negocioId: 1,
            miembroId: 1,
            fechaEfectiva: Hoy,
            registradoEn: DateTime.UtcNow,
            tipo: TipoMovimientoCredito.SaldoInicial,
            monto: 100m,
            hoy: Hoy,
            motivo: null);

        Assert.Null(movimiento.Motivo);
    }

    [Fact]
    public void Periodo_se_deriva_de_FechaEfectiva()
    {
        var movimiento = MovimientoCredito.Crear(
            negocioId: 1,
            miembroId: 1,
            fechaEfectiva: new DateOnly(2026, 3, 5),
            registradoEn: DateTime.UtcNow,
            tipo: TipoMovimientoCredito.SaldoInicial,
            monto: 100m,
            hoy: Hoy,
            motivo: "Carga inicial de ejemplo");

        Assert.Equal("2026-03", movimiento.Periodo);
    }
}
