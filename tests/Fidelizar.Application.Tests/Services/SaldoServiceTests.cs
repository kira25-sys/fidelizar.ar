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

    private static SaldoService CrearServicio(out FakeMovimientoRepository repositorio) =>
        CrearServicio(out repositorio, out _);

    private static SaldoService CrearServicio(
        out FakeMovimientoRepository movimientoRepositorio, out FakeMiembroRepository miembroRepositorio)
    {
        movimientoRepositorio = new FakeMovimientoRepository();
        miembroRepositorio = new FakeMiembroRepository();
        // Most tests here exercise the balance math, not the existence check — a member is
        // seeded by default so ObtenerSaldoAsync's new lookup does not 404 them all. The tests
        // that exist specifically to prove the lookup opt out of it explicitly below.
        miembroRepositorio.SembrarNuevo(NegocioId, MiembroId);
        return new SaldoService(movimientoRepositorio, miembroRepositorio);
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

        var request = new RegistrarCanjeRequest(
            NegocioId, MiembroId, 150m, "Quiere canjear de más", null, Hoy, Hoy, "clave-supera-saldo");

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

        var request = new RegistrarCanjeRequest(
            NegocioId, MiembroId, 10m, "Intento durante revisión", null, Hoy, Hoy, "clave-saldo-negativo");

        var ex = await Assert.ThrowsAsync<ValidationException>(() => servicio.RegistrarCanjeAsync(request));

        Assert.Equal("SALDO_EN_REVISION", ex.ErrorCode);
    }

    [Fact]
    public async Task Canje_dentro_del_saldo_se_registra_como_movimiento_negativo()
    {
        var servicio = CrearServicio(out var repositorio);
        await repositorio.AppendAsync(MovimientoCredito.Crear(
            NegocioId, MiembroId, Hoy, DateTime.UtcNow, TipoMovimientoCredito.SaldoInicial, 1_000m, Hoy));

        var request = new RegistrarCanjeRequest(NegocioId, MiembroId, 300m, "Canje válido", 7, Hoy, Hoy, "clave-canje-valido");

        var movimiento = await servicio.RegistrarCanjeAsync(request);

        Assert.Equal(TipoMovimientoCredito.Canje, movimiento.Tipo);
        Assert.Equal(-300m, movimiento.Monto);
        Assert.Equal("Canje válido", movimiento.Motivo);
        Assert.Equal("clave-canje-valido", movimiento.ClaveIdempotencia);
        Assert.Equal(700m, await servicio.ObtenerSaldoAsync(NegocioId, MiembroId));
    }

    [Fact]
    public async Task Canje_de_monto_cero_o_negativo_se_rechaza()
    {
        var servicio = CrearServicio(out _);

        var request = new RegistrarCanjeRequest(NegocioId, MiembroId, 0m, "Monto inválido", null, Hoy, Hoy, "clave-monto-invalido");

        var ex = await Assert.ThrowsAsync<ValidationException>(() => servicio.RegistrarCanjeAsync(request));
        Assert.Equal("MONTO_INVALIDO", ex.ErrorCode);
    }

    /// <summary>
    /// The "$0 fantasma" fix: <c>SUM(Monto)</c> over zero rows is also 0, so without a lookup an
    /// unknown id and a real member with a genuinely empty ledger were indistinguishable. This is
    /// the negative half of that fix — an id nobody registered 404s instead of returning 0.
    /// </summary>
    [Fact]
    public async Task Saldo_de_un_miembro_inexistente_lanza_EntityNotFoundException()
    {
        var movimientoRepositorio = new FakeMovimientoRepository();
        var miembroRepositorio = new FakeMiembroRepository();
        var servicio = new SaldoService(movimientoRepositorio, miembroRepositorio);

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => servicio.ObtenerSaldoAsync(NegocioId, MiembroId));
    }

    /// <summary>I8: a member that exists, but for a different business, must read exactly like a
    /// member that does not exist at all — never a 200, never a hint that the id is real
    /// somewhere else.</summary>
    [Fact]
    public async Task Saldo_de_un_miembro_de_otro_negocio_lanza_EntityNotFoundException()
    {
        var movimientoRepositorio = new FakeMovimientoRepository();
        var miembroRepositorio = new FakeMiembroRepository();
        miembroRepositorio.SembrarNuevo(negocioId: 2, MiembroId);
        var servicio = new SaldoService(movimientoRepositorio, miembroRepositorio);

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => servicio.ObtenerSaldoAsync(NegocioId, MiembroId));
    }

    /// <summary>The positive half of the same fix: a real member whose ledger sums to exactly 0
    /// still gets a normal 200 with 0 — the lookup must not turn a legitimate zero into a 404.</summary>
    [Fact]
    public async Task Saldo_de_un_miembro_real_sin_movimientos_es_cero_y_no_lanza()
    {
        var servicio = CrearServicio(out _, out _);

        var saldo = await servicio.ObtenerSaldoAsync(NegocioId, MiembroId);

        Assert.Equal(0m, saldo);
    }
}
