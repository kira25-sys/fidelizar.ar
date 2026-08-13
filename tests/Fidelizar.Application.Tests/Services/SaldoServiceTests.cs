using Fidelizar.Application.Services;
using Fidelizar.Application.Tests.Fakes;
using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Exceptions;

namespace Fidelizar.Application.Tests.Services;

public class SaldoServiceTests
{
    private const int NegocioId = 1;
    private const int MiembroId = 42;
    private static readonly DateOnly Hoy = new(2026, 8, 12);

    private static SaldoService CrearServicio(out FakeMovimientoRepository repositorio)
    {
        repositorio = new FakeMovimientoRepository();
        return new SaldoService(repositorio);
    }

    /// <summary>I2: the balance is always <c>SUM(Monto)</c>, after an arbitrary sequence of
    /// operations — never a stored column.</summary>
    [Fact]
    public async Task Saldo_es_la_suma_de_los_movimientos_tras_una_secuencia_arbitraria()
    {
        var servicio = CrearServicio(out var repositorio);

        await repositorio.AppendAsync(MovimientoCredito.Crear(
            NegocioId, MiembroId, Hoy, DateTime.UtcNow, TipoMovimientoCredito.SaldoInicial, 1_000m, Hoy));
        await repositorio.AppendAsync(MovimientoCredito.Crear(
            NegocioId, MiembroId, Hoy, DateTime.UtcNow, TipoMovimientoCredito.Acumulacion, 300m, Hoy, configuracionId: 1));
        await repositorio.AppendAsync(MovimientoCredito.Crear(
            NegocioId, MiembroId, Hoy, DateTime.UtcNow, TipoMovimientoCredito.Canje, -400m, Hoy, motivo: "Canje de prueba"));
        await repositorio.AppendAsync(MovimientoCredito.Crear(
            NegocioId, MiembroId, Hoy, DateTime.UtcNow, TipoMovimientoCredito.Ajuste, -50m, Hoy, motivo: "Corrección de prueba"));

        var saldo = await servicio.ObtenerSaldoAsync(NegocioId, MiembroId);

        Assert.Equal(1_000m + 300m - 400m - 50m, saldo);
        Assert.Equal(repositorio.Movimientos.Where(m => m.NegocioId == NegocioId && m.MiembroId == MiembroId).Sum(m => m.Monto), saldo);
    }

    [Fact]
    public async Task Saldo_filtra_por_NegocioId_y_MiembroId()
    {
        var servicio = CrearServicio(out var repositorio);

        // Otro negocio, mismo MiembroId numérico — nunca debe mezclarse (I8).
        await repositorio.AppendAsync(MovimientoCredito.Crear(
            negocioId: 2, MiembroId, Hoy, DateTime.UtcNow, TipoMovimientoCredito.SaldoInicial, 999_999m, Hoy));
        await repositorio.AppendAsync(MovimientoCredito.Crear(
            NegocioId, MiembroId, Hoy, DateTime.UtcNow, TipoMovimientoCredito.SaldoInicial, 500m, Hoy));

        var saldo = await servicio.ObtenerSaldoAsync(NegocioId, MiembroId);

        Assert.Equal(500m, saldo);
    }

    /// <summary>I6 / RN-24: a redemption never exceeds the available balance.</summary>
    [Fact]
    public async Task Canje_mayor_al_saldo_se_rechaza()
    {
        var servicio = CrearServicio(out var repositorio);
        await repositorio.AppendAsync(MovimientoCredito.Crear(
            NegocioId, MiembroId, Hoy, DateTime.UtcNow, TipoMovimientoCredito.SaldoInicial, 100m, Hoy));

        var request = new RegistrarCanjeRequest(NegocioId, MiembroId, 150m, "Quiere canjear de más", null, Hoy, Hoy);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => servicio.RegistrarCanjeAsync(request));

        Assert.Equal("CANJE_SUPERA_SALDO", ex.ErrorCode);
        // Ninguna acción humana puede producir un saldo negativo: el intento no debe dejar rastro.
        Assert.DoesNotContain(repositorio.Movimientos, m => m.Tipo == TipoMovimientoCredito.Canje);
    }

    /// <summary>RN-25: while the balance is negative, every human redemption is blocked outright.</summary>
    [Fact]
    public async Task Canje_con_saldo_negativo_se_bloquea()
    {
        var servicio = CrearServicio(out var repositorio);
        // Solo un Ajuste generado por el sistema puede dejar el saldo en negativo (RN-25); acá
        // se simula ese estado ya alcanzado para probar el bloqueo posterior.
        await repositorio.AppendAsync(MovimientoCredito.Crear(
            NegocioId, MiembroId, Hoy, DateTime.UtcNow, TipoMovimientoCredito.Ajuste, -200m, Hoy,
            motivo: "Venta anulada después de canjeado el crédito (RN-25)"));

        var request = new RegistrarCanjeRequest(NegocioId, MiembroId, 10m, "Intento durante revisión", null, Hoy, Hoy);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => servicio.RegistrarCanjeAsync(request));

        Assert.Equal("SALDO_EN_REVISION", ex.ErrorCode);
    }

    [Fact]
    public async Task Canje_dentro_del_saldo_se_registra_como_movimiento_negativo()
    {
        var servicio = CrearServicio(out var repositorio);
        await repositorio.AppendAsync(MovimientoCredito.Crear(
            NegocioId, MiembroId, Hoy, DateTime.UtcNow, TipoMovimientoCredito.SaldoInicial, 1_000m, Hoy));

        var request = new RegistrarCanjeRequest(NegocioId, MiembroId, 300m, "Canje válido", 7, Hoy, Hoy);

        var movimiento = await servicio.RegistrarCanjeAsync(request);

        Assert.Equal(TipoMovimientoCredito.Canje, movimiento.Tipo);
        Assert.Equal(-300m, movimiento.Monto);
        Assert.Equal("Canje válido", movimiento.Motivo);
        Assert.Equal(700m, await servicio.ObtenerSaldoAsync(NegocioId, MiembroId));
    }

    [Fact]
    public async Task Canje_de_monto_cero_o_negativo_se_rechaza()
    {
        var servicio = CrearServicio(out _);

        var request = new RegistrarCanjeRequest(NegocioId, MiembroId, 0m, "Monto inválido", null, Hoy, Hoy);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => servicio.RegistrarCanjeAsync(request));
        Assert.Equal("MONTO_INVALIDO", ex.ErrorCode);
    }
}
